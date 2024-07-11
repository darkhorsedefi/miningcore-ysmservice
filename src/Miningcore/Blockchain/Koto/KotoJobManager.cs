using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Miningcore.Blockchain.Koto.Configuration;
using Miningcore.Blockchain.Koto.DaemonResponses;
using Miningcore.Blockchain.Bitcoin.DaemonResponses;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Configuration;
using Miningcore.Contracts;
using Miningcore.Crypto;
using Miningcore.Mining;
using Miningcore.Rpc;
using Miningcore.Stratum;
using Miningcore.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NBitcoin;
using NLog;

namespace Miningcore.Blockchain.Koto
{
    public class KotoJobManager : BitcoinJobManagerBase<KotoJob>
    {
        private KotoExtraNonceProvider extraNonceProvider;
        private KotoDaemonClient daemonClient;
        private Network network;
        private RpcClient rpc;

        public KotoJobManager(IComponentContext ctx, IMasterClock clock, IExtraNonceProvider extraNonceProvider, ILogger logger) 
            : base(ctx, clock, extraNonceProvider, logger)
        {
            this.extraNonceProvider = extraNonceProvider as KotoExtraNonceProvider;
            ConfigureDaemons();
        }

        protected override void ConfigureDaemons()
        {
            var jsonSerializerSettings = ctx.Resolve<JsonSerializerSettings>();
            var rpcClient = new RpcClient(poolConfig.Daemons.First(), jsonSerializerSettings, messageBus, poolConfig.Id);
            daemonClient = new KotoDaemonClient(rpcClient, logger);
            rpc = rpcClient;
        }

        protected async Task<(bool IsNew, string Blob)> BuildJobAsync(CancellationToken ct)
        {
            var response = await daemonClient.GetBlockTemplateAsync();

            if (response.Error != null)
            {
                logger.Warn(() => $"Unable to get block template: {response.Error.Message} (Code: {response.Error.Code})");
                return (false, null);
            }

            var blockTemplate = response.Result;
            var jobId = NextJobId();
            var job = new KotoJob(jobId, blockTemplate, poolConfig);

            lock (jobLock)
            {
                validJobs.Insert(0, job);

                if (validJobs.Count > maxActiveJobs)
                    validJobs.RemoveAt(validJobs.Count - 1);
            }

            logger.Info(() => $"[BuildJob] New job {jobId} for block {blockTemplate.Height}");
            return (true, jobId);
        }

        public override void Configure(PoolConfig pc, ClusterConfig cc)
        {
            coin = pc.Template.As<KotoCoinTemplate>();
            base.Configure(pc, cc);
        }

        protected override object GetJobParamsForStratum(bool isNew)
        {
            var job = currentJob;
            if (job == null)
                return null;

            return new object[]
            {
                job.Id,
                job.PreviousBlockHash,
                job.CoinbaseTransaction,
                job.Transactions,
                job.MerkleRoot,
                job.Bits,
                job.Time,
                job.Nonce,
                isNew
            };
        }

        protected async Task<DaemonResponse<KotoBlockTemplate>> GetBlockTemplateAsync(CancellationToken ct)
        {
            var response = await daemonClient.GetBlockTemplateAsync();

            if (response.Error != null)
            {
                logger.Warn(() => $"Unable to get block: {response.Error.Message} (Code: {response.Error.Code})");
                return null;
            }

            return response;
        }

        public void PrepareWorker(StratumConnection worker)
        {
            var context = worker.ContextAs<KotoWorkerContext>();

            context.ExtraNonce1 = extraNonceProvider.Next();
            context.ExtraNonce2Size = extraNonceProvider.Size;
        }

        public string[] GetTransactionsForStratum()
        {
            return currentJob.Transactions;
        }
        protected override async Task<bool> AreDaemonsConnectedAsync(CancellationToken ct)
        {
            if(hasLegacyDaemon)
                return await AreDaemonsConnectedLegacyAsync(ct);

            var response = await rpc.ExecuteAsync<NetworkInfo>(logger, BitcoinCommands.GetNetworkInfo, ct);

            // update stats
            if(!string.IsNullOrEmpty(response.Response.Version))
                BlockchainStats.NodeVersion = (string) response.Response?.Version;

            return response.Error == null && response.Response?.Connections > 0;
        }
        protected override async Task<bool> AreDaemonsHealthyAsync(CancellationToken ct)
        {
            if(hasLegacyDaemon)
                return await AreDaemonsHealthyLegacyAsync(ct);

            var response = await rpc.ExecuteAsync<BlockchainInfo>(logger, BitcoinCommands.GetBlockchainInfo, ct);

            if(response.Error != null)
            {
                logger.Error(() => $"Daemon reports: {response.Error.Message}");
                return false;
            }
            return true;
        }
        protected override async Task EnsureDaemonsSynchedAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            var syncPendingNotificationShown = false;

            do
            {
                var response = await rpc.ExecuteAsync<BlockTemplate>(logger,
                    BitcoinCommands.GetBlockTemplate, ct, GetBlockTemplateParams());

                var isSynched = response.Error == null;

                if(isSynched)
                {
                    logger.Info(() => "All daemons synched with blockchain");
                    break;
                }
                else
                {
                    logger.Debug(() => $"Daemon reports error: {response.Error?.Message}");
                }

                if(!syncPendingNotificationShown)
                {
                    logger.Info(() => "Daemon is still syncing with network. Manager will be started once synced.");
                    syncPendingNotificationShown = true;
                }

                await ShowDaemonSyncProgressAsync(ct);
            } while(await timer.WaitForNextTickAsync(ct));
        }
        protected virtual async Task ShowDaemonSyncProgressAsync(CancellationToken ct)
        {
            if(hasLegacyDaemon)
            {
                await ShowDaemonSyncProgressLegacyAsync(ct);
                return;
            }

            var info = await rpc.ExecuteAsync<BlockchainInfo>(logger, BitcoinCommands.GetBlockchainInfo, ct);

            if(info != null)
            {
                var blockCount = info.Response?.Blocks;

                if(blockCount.HasValue)
                {
                    // get list of peers and their highest block height to compare to ours
                    var peerInfo = await rpc.ExecuteAsync<PeerInfo[]>(logger, BitcoinCommands.GetPeerInfo, ct);
                    var peers = peerInfo.Response;

                    var totalBlocks = Math.Max(info.Response.Headers, peers.Any() ? peers.Max(y => y.StartingHeight) : 0);

                    var percent = totalBlocks > 0 ? (double) blockCount / totalBlocks * 100 : 0;
                    logger.Info(() => $"Daemon has downloaded {percent:0.00}% of blockchain from {peers.Length} peers");
                }
            }
        }
        protected async Task ShowDaemonSyncProgressLegacyAsync(CancellationToken ct)
        {
            var info = await rpc.ExecuteAsync<DaemonInfo>(logger, BitcoinCommands.GetInfo, ct);

            if(info != null)
            {
                var blockCount = info.Response?.Blocks;

                if(blockCount.HasValue)
                {
                    // get list of peers and their highest block height to compare to ours
                    var peerInfo = await rpc.ExecuteAsync<PeerInfo[]>(logger, BitcoinCommands.GetPeerInfo, ct);
                    var peers = peerInfo.Response;

                    if(peers != null && peers.Length > 0)
                    {
                        var totalBlocks = peers.Max(x => x.StartingHeight);
                        var percent = totalBlocks > 0 ? (double) blockCount / totalBlocks * 100 : 0;
                        logger.Info(() => $"Daemon has downloaded {percent:0.00}% of blockchain from {peers.Length} peers");
                    }
                }
            }
        }
    protected record SubmitResult(bool Accepted, string CoinbaseTx);

    protected async Task<SubmitResult> SubmitBlockAsync(Share share, string blockHex, CancellationToken ct)
    {
        var submitBlockRequest = hasSubmitBlockMethod
            ? new RpcRequest(BitcoinCommands.SubmitBlock, new[] { blockHex })
            : new RpcRequest(BitcoinCommands.GetBlockTemplate, new { mode = "submit", data = blockHex });

        var batch = new []
        {
            submitBlockRequest,
            new RpcRequest(BitcoinCommands.GetBlock, new[] { share.BlockHash })
        };

        var results = await rpc.ExecuteBatchAsync(logger, ct, batch);

        // did submission succeed?
        var submitResult = results[0];
        var submitError = submitResult.Error?.Message ??
            submitResult.Error?.Code.ToString(CultureInfo.InvariantCulture) ??
            submitResult.Response?.ToString();

        if(!string.IsNullOrEmpty(submitError))
        {
            logger.Warn(() => $"Block {share.BlockHeight} submission failed with: {submitError}");
            messageBus.SendMessage(new AdminNotification("Block submission failed", $"Pool {poolConfig.Id} {(!string.IsNullOrEmpty(share.Source) ? $"[{share.Source.ToUpper()}] " : string.Empty)}failed to submit block {share.BlockHeight}: {submitError}"));
            return new SubmitResult(false, null);
        }

        // was it accepted?
        var acceptResult = results[1];
        var block = acceptResult.Response?.ToObject<DaemonResponses.Block>();
        var accepted = acceptResult.Error == null && block?.Hash == share.BlockHash;

        if(!accepted)
        {
            logger.Warn(() => $"Block {share.BlockHeight} submission failed for pool {poolConfig.Id} because block was not found after submission");
            messageBus.SendMessage(new AdminNotification($"[{poolConfig.Id}]-[{(!string.IsNullOrEmpty(share.Source) ? $"[{share.Source.ToUpper()}] " : string.Empty)}] Block submission failed", $"[{poolConfig.Id}]-[{(!string.IsNullOrEmpty(share.Source) ? $"[{share.Source.ToUpper()}] " : string.Empty)}] Block {share.BlockHeight} submission failed for pool {poolConfig.Id} because block was not found after submission"));
        }

        return new SubmitResult(accepted, block?.Transactions.FirstOrDefault());
    }
        protected override async Task PostStartInitAsync(CancellationToken ct)
    {
        var requests = new[]
        {
            new RpcRequest(BitcoinCommands.ValidateAddress, new[] { poolConfig.Address }),
            new RpcRequest(BitcoinCommands.SubmitBlock),
            new RpcRequest(!hasLegacyDaemon ? BitcoinCommands.GetBlockchainInfo : BitcoinCommands.GetInfo),
            new RpcRequest(BitcoinCommands.GetDifficulty),
            new RpcRequest(BitcoinCommands.GetAddressInfo, new[] { poolConfig.Address }),
        };

        var responses = await rpc.ExecuteBatchAsync(logger, ct, requests);

        if(responses.Any(x => x.Error != null))
        {
            // filter out optional RPCs
            var errors = responses
                .Where((x, i) => x.Error != null &&
                    requests[i].Method != BitcoinCommands.SubmitBlock &&
                    requests[i].Method != BitcoinCommands.GetAddressInfo)
                .ToArray();

            if(errors.Any())
                throw new PoolStartupException($"Init RPC failed: {string.Join(", ", errors.Select(y => y.Error.Message))}", poolConfig.Id);
        }

        // extract results
        var validateAddressResponse = responses[0].Error == null ? responses[0].Response.ToObject<ValidateAddressResponse>() : null;
        var submitBlockResponse = responses[1];
        var blockchainInfoResponse = !hasLegacyDaemon ? responses[2].Response.ToObject<BlockchainInfo>() : null;
        var daemonInfoResponse = hasLegacyDaemon ? responses[2].Response.ToObject<DaemonInfo>() : null;
        var difficultyResponse = responses[3].Response.ToObject<JToken>();
        var addressInfoResponse = responses[4].Error == null ? responses[4].Response.ToObject<AddressInfo>() : null;

        // chain detection
        if(!hasLegacyDaemon)
            network = (blockchainInfoResponse.Chain.ToLower() == "nexa") ? Network.Main : Network.GetNetwork(blockchainInfoResponse.Chain.ToLower());
        else
            network = daemonInfoResponse.Testnet ? Network.TestNet : Network.Main;

        PostChainIdentifyConfigure();

        // ensure pool owns wallet
        if(validateAddressResponse is not {IsValid: true})
            throw new PoolStartupException($"Daemon reports pool-address '{poolConfig.Address}' as invalid", poolConfig.Id);

        isPoS = poolConfig.Template is BitcoinTemplate {IsPseudoPoS: true} ||
            (difficultyResponse.Values().Any(x => x.Path == "proof-of-stake" && !difficultyResponse.Values().Any(x => x.Path == "proof-of-work")));
        
        forcePoolAddressDestinationWithPubKey = poolConfig.Template is BitcoinTemplate {ForcePoolAddressDestinationWithPubKey: true};

        // Create pool address script from response
        if(!isPoS && !forcePoolAddressDestinationWithPubKey)
        {
            if(extraPoolConfig != null && extraPoolConfig.AddressType != BitcoinAddressType.Legacy)
                logger.Info(()=> $"Interpreting pool address {poolConfig.Address} as type {extraPoolConfig?.AddressType.ToString()}");

            poolAddressDestination = AddressToDestination(poolConfig.Address, extraPoolConfig?.AddressType);
        }

        else
        {
            logger.Info(()=> $"Interpreting pool address {poolConfig.Address} as raw public key");
            poolAddressDestination = new PubKey(poolConfig.PubKey ?? validateAddressResponse.PubKey);
        }

        // Payment-processing setup
        if(clusterConfig.PaymentProcessing?.Enabled == true && poolConfig.PaymentProcessing?.Enabled == true)
        {
            // ensure pool owns wallet
            if(validateAddressResponse is {IsMine: false} && addressInfoResponse is {IsMine: false})
                logger.Warn(()=> $"Daemon does not own pool-address '{poolConfig.Address}'");
        }

        // update stats
        BlockchainStats.NetworkType = network.Name;
        BlockchainStats.RewardType = isPoS ? "POS" : "POW";

        // block submission RPC method
        if(submitBlockResponse.Error?.Message?.ToLower() == "method not found")
            hasSubmitBlockMethod = false;
        else if(submitBlockResponse.Error?.Code == (int)BitcoinRPCErrorCode.RPC_MISC_ERROR || submitBlockResponse.Error?.Code == (int)BitcoinRPCErrorCode.RPC_INVALID_PARAMS)
            hasSubmitBlockMethod = true;
        else
            throw new PoolStartupException($"Code [{submitBlockResponse.Error?.Code}]: Unable detect block submission RPC method", poolConfig.Id);

        if(!hasLegacyDaemon)
            await UpdateNetworkStatsAsync(ct);
        else
            await UpdateNetworkStatsLegacyAsync(ct);

        // Periodically update network stats
        Observable.Interval(TimeSpan.FromMinutes(10))
            .Select(_ => Observable.FromAsync(() =>
                Guard(()=> !hasLegacyDaemon ? UpdateNetworkStatsAsync(ct) : UpdateNetworkStatsLegacyAsync(ct),
                    ex => logger.Error(ex))))
            .Concat()
            .Subscribe();

        SetupCrypto();
        SetupJobUpdates(ct);
    }
        protected override async Task<(bool IsNew, bool Force)> UpdateJob(CancellationToken ct, bool forceUpdate, string via = null, string json = null)
    {
        try
        {
            if(forceUpdate)
                lastJobRebroadcast = clock.Now;

            var response = string.IsNullOrEmpty(json) ?
                await GetBlockTemplateAsync(ct) :
                GetBlockTemplateFromJson(json);

            // may happen if daemon is currently not connected to peers
            if(response.Error != null)
            {
                logger.Warn(() => $"Unable to update job. Daemon responded with: {response.Error.Message} Code {response.Error.Code}");
                return (false, forceUpdate);
            }

            var blockTemplate = response.Response;
            var job = currentJob;

            var isNew = job == null ||
                (blockTemplate != null &&
                    (job.BlockTemplate?.PreviousBlockhash != blockTemplate.PreviousBlockhash ||
                        blockTemplate.Height > job.BlockTemplate?.Height));

            if(isNew)
                messageBus.NotifyChainHeight(poolConfig.Id, blockTemplate.Height, poolConfig.Template);

            if(isNew || forceUpdate)
            {
                jobId = NextJobId();
                job = new KotoJob(jobId, blockTemplate, poolConfig);

                //job.Init(blockTemplate, NextJobId(),
                //    poolConfig, extraPoolConfig, clusterConfig, clock, poolAddressDestination, network, isPoS,
                //    ShareMultiplier, coin.CoinbaseHasherValue, coin.HeaderHasherValue,
                //    !isPoS ? coin.BlockHasherValue : coin.PoSBlockHasherValue ?? coin.BlockHasherValue);

                lock(jobLock)
                {
                    validJobs.Insert(0, job);

                    // trim active jobs
                    while(validJobs.Count > maxActiveJobs)
                        validJobs.RemoveAt(validJobs.Count - 1);
                }

                if(isNew)
                {
                    if(via != null)
                        logger.Info(() => $"Detected new block {blockTemplate.Height} [{via}]");
                    else
                        logger.Info(() => $"Detected new block {blockTemplate.Height}");

                    // update stats
                    BlockchainStats.LastNetworkBlockTime = clock.Now;
                    BlockchainStats.BlockHeight = blockTemplate.Height;
                    BlockchainStats.NetworkDifficulty = job.Difficulty;
                    BlockchainStats.NextNetworkTarget = blockTemplate.Target;
                    BlockchainStats.NextNetworkBits = blockTemplate.Bits;
                }

                else
                {
                    if(via != null)
                        logger.Debug(() => $"Template update {blockTemplate?.Height} [{via}]");
                    else
                        logger.Debug(() => $"Template update {blockTemplate?.Height}");
                }

                currentJob = job;
            }

            return (isNew, forceUpdate);
        }

        catch(OperationCanceledException)
        {
            // ignored
        }

        catch(Exception ex)
        {
            logger.Error(ex, () => $"Error during {nameof(UpdateJob)}");
        }

        return (false, forceUpdate);
    }
    }
}
