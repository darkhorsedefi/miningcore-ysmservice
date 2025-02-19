using System;
using System.Reactive;
using Autofac;
using Microsoft.Extensions.Logging;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Notifications.Messages;
using Miningcore.Mining;
using Miningcore.Notifications;
using Miningcore.Stratum;
using Miningcore.Time;
using Newtonsoft.Json;
using System.Diagnostics.Contracts;
using System.Reactive.Linq;
using Miningcore.Blockchain.Aleo.Configuration;
using System.Reactive.Subjects;

namespace Miningcore.Blockchain.Aleo;

public class AleoJobManager : JobManagerBase<AleoJob>
{
    public AleoJobManager(
        IComponentContext ctx,
        IMasterClock clock,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory) : base(ctx, messageBus)
    {
        this.clock = clock;
        this.loggerFactory = loggerFactory;
        this.httpClientFactory = httpClientFactory;
        this.seenNonces = new HashSet<ulong>();
        this.globalTargetModifier = 1.0;
        this.speedometer = new Dictionary<string, Dictionary<string, double>>();
    }

    private readonly IMasterClock clock;
    private readonly ILoggerFactory loggerFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private AleoClient rpcClient;
    private readonly HashSet<ulong> seenNonces;
    private double globalTargetModifier;
    private Dictionary<string, Dictionary<string, double>> speedometer;

    protected override async Task<bool> AreDaemonsHealthyAsync(CancellationToken ct)
    {
        try
        {
            var response = await rpcClient.GetBlockTemplateAsync();
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    protected override async Task<bool> AreDaemonsConnectedAsync(CancellationToken ct)
    {
        try
        {
            var response = await rpcClient.GetBlockTemplateAsync();
            return response != null;
        }
        catch
        {
            return false;
        }
    }

    protected override Task EnsureDaemonsSynchedAsync(CancellationToken ct)
    {
        // Aleoではこの実装は不要
        return Task.CompletedTask;
    }

    protected override async Task PostStartInitAsync(CancellationToken ct)
    {
        SetupJobUpdates(ct);

        
    }

    protected virtual void SetupJobUpdates(CancellationToken ct)
    {
        var blockFound = blockFoundSubject.Synchronize();
        var pollTimerRestart = blockFoundSubject.Synchronize();

        var triggers = new List<IObservable<(bool Force, string Via, string Data)>>
        {
            blockFound.Select(_ => (false, "BlockFound", (string)null))
        };

        if(poolConfig.BlockRefreshInterval > 0)
        {
            // periodically update block-template
            var pollingInterval = poolConfig.BlockRefreshInterval > 0 ? poolConfig.BlockRefreshInterval : 1000;

            triggers.Add(Observable.Timer(TimeSpan.FromMilliseconds(pollingInterval))
                .TakeUntil(pollTimerRestart)
                .Select(_ => (false, "Poll", (string)null))
                .Repeat());
        }
        else
        {
            // get initial blocktemplate
            triggers.Add(Observable.Interval(TimeSpan.FromMilliseconds(1000))
                .Select(_ => (false, "Initial", (string)null))
                .TakeWhile(_ => !hasInitialBlockTemplate));
        }

        // periodically update transactions for current template
        if(poolConfig.JobRebroadcastTimeout > 0)
        {
            triggers.Add(Observable.Timer(TimeSpan.FromSeconds(poolConfig.JobRebroadcastTimeout))
                .TakeUntil(pollTimerRestart)
                .Select(_ => (true, "PollRefresh", (string)null))
                .Repeat());
        }

        Jobs = triggers.Merge()
            .Select(x => Observable.FromAsync(() => UpdateJob(x.Force)))
            .Concat()
            .Where(x => x.IsNew || x.Force)
            .Do(x =>
            {
                if(x.IsNew)
                    hasInitialBlockTemplate = true;
            })
            .Select(x => GetJobForStratum())
            .Publish()
            .RefCount();
    }

    protected override void ConfigureDaemons()
    {
        // デーモンの設定はrpcClientの初期化時に行われます
    }

    public override AleoJob GetJobForStratum()
    {
        return currentJob;
    }

    private new AleoJob currentJob;
    private readonly Subject<Unit> blockFoundSubject = new();
    private bool hasInitialBlockTemplate = false;

    protected async Task<(bool IsNew, bool Force)> UpdateJob(bool forceUpdate)
    {
        try
        {
            var blockTemplate = await rpcClient.GetBlockTemplateAsync();
            var newJob = new AleoJob(blockTemplate.JobId, blockTemplate, clock);

            var isNew = currentJob == null || newJob.BlockTemplate.Height != currentJob.BlockTemplate.Height;

            if(isNew)
            {
                currentJob = newJob;
                blockFoundSubject.OnNext(Unit.Default);
                await OnNewJobAsync(newJob, true);
            }

            return (isNew, forceUpdate);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to update job: {0}", ex.Message);
            return (false, forceUpdate);
        }
    }

    public override void Configure(PoolConfig poolConfig, ClusterConfig clusterConfig)
    {
        base.Configure(poolConfig, clusterConfig);

        // RPC クライアントの設定
        rpcClient = new AleoClient(poolConfig.Daemons[0], messageBus, loggerFactory, httpClientFactory);

        // AleoNetworkVersionの設定
        var coin = poolConfig.Template as AleoCoinTemplate;
        if (!string.IsNullOrEmpty(coin?.AleoNetworkVersion))
        {
            rpcClient.SetNetworkVersion(coin.AleoNetworkVersion);
        }

        // ノンスクリーンアップタイマーの設定
        StartNonceCleanupTimer();

        SetupJobUpdates(CancellationToken.None);

        logger.Info(() => $"Configured Aleo daemon(s) [{string.Join(", ", poolConfig.Daemons.Select(x => x.Host))}]");
    }

    private void StartNonceCleanupTimer()
    {
        var timer = new System.Timers.Timer();
        timer.Interval = 60000; // 1分ごとにクリーンアップ
        timer.Elapsed += (s, e) =>
        {
            seenNonces.Clear();
            logger.Debug(() => $"Cleared seen nonces: {seenNonces.Count}");
        };
        timer.Start();

        // スピードメーター更新タイマー
        var speedTimer = new System.Timers.Timer();
        speedTimer.Interval = 5000; // 5秒ごとに更新
        speedTimer.Elapsed += (s, e) =>
        {
            var now = clock.Now.ToUnixTimestamp();
            var expiry = now - 3600; // 1時間以上前のデータを削除

            foreach(var miner in speedometer.Keys.ToList())
            {
                var stats = speedometer[miner];
                if(stats.GetValueOrDefault("last", 0) < expiry)
                {
                    speedometer.Remove(miner);
                    continue;
                }

                // スピードの減衰
                var speed = stats.GetValueOrDefault("speed", 0);
                stats["speed"] = speed * 0.95; // 5%減衰
            }

            // グローバルターゲット修飾子の更新
            var totalSpeed = speedometer.Values.Sum(x => x.GetValueOrDefault("speed", 0));
            globalTargetModifier = Math.Max(1.0, totalSpeed / 200.0);
        };
        speedTimer.Start();
    }

    public async Task<bool> ValidateAddressAsync(string address, CancellationToken ct)
    {
        Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(address), $"{nameof(address)} must not be empty");

        try
        {
            return await rpcClient.ValidateAddressAsync(address);
        }
        catch(Exception ex)
        {
            logger.Error("Error validating address: {0}", ex.Message);
            return false;
        }
    }

public IObservable<object> Jobs { get; private set; }


    private async Task OnNewJobAsync(AleoJob job, bool clean)
    {
        currentJob = job;
        logger.Info(() => $"New job detected [jobId: {job.Id}]");
    }


    public AleoJob ValidateJob(StratumConnection connection, string jobId)
    {
        var context = connection.ContextAs<AleoWorkerContext>();
        var job = context.GetJob(jobId);

        if(job == null)
            throw new StratumException(StratumError.JobNotFound, "job not found");

        return job;
    }

    public async Task<Share> SubmitShareAsync(StratumConnection connection, AleoJob job, string counter, CancellationToken ct)
    {
        var context = connection.ContextAs<AleoWorkerContext>();
        
        // validate counter
        if(!ulong.TryParse(counter, out var nonce))
            throw new StratumException(StratumError.Other, "invalid counter");

        // check for duplicate nonce
        if(seenNonces.Contains(nonce))
            throw new StratumException(StratumError.DuplicateShare, "duplicate nonce");

        // validate proof target
        var proofTarget = await rpcClient.GetProofTargetAsync(job.BlockTemplate.EpochHash, poolConfig.Address, nonce);
        
        // calculate adjusted target based on global modifier
        var adjustedTarget = (ulong)(job.BlockTemplate.ProofTarget * globalTargetModifier);
        
        if(proofTarget > adjustedTarget)
            throw new StratumException(StratumError.LowDifficultyShare, "low difficulty share");

        // validate partial solution
        try
        {
            // check if partial solution is valid
            var isValid = await rpcClient.ValidatePartialSolutionAsync(
                job.BlockTemplate.EpochHash,
                poolConfig.Address,
                nonce,
                ct);

            if(!isValid)
                throw new StratumException(StratumError.Other, "invalid partial solution");
        }
        catch(Exception ex)
        {
            throw new StratumException(StratumError.Other, $"partial solution validation failed: {ex.Message}");
        }

        // add nonce to seen set
        seenNonces.Add(nonce);

        // update speedometer
        var minerKey = context.Miner;
        if(!speedometer.ContainsKey(minerKey))
            speedometer[minerKey] = new Dictionary<string, double>();

        var now = clock.Now;
        speedometer[minerKey]["last"] = now.ToUnixTimestamp();
        
        var speed = speedometer[minerKey].GetValueOrDefault("speed", 0);
        speedometer[minerKey]["speed"] = speed + 1;

        // adjust global target modifier based on pool hashrate
        var totalSpeed = speedometer.Values.Sum(x => x.GetValueOrDefault("speed", 0));
        globalTargetModifier = Math.Max(1.0, totalSpeed / 200.0);

        // create share
        var share = new Share
        {
            PoolId = poolConfig.Id,
            BlockHeight = (long) job.BlockTemplate.Height,
            NetworkDifficulty = job.BlockTemplate.ProofTarget,
            Difficulty = (double)adjustedTarget,
            Miner = context.Miner,
            Worker = context.Worker,
            UserAgent = context.UserAgent,
            IpAddress = connection.RemoteEndpoint.Address.ToString(),
            Source = clusterConfig.ClusterName,
            Created = now
        };

        return share;
    }

    private double? GetStaticDiffFromPassword(string password)
    {
        if(string.IsNullOrEmpty(password))
            return null;

        var parts = password.Split(';');
        foreach(var part in parts)
        {
            if(part.Contains("d="))
            {
                var diffStr = part.Split('=')[1];
                if(double.TryParse(diffStr, out var diff))
                    return diff;
            }
        }

        return null;
    }
}
