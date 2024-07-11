using System;
using System.Threading.Tasks;
using System.Reactive;
using Autofac;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Koto.Configuration;
using Miningcore.Persistence.Repositories;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Notifications.Messages;
using Miningcore.Configuration;
using Miningcore.Mining;
using Miningcore.Notifications;
using Miningcore.Persistence;
using Miningcore.Stratum;
using Miningcore.Nicehash;
using Miningcore.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.IO;
using NLog;
using AutoMapper;

namespace Miningcore.Blockchain.Koto
{
    [CoinFamily(CoinFamily.Koto)]
    public class KotoPool : PoolBase
    {
        private KotoJobManager manager;
        private KotoExtraNonceProvider extraNonceProvider;
        private KotoCoinTemplate coin;
        private readonly IMessageBus messageBus;
        private object currentJobParams;
        private double hashrateDivisor;
        public KotoPool(IComponentContext ctx, JsonSerializerSettings serializerSettings, IConnectionFactory cf, IStatsRepository statsRepo, IMapper mapper, IMasterClock clock, IMessageBus messageBus, RecyclableMemoryStreamManager rmsm, NicehashService nicehashService) 
            : base(ctx, serializerSettings, cf, statsRepo, mapper, clock, messageBus, rmsm, nicehashService)
        {
             this.messageBus = messageBus;
        }

        protected override async Task SetupJobManager(CancellationToken ct)
        {
            coin = poolConfig.Template.As<KotoCoinTemplate>();
            manager = ctx.Resolve<KotoJobManager>(new TypedParameter(typeof(IExtraNonceProvider), new KotoExtraNonceProvider(poolConfig.Id, clusterConfig.InstanceId)));;
            extraNonceProvider = ctx.Resolve<KotoExtraNonceProvider>();
            manager.Configure(poolConfig, clusterConfig);
            await manager.StartAsync(ct);
        }

        protected override async Task OnRequestAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
        {
            var request = tsRequest;
            var context = connection.ContextAs<BitcoinWorkerContext>();
            var requestId = request.Value.Id;
            var method = request.Value.Method;

            switch (method)
            {
                case BitcoinStratumMethods.Subscribe:
                    await OnSubscribeAsync(connection, tsRequest);
                    break;

                case BitcoinStratumMethods.Authorize:
                    await OnAuthorizeAsync(connection, tsRequest, ct);
                    break;

                case BitcoinStratumMethods.SubmitShare:
                    await OnSubmitAsync(connection, tsRequest, ct);
                    break;

                case BitcoinStratumMethods.GetTransactions:
                    await OnGetTransactionsAsync(ct, connection, requestId, request);
                    break;

        //        case BitcoinStratumMethods.GetJob:
        //            await OnGetJobAsync(ct, connection, requestId, request.Value.Params);
        //            break;

                default:
                    logger.Warn(() => $"[{LogCategory}] Unsupported RPC request: {method}");
                    break;
            }
        }

    public override double HashrateFromShares(double shares, double interval)
    {
        var multiplier = BitcoinConstants.Pow2x32;
        var result = shares * multiplier / interval;
        return result;
    }


    protected virtual async Task OnSubscribeAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest)
    {
        var request = tsRequest.Value;

        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        var context = connection.ContextAs<KotoWorkerContext>();
        var requestParams = request.ParamsAs<string[]>();

        var data = new object[]
        {
            new object[]
            {
                new object[] { BitcoinStratumMethods.SetDifficulty, connection.ConnectionId },
                new object[] { BitcoinStratumMethods.MiningNotify, connection.ConnectionId }
            }
        }
        .Concat(manager.GetSubscriberData(connection))
        .ToArray();

        await connection.RespondAsync(data, request.Id);

        // setup worker context
        context.IsSubscribed = true;
        context.UserAgent = requestParams.FirstOrDefault()?.Trim();

        // Nicehash support
        var nicehashDiff = await GetNicehashStaticMinDiff(context, coin.Name, coin.GetAlgorithmName());

        if(nicehashDiff.HasValue)
        {
            logger.Info(() => $"[{connection.ConnectionId}] Nicehash detected. Using API supplied difficulty of {nicehashDiff.Value}");

            context.VarDiff = null; // disable vardiff
            context.SetDifficulty(nicehashDiff.Value);
        }

        // send intial update
        await connection.NotifyAsync(BitcoinStratumMethods.SetDifficulty, new object[] { context.Difficulty });
        await connection.NotifyAsync(BitcoinStratumMethods.MiningNotify, currentJobParams);
    }

    protected virtual async Task OnAuthorizeAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;

        if(request.Id == null)
            throw new StratumException(StratumError.MinusOne, "missing request id");

        var context = connection.ContextAs<BitcoinWorkerContext>();
        var requestParams = request.ParamsAs<string[]>();
        var workerValue = requestParams?.Length > 0 ? requestParams[0] : null;
        var password = requestParams?.Length > 1 ? requestParams[1] : null;
        var passParts = password?.Split(PasswordControlVarsSeparator);

        // extract worker/miner
        var split = workerValue?.Split('.');
        var minerName = split?.FirstOrDefault()?.Trim();
        var workerName = split?.Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;

        // assumes that minerName is an address
        context.IsAuthorized = await manager.ValidateAddressAsync(minerName, ct);
        context.Miner = minerName;
        context.Worker = workerName;

        if(context.IsAuthorized)
        {
            // respond
            await connection.RespondAsync(context.IsAuthorized, request.Id);

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

                await connection.NotifyAsync(BitcoinStratumMethods.SetDifficulty, new object[] { context.Difficulty });
            }
        }

        else
        {
            await connection.RespondErrorAsync(StratumError.UnauthorizedWorker, "Authorization failed", request.Id, context.IsAuthorized);

            if(clusterConfig?.Banning?.BanOnLoginFailure is null or true)
            {
                // issue short-time ban if unauthorized to prevent DDos on daemon (validateaddress RPC)
                logger.Info(() => $"[{connection.ConnectionId}] Banning unauthorized worker {minerName} for {loginFailureBanTimeout.TotalSeconds} sec");

                banManager.Ban(connection.RemoteEndpoint.Address, loginFailureBanTimeout);

                Disconnect(connection);
            }
        }
    }

    protected virtual async Task OnSubmitAsync(StratumConnection connection, Timestamped<JsonRpcRequest> tsRequest, CancellationToken ct)
    {
        var request = tsRequest.Value;
        var context = connection.ContextAs<BitcoinWorkerContext>();

        try
        {
            if(request.Id == null)
                throw new StratumException(StratumError.MinusOne, "missing request id");

            // check age of submission (aged submissions are usually caused by high server load)
            var requestAge = clock.Now - tsRequest.Timestamp.UtcDateTime;

            if(requestAge > maxShareAge)
            {
                logger.Warn(() => $"[{connection.ConnectionId}] Dropping stale share submission request (server overloaded?)");
                return;
            }

            // check worker state
            context.LastActivity = clock.Now;

            // validate worker
            if(!context.IsAuthorized)
                throw new StratumException(StratumError.UnauthorizedWorker, "unauthorized worker");
            else if(!context.IsSubscribed)
                throw new StratumException(StratumError.NotSubscribed, "not subscribed");

            var requestParams = request.ParamsAs<string[]>();

            // submit
            var share = await manager.SubmitShareAsync(connection, requestParams, ct);
            
            // Nicehash's stupid validator insists on "error" property present
            // in successful responses which is a violation of the JSON-RPC spec
            // [We miss you Oliver <3 We miss you so much <3 Respect the goddamn standards Nicehash :(]
            var response = new JsonRpcResponse<object>(true, request.Id);

            if(context.IsNicehash)
            {
                response.Extra = new Dictionary<string, object>();
                response.Extra["error"] = null;
            }
            
            await connection.RespondAsync(response);

            // publish
            messageBus.SendMessage(share);

            // telemetry
            PublishTelemetry(TelemetryCategory.Share, clock.Now - tsRequest.Timestamp.UtcDateTime, true);

            logger.Info(() => $"[{connection.ConnectionId}] Share accepted: D={Math.Round(share.Difficulty, 3)}");

            // update pool stats
            if(share.IsBlockCandidate)
                poolStats.LastPoolBlockTime = clock.Now;

            // update client stats
            context.Stats.ValidShares++;

            await UpdateVarDiffAsync(connection, false, ct);
        }

        catch(StratumException ex)
        {
            // telemetry
            PublishTelemetry(TelemetryCategory.Share, clock.Now - tsRequest.Timestamp.UtcDateTime, false);

            // update client stats
            context.Stats.InvalidShares++;
            logger.Info(() => $"[{connection.ConnectionId}] Share rejected: {ex.Message} [{context.UserAgent}]");

            // banning
            ConsiderBan(connection, context, poolConfig.Banning);

            throw;
        }
    }

        private async Task OnGetTransactionsAsync(CancellationToken ct, StratumConnection client, object requestId, Timestamped<JsonRpcRequest> tsRequest)
        {
            var context = client.ContextAs<BitcoinWorkerContext>();

            var transactions = manager.GetTransactionsForStratum();
            await client.RespondAsync(transactions, requestId);
        }

        private async Task OnGetJobAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var context = client.ContextAs<KotoWorkerContext>();
            var jobParams = manager.gjpfs(context.IsAuthorized);

            await client.RespondAsync(jobParams, requestId);
        }

        private async Task OnMiningSubscribeAsync(CancellationToken ct, StratumConnection client, object requestId, JToken[] parameters)
        {
            var context = client.ContextAs<KotoWorkerContext>();

            var extraNonce1 = extraNonceProvider.Next();
            context.ExtraNonce1 = extraNonce1;
            context.ExtraNonce2Size = extraNonceProvider.Size;

            var response = new object[]
            {
                new object[] { BitcoinStratumMethods.MiningNotify, extraNonce1, context.ExtraNonce2Size }
            };

            await client.RespondAsync(response, requestId);
        }
        protected override WorkerContextBase CreateWorkerContext()
        {
            return new KotoWorkerContext();
        }
        protected string LogCategory => "Koto Pool";
    }
}
