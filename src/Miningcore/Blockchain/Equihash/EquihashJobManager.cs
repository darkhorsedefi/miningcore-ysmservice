using System.Globalization;
using Autofac;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Bitcoin.DaemonResponses;
using Miningcore.Blockchain.Equihash.Custom.BitcoinGold;
using Miningcore.Blockchain.Equihash.Custom.Komodo;
using Miningcore.Blockchain.Equihash.Custom.Minexcoin;
using Miningcore.Blockchain.Equihash.Custom.Piratechain;
using Miningcore.Blockchain.Equihash.Custom.Veruscoin;
using Miningcore.Blockchain.Equihash.DaemonResponses;
using Miningcore.Configuration;
using Miningcore.Contracts;
using Miningcore.Crypto.Hashing.Equihash;
using Miningcore.Extensions;
using Miningcore.JsonRpc;
using Miningcore.Messaging;
using Miningcore.Notifications.Messages;
using Miningcore.Rpc;
using Miningcore.Stratum;
using Miningcore.Time;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using NLog;

namespace Miningcore.Blockchain.Equihash;

public class EquihashJobManager : BitcoinJobManagerBase<EquihashJob>
{
    public EquihashJobManager(
        IComponentContext ctx,
        IMasterClock clock,
        IMessageBus messageBus,
        IExtraNonceProvider extraNonceProvider) : base(ctx, clock, messageBus, extraNonceProvider)
    {
    }

    private EquihashCoinTemplate coin;
    private EquihashSolver solver;

    public EquihashCoinTemplate.EquihashNetworkParams ChainConfig { get; private set; }

    protected override void PostChainIdentifyConfigure()
    {
        ChainConfig = coin.GetNetwork(network.ChainName);
        solver = EquihashSolverFactory.GetSolver(ctx, ChainConfig.Solver);

        base.PostChainIdentifyConfigure();
    }

    private async Task<RpcResponse<EquihashBlockTemplate>> GetBlockTemplateAsync(CancellationToken ct)
    {
        var subsidyResponse = await rpc.ExecuteAsync<ZCashBlockSubsidy>(logger, BitcoinCommands.GetBlockSubsidy, ct);
        
        var result = await rpc.ExecuteAsync<EquihashBlockTemplate>(logger,
            BitcoinCommands.GetBlockTemplate, ct, extraPoolConfig?.GBTArgs ?? (object) GetBlockTemplateParams());

        if(subsidyResponse.Error == null && result.Error == null && result.Response != null)
            result.Response.Subsidy = subsidyResponse.Response;
        else if(subsidyResponse.Error?.Code != (int) BitcoinRPCErrorCode.RPC_METHOD_NOT_FOUND)
            result = new RpcResponse<EquihashBlockTemplate>(null, new JsonRpcError(-1, $"{BitcoinCommands.GetBlockSubsidy} failed", null));
        
        return result;
    }

    private RpcResponse<EquihashBlockTemplate> GetBlockTemplateFromJson(string json)
    {
        var result = JsonConvert.DeserializeObject<JsonRpcResponse>(json);

        return new RpcResponse<EquihashBlockTemplate>(result.ResultAs<EquihashBlockTemplate>());
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

    private EquihashJob CreateJob()
    {
        switch(coin.Symbol)
        {
            case "ARRR":
                return new PiratechainJob();

            case "BTG":
                return new BitcoinGoldJob();

            case "MNX":
                return new MinexcoinJob();
            
            case "VRSC":
                return new VeruscoinJob();
            case "KMD":
            case "SQCN":
                return new KomodoJob();
        }

        return new EquihashJob();
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
                job = CreateJob();

                job.Init(blockTemplate, NextJobId(),
                    poolConfig, clusterConfig, clock, poolAddressDestination, network, solver);

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
                        logger.Debug(() => $"Template update {blockTemplate.Height} [{via}]");
                    else
                        logger.Debug(() => $"Template update {blockTemplate.Height}");
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

    protected override object GetJobParamsForStratum(bool isNew)
    {
        var job = currentJob;
        return job?.GetJobParams(isNew);
    }

    public override EquihashJob GetJobForStratum()
    {
        var job = currentJob;
        return job;
    }

    #region API-Surface

    public override void Configure(PoolConfig pc, ClusterConfig cc)
    {
        coin = pc.Template.As<EquihashCoinTemplate>();

        base.Configure(pc, cc);
    }

    public override async Task<bool> ValidateAddressAsync(string address, CancellationToken ct)
    {
        if(string.IsNullOrEmpty(address))
            return false;
        
        // handle t-addr
        if(await base.ValidateAddressAsync(address, ct))
            return true;
        
        if(!coin.UseBitcoinPayoutHandler)
        {
            // handle z-addr
            var result = await rpc.ExecuteAsync<ValidateAddressResponse>(logger,
                EquihashCommands.ZValidateAddress, ct, new[] { address });

            return result.Response is {IsValid: true};
        }
        
        return false;
    }

    public object[] GetSubscriberData(StratumConnection worker)
    {
        Contract.RequiresNonNull(worker);

        var context = worker.ContextAs<EquihashWorkerContext>();

        // assign unique ExtraNonce1 to worker (miner)
        context.ExtraNonce1 = extraNonceProvider.Next();

        // setup response data
        var responseData = new object[]
        {
            context.ExtraNonce1
        };

        return responseData;
    }

    public async ValueTask<Share> SubmitShareAsync(StratumConnection worker,
        object submission, CancellationToken ct)
    {
        Contract.RequiresNonNull(worker);
        Contract.RequiresNonNull(submission);

        if(submission is not object[] submitParams)
            throw new StratumException(StratumError.Other, "invalid params");

        var context = worker.ContextAs<EquihashWorkerContext>();

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

        EquihashJob job;

        lock(context)
        {
            job = context.GetJob(jobId);
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
            
            switch(coin.Symbol)
            {
                case "VRSC":
                    // when PBaaS activates we must use the coinbasetxn from daemon to get proper fee pool calculations in coinbase
                    var solutionVersion = job.BlockTemplate.Solution.Substring(0, 8);
                    var reversedSolutionVersion = uint.Parse(solutionVersion.HexToReverseByteArray().ToHexString(), NumberStyles.HexNumber);
                    var isPBaaSActive = (reversedSolutionVersion > 6);
                    
                    acceptResponse = await SubmitVeruscoinBlockAsync(share, blockHex, isPBaaSActive, ct);
                    
                    break;
                default:
                    acceptResponse = await SubmitBlockAsync(share, blockHex, ct);
                    
                    break;
            }

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
    
    protected async Task<SubmitResult> SubmitVeruscoinBlockAsync(Share share, string blockHex, bool isPBaaSActive, CancellationToken ct)
    {
        var requestCommand = isPBaaSActive ? VeruscoinCommands.SubmitMergedBlock : BitcoinCommands.SubmitBlock;
        var batch = new []
        {
            new RpcRequest(requestCommand, new[] { blockHex }),
            new RpcRequest(BitcoinCommands.GetBlock, new[] { share.BlockHash })
        };

        var results = await rpc.ExecuteBatchAsync(logger, ct, batch);

        // did submission succeed?
        var submitResult = results[0];
        var submitError = submitResult.Error?.Message ??
            submitResult.Error?.Code.ToString(CultureInfo.InvariantCulture) ??
            submitResult.Response?.ToString();

        if((!isPBaaSActive && !string.IsNullOrEmpty(submitError)) || (isPBaaSActive && !submitError.Contains("accepted")))
        {
            logger.Warn(() => $"Block {share.BlockHeight} submission failed with: {submitError}");
            messageBus.SendMessage(new AdminNotification("Block submission failed", $"Pool {poolConfig.Id} {(!string.IsNullOrEmpty(share.Source) ? $"[{share.Source.ToUpper()}] " : string.Empty)}failed to submit block {share.BlockHeight}: {submitError}"));
            return new SubmitResult(false, null);
        }

        // was it accepted?
        var acceptResult = results[1];
        var block = acceptResult.Response?.ToObject<Bitcoin.DaemonResponses.Block>();
        var accepted = acceptResult.Error == null && block?.Hash == share.BlockHash;

        if(!accepted)
        {
            logger.Warn(() => $"Block {share.BlockHeight} submission failed for pool {poolConfig.Id} because block was not found after submission");
            messageBus.SendMessage(new AdminNotification($"[{share.PoolId.ToUpper()}]-[{share.Source}] Block submission failed", $"[{share.PoolId.ToUpper()}]-[{share.Source}] Block {share.BlockHeight} submission failed for pool {poolConfig.Id} because block was not found after submission"));
        }

        return new SubmitResult(accepted, block?.Transactions.FirstOrDefault());
    }

    #endregion // API-Surface
}
