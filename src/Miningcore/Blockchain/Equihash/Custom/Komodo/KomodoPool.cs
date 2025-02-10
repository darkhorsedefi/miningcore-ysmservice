using Autofac;
using AutoMapper;
using Microsoft.IO;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Equihash.Configuration;
using Miningcore.Blockchain.Equihash.DaemonResponses;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Rpc;
using Miningcore.Time;
using Newtonsoft.Json;
using NLog;
using Contract = Miningcore.Contracts.Contract;

namespace Miningcore.Blockchain.Equihash.Custom.Komodo
{
    [CoinFamily(CoinFamily.Equihash)]
    public class KomodoPool : EquihashPool
    {
        public KomodoPool(IComponentContext ctx,
            JsonSerializerSettings serializerSettings,
            IConnectionFactory cf,
            IStatsRepository statsRepo,
            IMapper mapper,
            IMasterClock clock,
            IMessageBus messageBus,
            RecyclableMemoryStreamManager rmsm,
            ILoggerFactory loggerFactory) :
            base(ctx, serializerSettings, cf, statsRepo, mapper, clock, messageBus, rmsm, loggerFactory)
        {
        }

        protected override async Task<bool> AreDaemonsHealthyAsync(CancellationToken ct)
        {
            var responses = await base.AreDaemonsHealthyAsync(ct);

            if (!responses)
                return false;

            var infoResponse = await rpc.ExecuteAsync<GetMiningInfoResponse>(logger,
                BitcoinCommands.GetMiningInfo, ct);

            if (infoResponse.Error != null || infoResponse.Response == null)
            {
                logger.Error(() => $"Error(s) reading mining info: {infoResponse.Error}");
                return false;
            }

            return true;
        }

        protected override void BlockchainStats(IStatisticsUpdater stats)
        {
            base.BlockchainStats(stats);

            stats.UpdateHashrateSolo(manager.LastStats?.PoolHashrate ?? 0);
            stats.UpdateHashratePool(manager.LastStats?.NetworkHashrate ?? 0);
        }

        public override void Configure(PoolConfig pc, ClusterConfig cc)
        {
            Contract.RequiresNonNull(pc);

            poolConfig = pc;
            clusterConfig = cc;
            extraPoolConfig = pc.Extra.SafeExtensionDataAs<EquihashPoolConfigExtra>();

            if (extraPoolConfig?.MaxActiveJobs == null)
                extraPoolConfig = new EquihashPoolConfigExtra
                {
                    MaxActiveJobs = 4,
                    MaxViewCache = 10
                };

            base.Configure(pc, cc);
        }

        private record SubmitResponse(string BlockHash, string CoinbaseTx);

        protected override async Task<Share> SubmitShareAsync(IClient worker,
            IShare share, string blockHash, string blockHex, CancellationToken ct)
        {
            Contract.RequiresNonNull(share);

            var context = worker.GetContextAs<BitcoinWorkerContext>();

            var extraNonce1 = context.ExtraNonce1;
            var job = share.Job as KomodoJob;
            var nTime = job.BlockTemplate.Time;
            var nonce = share.Nonce;
            var soln = share.Solution;

            if (job == null)
                throw new StratumException(StratumError.Other, "bad job");

            // submit block
            var response = await rpc.ExecuteAsync<SubmitResponse>(logger,
                BitcoinCommands.SubmitBlock, ct, new[] { blockHex });

            if (response.Error != null)
            {
                throw new StratumException(StratumError.Other, $"block submit failed: {response.Error.Message}");
            }

            return share;
        }

        protected override async Task<(Share Share, string BlockHex)> ProcessShareAsync(
            IClient worker, IShare share, CancellationToken ct)
        {
            Contract.RequiresNonNull(share);

            var context = worker.GetContextAs<BitcoinWorkerContext>();

            var job = share.Job as KomodoJob;
            var nTime = job.BlockTemplate.Time;
            var nonce = share.Nonce;
            var soln = share.Solution;

            if (job == null)
                throw new StratumException(StratumError.Other, "bad job");

            // validate & process
            var (shareResult, blockHex) = job.ProcessShare(worker, nonce, soln);

            // enrich share with common data
            share.BlockHash = shareResult.BlockHash;
            share.NetworkDifficulty = shareResult.NetworkDifficulty;
            share.TransactionConfirmationData = blockHex;

            return (share, blockHex);
        }

        protected override IShare ProcessShareInternal(IClient worker, IShare share)
        {
            var context = worker.GetContextAs<BitcoinWorkerContext>();

            var job = share.Job as KomodoJob;
            var nTime = job.BlockTemplate.Time;
            var nonce = share.Nonce;
            var soln = share.Solution;

            if (job == null)
                throw new StratumException(StratumError.Other, "bad job");

            // validate & process
            job.ProcessShare(worker, nonce, soln);

            return share;
        }

        protected override Task<bool> ValidateAddressAsync(string address, CancellationToken ct)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(address));

            var result = !string.IsNullOrEmpty(address) && address.Length == 34;
            return Task.FromResult(result);
        }
    }
}
