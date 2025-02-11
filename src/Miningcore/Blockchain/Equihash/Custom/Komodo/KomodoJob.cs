using System;
using System.Linq;
using System.Threading.Tasks;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Equihash.DaemonResponses;
using Miningcore.Stratum;
using Miningcore.Time;
using Miningcore.Util;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Zcash;

namespace Miningcore.Blockchain.Equihash.Custom.Komodo
{
    public class KomodoJob : EquihashJob
    {
        protected override (Share Share, string BlockHex) ProcessShareInternal(StratumConnection worker,
            string nonce, uint nTime, string solution)
        {
            var context = worker.ContextAs<EquihashWorkerContext>();
            var solutionBytes = (Span<byte>) solution.HexToByteArray();

            // serialize block-header
            var headerBytes = SerializeHeader(nTime, nonce);

            // verify solution
            if(!solver.Verify(headerBytes, solutionBytes))
                throw new StratumException(StratumError.Other, "invalid solution");

            // concat header and solution
            Span<byte> headerSolutionBytes = stackalloc byte[headerBytes.Length + solutionBytes.Length];
            headerBytes.CopyTo(headerSolutionBytes);
            solutionBytes.CopyTo(headerSolutionBytes[headerBytes.Length..]);

            // hash block-header
            Span<byte> headerHash = stackalloc byte[32];
            headerHasher.Digest(headerSolutionBytes, headerHash, (ulong) nTime);
            var headerValue = new uint256(headerHash);

            // calc share-diff
            var shareDiff = (double) new BigRational(networkParams.Diff1BValue, headerHash.ToBigInteger());
            var stratumDifficulty = context.Difficulty;
            var ratio = shareDiff / stratumDifficulty;

            // check if the share meets the much harder block difficulty (block candidate)
            var isBlockCandidate = headerValue <= blockTargetValue;

            // test if share meets at least workers current difficulty
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

            var result = new Share
            {
                BlockHeight = BlockTemplate.Height,
                NetworkDifficulty = Difficulty,
                Difficulty = stratumDifficulty,
            };

            if(isBlockCandidate)
            {
                var headerHashReversed = headerHash.ToNewReverseArray();

                result.IsBlockCandidate = true;
                result.BlockReward = rewardToPool.ToDecimal(MoneyUnit.BTC);
                result.BlockHash = headerHashReversed.ToHexString();

                var blockBytes = SerializeBlock(headerBytes, coinbaseInitial, solutionBytes);
                var blockHex = blockBytes.ToHexString();

                return (result, blockHex);
            }

            return (result, null);
        }

        protected override byte[] SerializeHeader(uint nTime, string nonce)
        {
            var blockHeader = new EquihashBlockHeader
            {
                Version = (int) BlockTemplate.Version,
                Bits = new Target(BlockTemplate.Bits.HexToByteArray()),
                HashPrevBlock = uint256.Parse(BlockTemplate.PreviousBlockhash),
                HashMerkleRoot = new uint256(merkleRoot),
                NTime = nTime,
                Nonce = nonce
            };

            if(isSaplingActive && !string.IsNullOrEmpty(BlockTemplate.FinalSaplingRootHash))
                blockHeader.HashReserved = BlockTemplate.FinalSaplingRootHash.HexToReverseByteArray();

            return blockHeader.ToBytes();
        }

    }
}
