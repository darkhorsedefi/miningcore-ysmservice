using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Miningcore.Blockchain.Koto.Configuration;
using Miningcore.Blockchain.Koto.DaemonResponses;
using Miningcore.Blockchain.Bitcoin.DaemonResponses;
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
    public class KotoJobManager : JobManagerBase<KotoJob>
    {
        private KotoExtraNonceProvider extraNonceProvider;
        private KotoDaemonClient daemonClient;
        private Network network;
        private RpcClient rpc;

        public KotoJobManager(IComponentContext ctx, IMasterClock clock, IExtraNonceProvider extraNonceProvider, ILogger<KotoJobManager> logger) 
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

        protected override async Task<(bool IsNew, string Blob)> BuildJobAsync(CancellationToken ct)
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

        protected override async Task<DaemonResponse<KotoBlockTemplate>> GetBlockAsync(CancellationToken ct)
        {
            var response = await daemonClient.GetBlockTemplateAsync();

            if (response.Error != null)
            {
                logger.Warn(() => $"Unable to get block: {response.Error.Message} (Code: {response.Error.Code})");
                return null;
            }

            return response;
        }

        public override void PrepareWorker(StratumConnection worker)
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

    }
}
