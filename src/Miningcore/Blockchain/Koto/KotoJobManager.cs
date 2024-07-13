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
using Miningcore.Messaging;
using Miningcore.Extensions;
using Miningcore.Notifications.Messages;
using Miningcore.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using NLog;

namespace Miningcore.Blockchain.Koto
{
    public class KotoJobManager : BitcoinJobManagerBase<KotoJob>
    {
        //private KotoExtraNonceProvider extraNonceProvider;
        private KotoDaemonClient daemonClient;
        //private Network network;
        //private RpcClient rpc;
        public KotoExtraNonceProvider enp;
        protected KotoCoinTemplate coin;
        IExtraNonceProvider extraNonceProvider;
        public KotoJobManager(IComponentContext ctx, IMasterClock clock, IMessageBus messageBus, IExtraNonceProvider extraNonceProvider) 
            : base(ctx, clock, messageBus, extraNonceProvider)
        {
    
            this.extraNonceProvider = extraNonceProvider;
            this.enp = (KotoExtraNonceProvider)extraNonceProvider;
            //ConfigureDaemons();
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
            var response = await daemonClient.GetBlockTemplateAsync(ct);

            if (response.Error != null)
            {
                logger.Warn(() => $"Unable to get block template: {response.Error.Message} (Code: {response.Error.Code})");
                return (false, null);
            }

            var blockTemplate = response.Response;
            var jobId = NextJobId();
            var job = new KotoJob(jobId, blockTemplate, poolConfig, network);

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
        public object gjpfs(bool isNew)
        {
            return GetJobParamsForStratum(isNew);
        }

        protected override object GetJobParamsForStratum(bool isNew)
        {
            var job = currentJob;
            if (job == null)
                return null;

            return new object[]
            {
                job.JobId,
                job.PreviousBlockHash,
                job.CoinbaseTransaction,
                job.Transactions,
                job.merkleBranch,
                ((uint)job.BlockTemplate.Version).ToStringHex8(),
                job.Bits,
                ((uint)job.BlockTemplate.CurTime).ToStringHex8(),
                isNew
            };
        }
    public virtual object[] GetSubscriberData(StratumConnection worker)
    {
        Contract.RequiresNonNull(worker);

        var context = worker.ContextAs<KotoWorkerContext>();

        // assign unique ExtraNonce1 to worker (miner)
        context.ExtraNonce1 = extraNonceProvider.Next();

        // setup response data
        var responseData = new object[]
        {
            context.ExtraNonce1,
            BitcoinConstants.ExtranoncePlaceHolderLength - ExtranonceBytes,
        };

        return responseData;
    }
        protected async Task<RpcResponse<KotoBlockTemplate>> GetBlockTemplateAsync(CancellationToken ct)
        {
            var response = await daemonClient.GetBlockTemplateAsync(ct);

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
            context.ExtraNonce2Size = ((KotoExtraNonceProvider)extraNonceProvider).Size;
        }

        public string[] GetTransactionsForStratum()
        {
            return currentJob.BlockTemplate.Transactions.Select(dr => dr.ToString()).ToArray();;
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
        
    //protected record SubmitResult(bool Accepted, string CoinbaseTx);

    public async ValueTask<Share> SubmitShareAsync(StratumConnection worker,
        object submission, CancellationToken ct)
    {
        Contract.RequiresNonNull(worker);
        Contract.RequiresNonNull(submission);

        if(submission is not object[] submitParams)
            throw new StratumException(StratumError.Other, "invalid params");

        var context = worker.ContextAs<BitcoinWorkerContext>();

        // extract params
        var workerValue = (submitParams[0] as string)?.Trim();
        var jobId = submitParams[1] as string;
        var nTime = submitParams[2] as string;
        var extraNonce2 = submitParams[3] as string;
        var solution = submitParams[4] as string;

        if(string.IsNullOrEmpty(workerValue))
            throw new StratumException(StratumError.Other, "missing or invalid workername");

        if(string.IsNullOrEmpty(solution))
            throw new StratumException(StratumError.Other, "missing or invalid solution");

        KotoJob job;

        lock(jobLock)
        {
            job = validJobs.FirstOrDefault(x => x.JobId == jobId);
        }

        if(job == null)
            throw new StratumException(StratumError.JobNotFound, "job not found");

        // validate & process
        var (share, blockHex) = job.ProcessShare(worker, extraNonce2, nTime, solution);

        // if block candidate, submit & check if accepted by network
        if(share.IsBlockCandidate)
        {
            logger.Info(() => $"Submitting block {share.BlockHeight} [{share.BlockHash}]");
            
            SubmitResult acceptResponse;
            
            acceptResponse = await SubmitBlockAsync(share, blockHex, ct);
            // is it still a block candidate?
            share.IsBlockCandidate = acceptResponse.Accepted;

            if(share.IsBlockCandidate)
            {
                logger.Info(() => $"Daemon accepted block {share.BlockHeight} [{share.BlockHash}] submitted by {context.Miner}");

                OnBlockFound();

                // persist the coinbase transaction-hash to allow the payment processor
                // to verify later on that the pool has received the reward for the block
                share.TransactionConfirmationData = acceptResponse.CoinbaseTx;
            }

            else
            {
                // clear fields that no longer apply
                share.TransactionConfirmationData = null;
            }
        }

        // enrich share with common data
        share.PoolId = poolConfig.Id;
        share.IpAddress = worker.RemoteEndpoint.Address.ToString();
        share.Miner = context.Miner;
        share.Worker = context.Worker;
        share.UserAgent = context.UserAgent;
        share.Source = clusterConfig.ClusterName;
        share.NetworkDifficulty = job.Difficulty;
        share.Difficulty = share.Difficulty;
        share.Created = clock.Now;

        return share;
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
                    (job.BlockTemplate?.PreviousBlockHash != blockTemplate.PreviousBlockHash ||
                        blockTemplate.Height > job.BlockTemplate?.Height));

            if(isNew)
                messageBus.NotifyChainHeight(poolConfig.Id, blockTemplate.Height, poolConfig.Template);

            if(isNew || forceUpdate)
            {
                job = new KotoJob(NextJobId(), blockTemplate, poolConfig, network);

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
    private RpcResponse<DaemonResponses.KotoBlockTemplate> GetBlockTemplateFromJson(string json)
    {
        return JsonConvert.DeserializeObject<RpcResponse<DaemonResponses.KotoBlockTemplate>>(json);
    }
    protected override IDestination AddressToDestination(string address, BitcoinAddressType? addressType)
    {
        if(!coin.UsesZCashAddressFormat)
            return base.AddressToDestination(address, addressType);

        var decoded = Encoders.Base58.DecodeData(address);
        var hash = decoded.Skip(2).Take(20).ToArray();
        var result = new KeyId(hash);
        return result;
    }
    }
}
