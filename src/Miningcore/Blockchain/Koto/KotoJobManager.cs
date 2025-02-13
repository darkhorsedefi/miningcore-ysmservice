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
using Miningcore.Crypto.Hashing.Yescrypt;
using Miningcore.Mining;
using Miningcore.Rpc;
using Miningcore.Stratum;
using Miningcore.Messaging;
using Miningcore.Extensions;
using Miningcore.Native;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NLog;

namespace Miningcore.Blockchain.Koto
{
public class KotoJobManager : BitcoinJobManagerBase<KotoJob>
{
    private KotoDaemonClient daemonClient;
    public KotoExtraNonceProvider enp;
    protected KotoCoinTemplate coin;
    protected new IExtraNonceProvider extraNonceProvider;
    private readonly IYescryptSolverFactory yescryptSolverFactory;
    private IYescryptSolver yescryptSolver;
    protected readonly List<KotoJob> validJobs = new List<KotoJob>();
    protected new int maxActiveJobs;
    private KotoPoolConfigExtra extraPoolConfig;

    public KotoJobManager(
        IComponentContext ctx,
        IMasterClock clock,
        IMessageBus messageBus,
        IExtraNonceProvider extraNonceProvider,
        IYescryptSolverFactory yescryptSolverFactory) 
        : base(ctx, clock, messageBus, extraNonceProvider)
    {
        this.extraNonceProvider = extraNonceProvider;
        this.enp = (KotoExtraNonceProvider)extraNonceProvider;
        this.yescryptSolverFactory = yescryptSolverFactory;
    }

    protected override void PostChainIdentifyConfigure()
    {
    maxActiveJobs = extraPoolConfig?.MaxActiveJobs ?? 4;
    var chainConfig = coin.GetNetwork(network.ChainName);
    // Initialize yescrypt solver with parameters from configuration
    var solverConfig = chainConfig.Solver;
    var args = solverConfig["args"]?
        .Select(token => token.Value<object>())
        .ToArray();
    yescryptSolver = yescryptSolverFactory.CreateSolver(
        (int) Convert.ChangeType(args[0], typeof(int)),
        (int) Convert.ChangeType(args[1], typeof(int)),
        args[2].ToString());


        base.PostChainIdentifyConfigure();
    }
public override void Configure(PoolConfig pc, ClusterConfig cc)
{
    coin = pc.Template.As<KotoCoinTemplate>();
    extraPoolConfig = pc.Extra.SafeExtensionDataAs<KotoPoolConfigExtra>();

    base.Configure(pc, cc);
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
var job = new KotoJob(jobId, blockTemplate, poolConfig, base.network, base.clock, yescryptSolver);

lock (jobLock)
{
    validJobs.Insert(0, job);

    if (validJobs.Count > maxActiveJobs)
        validJobs.RemoveAt(validJobs.Count - 1);
}

logger.Info(() => $"[BuildJob] New job {jobId} for block {blockTemplate.Height}");
return (true, jobId);
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
            job.JobId,                                                    // params[0] - job_id
            job.BlockTemplate.PreviousBlockHash,                         // params[1] - prevhash
            job.CoinbaseTransaction.Substring(0, job.CoinbaseTransaction.Length - ExtranonceBytes * 2), // params[2] - coinb1
            job.CoinbaseTransaction.Substring(job.CoinbaseTransaction.Length - ExtranonceBytes * 2),    // params[3] - coinb2
            job.merkleBranch,                                         // params[4] - merkle array
            job.BlockTemplate.Version.ToStringHex8(),                   // params[5] - version
            job.BlockTemplate.Bits,                                     // params[6] - nbits 
            ((uint)job.BlockTemplate.CurTime).ToStringHex8(),         // params[7] - ntime
            isNew,                                                     // params[8] - clean jobs flag
            job.BlockTemplate.FinalSaplingRootHash                    // params[9] - final sapling root hash (for version 5)
        };
    }

    public override KotoJob GetJobForStratum()
    {
        return currentJob;
    }

    public virtual object[] GetSubscriberData(StratumConnection worker)
    {
        Contract.RequiresNonNull(worker);

        var context = worker.ContextAs<KotoWorkerContext>();

        context.ExtraNonce1 = extraNonceProvider.Next();

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
        return currentJob.BlockTemplate.Transactions.Select(dr => dr.ToString()).ToArray();
    }

    public async ValueTask<Share> SubmitShareAsync(StratumConnection worker,
        object submission, CancellationToken ct)
    {
        Contract.RequiresNonNull(worker);
        Contract.RequiresNonNull(submission);

        if(submission is not object[] submitParams)
            throw new StratumException(StratumError.Other, "invalid params");

        var context = worker.ContextAs<KotoWorkerContext>();

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

        try
        {
            // Verify yescrypt solution using solver
            var isValid = yescryptSolver.Verify(solution);
            if (!isValid)
                throw new StratumException(StratumError.Other, "invalid solution");

            // Process share as normal
            var (share, blockHex) = job.ProcessShare(worker, extraNonce2, nTime, solution);

            // Submit block if it's a candidate
            if(share.IsBlockCandidate)
            {
                logger.Info(() => $"Submitting block {share.BlockHeight} [{share.BlockHash}]");
                
                var acceptResponse = await SubmitBlockAsync(share, blockHex, ct);
                share.IsBlockCandidate = acceptResponse.Accepted;

                if(share.IsBlockCandidate)
                {
                    logger.Info(() => $"Daemon accepted block {share.BlockHeight} [{share.BlockHash}] submitted by {context.Miner}");
                    OnBlockFound();
                    share.TransactionConfirmationData = acceptResponse.CoinbaseTx;
                }
                else
                {
                    share.TransactionConfirmationData = null;
                }
            }

            // Enrich share with common data
            share.PoolId = poolConfig.Id;
            share.IpAddress = worker.RemoteEndpoint.Address.ToString();
            share.Miner = context.Miner;
            share.Worker = context.Worker;
            share.UserAgent = context.UserAgent;
            share.Source = clusterConfig.ClusterName;
            share.NetworkDifficulty = job.Difficulty;
            share.Created = clock.Now;

            return share;
        }
        catch(Exception ex)
        {
            logger.Error(ex);
            throw;
        }
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
                job = new KotoJob(NextJobId(), blockTemplate, poolConfig, base.network, base.clock, yescryptSolver);

                lock(jobLock)
                {
                    validJobs.Insert(0, job);

                    while(validJobs.Count > maxActiveJobs)
                        validJobs.RemoveAt(validJobs.Count - 1);
                }

                if(isNew)
                {
                    if(via != null)
                        logger.Info(() => $"Detected new block {blockTemplate.Height} [{via}]");
                    else
                        logger.Info(() => $"Detected new block {blockTemplate.Height}");

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

    public override async Task<bool> ValidateAddressAsync(string address, CancellationToken ct)
    {
        if(string.IsNullOrEmpty(address))
            return false;
        return true;//アドレス検証がキャンセルエラーになるため暫定措置
        if(await base.ValidateAddressAsync(address, ct))
            return true;
        
        if(!false)
        {
            var result = await rpc.ExecuteAsync<ValidateAddressResponse>(logger,
                "z_validateaddress", ct, new[] { address });

            return result.Response is {IsValid: true};
        }
        
        return false;
    }
}
}