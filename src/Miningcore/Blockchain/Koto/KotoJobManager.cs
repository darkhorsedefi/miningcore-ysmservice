using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Miningcore.Blockchain.Koto.Configuration;
using Miningcore.Blockchain.Koto.DaemonResponses;
using Miningcore.Configuration;
using Miningcore.Contracts;
using Miningcore.Crypto;
using Miningcore.Mining;
using Miningcore.Rpc;
using Miningcore.Stratum;
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

        protected override async Task<DaemonResponse<Block>> GetBlockAsync(CancellationToken ct)
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
    }
}
