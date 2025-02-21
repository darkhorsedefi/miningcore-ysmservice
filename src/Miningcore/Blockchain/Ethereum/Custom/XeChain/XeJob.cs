using System;
using System.Globalization;
using System.Numerics;
using System.Reactive.Threading.Tasks;
using Miningcore.Crypto.Hashing.Algorithms;
using Miningcore.Crypto.Hashing.Ethash;
using Miningcore.Extensions;
using Miningcore.Stratum;
using Miningcore.Util;
using NBitcoin;
using NLog;

namespace Miningcore.Blockchain.Ethereum.Custom.XeChain;

public class XeJob : EthereumJob
{
    private XeHash xehasher;

    public XeJob(string id, EthereumBlockTemplate blockTemplate, ILogger logger, IEthashLight ethash, int shareMultiplier = 1) : base(id, blockTemplate, logger, ethash, shareMultiplier)
    {
        xehasher = new XeHash();
    }

    public override async Task<SubmitResult> ProcessShareAsync(StratumConnection worker,
        string workerName, string fullNonceHex, string solution, CancellationToken ct)
    {
        // dupe check
        lock(workerNonces)
        {
            RegisterNonce(worker, fullNonceHex);
        }

        var context = worker.ContextAs<EthereumWorkerContext>();

        if(!ulong.TryParse(fullNonceHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var fullNonce))
            throw new StratumException(StratumError.MinusOne, "bad nonce " + fullNonceHex);

        // Prepare header with nonce
        var headerBytes = new byte[32];
        var headerWithNonceBytes = BlockTemplate.Header.HexToByteArray()
            .Concat(BitConverter.GetBytes(fullNonce))
            .ToArray();

        // Calculate hash
        xehasher.Digest(headerWithNonceBytes, headerBytes);

        // test if share meets at least workers current difficulty
        var resultBytes = headerBytes;
        resultBytes.ReverseInPlace();
        
        var resultValue = new uint256(resultBytes);
        var resultValueBig = resultBytes.AsSpan().ToBigInteger();
        var shareDiff = (double) BigInteger.Divide(EthereumConstants.BigMaxValue, resultValueBig) / EthereumConstants.Pow2x32;
        var stratumDifficulty = context.Difficulty;
        var ratio = shareDiff / stratumDifficulty * shareM;
        var isBlockCandidate = resultValue <= blockTarget;

        if(!isBlockCandidate && ratio < 0.99)
        {
            // check if share matched the previous difficulty from before a vardiff retarget
            if(context.VarDiff?.LastUpdate != null && context.PreviousDifficulty.HasValue)
            {
                ratio = shareDiff / context.PreviousDifficulty.Value;

                if(ratio < 0.99)
                    throw new StratumException(StratumError.LowDifficultyShare, $"low difficulty share ({shareDiff})");

                // use previous difficulty
                stratumDifficulty = context.PreviousDifficulty.Value;
            }

            else
                throw new StratumException(StratumError.LowDifficultyShare, $"low difficulty share ({shareDiff})");
        }

        var share = new Share
        {
            BlockHeight = (long) BlockTemplate.Height,
            IpAddress = worker.RemoteEndpoint?.Address?.ToString(),
            Miner = context.Miner,
            Worker = workerName,
            UserAgent = context.UserAgent,
            IsBlockCandidate = isBlockCandidate,
            Difficulty = stratumDifficulty * EthereumConstants.Pow2x32
        };

        if(share.IsBlockCandidate)
        {
            fullNonceHex = "0x" + fullNonceHex;
            var headerHash = BlockTemplate.Header;
            var mixHash = resultBytes.ToHexString(true);

            share.TransactionConfirmationData = "";

            return new SubmitResult(share, fullNonceHex, headerHash, mixHash);
        }

        return new SubmitResult(share);
    }
}
