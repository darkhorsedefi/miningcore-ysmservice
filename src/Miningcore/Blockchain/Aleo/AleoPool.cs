using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Autofac;
using AutoMapper;
using Microsoft.IO;
using Miningcore.Blockchain.Aleo.Configuration;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Nicehash;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Persistence.Repositories;
using Miningcore.Stratum;
using Miningcore.Time;
using Newtonsoft.Json;
using static Miningcore.Util.ActionUtils;

namespace Miningcore.Blockchain.Aleo;

[CoinFamily(CoinFamily.Aleo)]
public class AleoPool : PoolBase
{
    public AleoPool(IComponentContext ctx,
        JsonSerializerSettings serializerSettings,
        IConnectionFactory cf,
        IStatsRepository statsRepo,
        IShareRepository shareRepo,
        IMapper mapper,
        IMasterClock clock,
        IMessageBus messageBus,
        RecyclableMemoryStreamManager rmsm,
        NicehashService nicehashService) :
        base(ctx, serializerSettings, cf, statsRepo, mapper, clock, messageBus, rmsm, nicehashService)
    {
        this.shareRepository = shareRepo;
    }

    private readonly IShareRepository shareRepository;
    private AleoJobManager manager;
    protected object currentJobParams;
    private AleoCoinTemplate coin;

    protected virtual async Task OnSubscribeAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<AleoWorkerContext>();
        
        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        var requestParams = request.ParamsAs<string[]>();
        context.UserAgent = requestParams.FirstOrDefault()?.Trim();

        // setup worker context
        context.IsSubscribed = true;

        // send response
        await connection.RespondAsync(context.IsSubscribed, request.Id);

        // send difficulty
        await connection.NotifyAsync(AleoStratumMethods.SetDifficulty, new object[] { context.Difficulty });

        // send job
        var job = CreateWorkerJob(connection);
        await connection.NotifyAsync(AleoStratumMethods.MiningNotify, job);
    }

    protected virtual async Task OnAuthorizeAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<AleoWorkerContext>();

        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        var requestParams = request.ParamsAs<string[]>();
        var workerValue = requestParams?.Length > 0 ? requestParams[0] : null;
        var password = requestParams?.Length > 1 ? requestParams[1] : null;
        var passParts = password?.Split(PasswordControlVarsSeparator);

        // extract worker/miner
        var split = workerValue?.Split('.');
        var minerName = split?.FirstOrDefault()?.Trim();
        var workerName = split?.Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;

        // validate & authenticate
        var (IsAuthorized, Address, StaticDiff, MaxDiff) = manager.ValidateLogin(minerName, password);
        context.IsAuthorized = await manager.ValidateAddressAsync(Address, ct);
        context.Miner = minerName;
        context.Worker = workerName;

        if(context.IsAuthorized)
        {
            // respond success
            await connection.RespondAsync(true, request.Id);

            // log association
            logger.Info(() => $"[{connection.ConnectionId}] Authorized worker {workerValue}");

            // extract control vars from password
            var staticDiff = GetStaticDiffFromPassparts(passParts);

            // Static diff
            if(staticDiff.HasValue &&
               (context.VarDiff != null && staticDiff.Value >= context.VarDiff.Config.MinDiff ||
                   context.VarDiff == null && staticDiff.Value > context.Difficulty))
            {
                context.VarDiff = null; // disable vardiff
                context.SetDifficulty(staticDiff.Value);

                logger.Info(() => $"[{connection.ConnectionId}] Setting static difficulty of {staticDiff.Value}");

                await connection.NotifyAsync(AleoStratumMethods.SetDifficulty, new object[] { context.Difficulty });
            }

            // send initial job
            var job = CreateWorkerJob(connection);
            await connection.NotifyAsync(AleoStratumMethods.MiningNotify, job);
        }

        else
        {
            await connection.RespondErrorAsync(StratumError.UnauthorizedWorker, "Authorization failed", request.Id, context.IsAuthorized);

            if(clusterConfig?.Banning?.BanOnLoginFailure is null or true)
            {
                // issue short-time ban if unauthorized to prevent DDos
                logger.Info(() => $"[{connection.ConnectionId}] Banning unauthorized worker {minerName} for {loginFailureBanTimeout.TotalSeconds} sec");

                banManager.Ban(connection.RemoteEndpoint.Address, loginFailureBanTimeout);

                Disconnect(connection);
            }
        }
    }

    private async Task OnSubmitAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<AleoWorkerContext>();

        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        if(!context.IsAuthorized)
            throw new StratumException(StratumError.UnauthorizedWorker, "unauthorized worker");
        
        if(!context.IsSubscribed)
            throw new StratumException(StratumError.NotSubscribed, "not subscribed");

        var requestParams = request.ParamsAs<string[]>();
        var workerName = requestParams[0];
        var jobId = requestParams[1];
        var counter = requestParams[2];

        AleoJob job;

        try
        {
            job = manager.ValidateJob(connection, jobId);
        }
        catch(StratumException ex)
        {
            await connection.RespondErrorAsync(ex.Code, ex.Message, request.Id, false);
            return;
        }

        try
        {
            var share = await manager.SubmitShareAsync(connection, job, counter, ct);
            
            // record it
            await RecordShareAsync(connection, share, ct);
            
            // respond
            await connection.RespondAsync(true, request.Id);
        }
        catch(StratumException ex)
        {
            await connection.RespondErrorAsync(ex.Code, ex.Message, request.Id, false);
            return; 
        }
    }

    private object[] CreateWorkerJob(StratumConnection connection)
    {
        var context = connection.ContextAs<AleoWorkerContext>();
        var job = manager.GetJobForStratum();

        return job.GetJobParams();
    }

    protected override async Task OnRequestAsync(StratumConnection connection,
        Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<AleoWorkerContext>();

        try
        {
            switch(request.Method)
            {
                case AleoStratumMethods.Subscribe:
                    await OnSubscribeAsync(connection, tsRequest);
                    break;

                case AleoStratumMethods.Authorize:
                    await OnAuthorizeAsync(connection, tsRequest, ct);
                    break;

                case AleoStratumMethods.SubmitShare:
                    await OnSubmitAsync(connection, tsRequest, ct);
                    break;

                default:
                    logger.Debug(() => $"[{connection.ConnectionId}] Unsupported RPC request: {JsonConvert.SerializeObject(request, serializerSettings)}");

                    await connection.RespondErrorAsync(StratumError.Other, $"Unsupported request {request.Method}", request.Id);
                    break;
            }
        }

        catch(StratumException ex)
        {
            await connection.RespondErrorAsync(ex.Code, ex.Message, request.Id, false);
        }
    }

    protected override async Task SetupJobManager(CancellationToken ct)
    {
        manager = ctx.Resolve<AleoJobManager>();
        manager.Configure(poolConfig, clusterConfig);

        await manager.StartAsync(ct);

        if(poolConfig.EnableInternalStratum == true)
        {
            disposables.Add(manager.Jobs
                .Select(job => Observable.FromAsync(() =>
                    Guard(()=> OnNewJobAsync((AleoJob)job),
                        ex=> logger.Debug(() => $"{nameof(OnNewJobAsync)}: {ex.Message}"))))
                .Concat()
                .Subscribe(_ => { }, ex =>
                {
                    logger.Debug(ex, nameof(OnNewJobAsync));
                }));

            // initial update
            await manager.Jobs.Take(1).ToTask(ct);
        }

        else
        {
            // keep updating NetworkStats
            disposables.Add(manager.Jobs.Subscribe());
        }
    }

    protected virtual async Task OnNewJobAsync(AleoJob job)
    {
        logger.Info(() => $"New job detected [{job.Id}]");

        currentJobParams = job.GetJobParams();

        // notify stratum clients
        await Guard(() => ForEachMinerAsync(async (connection, ct) =>
        {
            if(!connection.IsAlive)
                return;

            var context = connection.ContextAs<AleoWorkerContext>();

            if(!context.IsSubscribed || !context.IsAuthorized)
                return;

            await connection.NotifyAsync(AleoStratumMethods.MiningNotify, currentJobParams);
        }));
    }

    private async Task RecordShareAsync(StratumConnection connection, Share share, CancellationToken ct)
    {
        share.PoolId = poolConfig.Id;
        share.Source = clusterConfig.ClusterName;
        share.Created = clock.Now;

        // record it
        //await shareRepository.InsertAsync(share, ct);

        // broadcast to network
        messageBus.SendMessage(share);
    }

    #region API-Surface

    public override void Configure(PoolConfig poolConfig, ClusterConfig clusterConfig)
    {
        coin = poolConfig.Template.As<AleoCoinTemplate>();
        base.Configure(poolConfig, clusterConfig);
    }

    public override double HashrateFromShares(double shares, double interval)
    {
        if(interval <= 0)
            return 0;

        // アルゴ固有の補正係数
        const double multiplier = 1.0;
        
        // ハッシュレートの計算
        // shares: シェア数
        // interval: 計測期間（秒）
        var hashrate = shares * multiplier / interval;

        // 結果を返す前に負の値をチェック
        return Math.Max(0, hashrate);
    }

    public override double ShareMultiplier => 1;

    protected override WorkerContextBase CreateWorkerContext()
    {
        return new AleoWorkerContext();
    }

    #endregion // API-Surface
}
