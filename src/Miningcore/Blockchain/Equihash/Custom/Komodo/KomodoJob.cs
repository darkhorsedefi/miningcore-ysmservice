using System;
using System.Linq;
using System.Threading.Tasks;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Blockchain.Bitcoin;
using Miningcore.Blockchain.Equihash.DaemonResponses;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Miningcore.Blockchain.Equihash.Custom.Komodo
{
    public class KomodoJob : EquihashJob
    {
        protected override (Share Share, string BlockHex) ProcessShareInternal(StratumConnection worker,
            string nonce, string solution, double stratumDifficulty, double stratumDifficultyBase)
        {
            // Nonce validation
            if (nonce.Length != 64)
                throw new StratumException(StratumError.Other, "incorrect size of nonce");

            // Solution validation
            if (solution.Length != 2694)
                throw new StratumException(StratumError.Other, "incorrect size of solution");

            // Duplicate check
            if (!RegisterSubmit(nonce, solution))
                throw new StratumException(StratumError.DuplicateShare, "duplicate share");

            // Verify solution
            if (!VerifyHeader(nonce, solution))
                throw new StratumException(StratumError.Other, "invalid solution");

            // Build block header
            var headerBytes = SerializeHeader(nonce);
            var solutionBytes = solution.HexToByteArray();
            var headerSolutionBytes = headerBytes.Concat(solutionBytes).ToArray();

            // Calculate share difficulty
            var resultBytes = headerSolutionBytes.AsSpan();
            var resultHash = new Span<byte>(new byte[32]);
            Sha256D(resultBytes, resultHash);
            var shareDiff = (double) new BigRational(coinbaseInitial.Difficulty1, resultHash.ToBigInteger());

            // Check block candidate
            var isBlockCandidate = shareDiff >= BlockTemplate.Target;

            // Calculate block hash
            var blockHash = resultHash.ToHexString();
            var blockHex = SerializeBlock(headerBytes, solutionBytes).ToHexString();

            // Create share
            var share = new Share
            {
                BlockHeight = BlockTemplate.Height,
                NetworkDifficulty = Difficulty,
                Difficulty = stratumDifficulty,
            };

            if (isBlockCandidate)
            {
                share.IsBlockCandidate = true;
                share.BlockHash = blockHash;
                share.BlockReward = BlockTemplate.Reward;
            }

            return (share, isBlockCandidate ? blockHex : null);
        }

        private bool VerifyHeader(string nonce, string solution)
        {
            var headerBytes = SerializeHeader(nonce);
            var solutionBytes = solution.HexToByteArray();

            using(var hasher = new EquihashHasher(Network))
            {
                return hasher.Verify(headerBytes, solutionBytes);
            }
        }

        protected override byte[] SerializeHeader(string nonce)
        {
            var blockHeader = BlockTemplate.Version.ToBytes(true)
                .Concat(BlockTemplate.PrevHash.HexToByteArray().ReverseArray())
                .Concat(BlockTemplate.MerkleRoot.HexToByteArray().ReverseArray())
                .Concat(BlockTemplate.FinalSaplingRoot.HexToByteArray().ReverseArray())
                .Concat(BlockTemplate.Time.ToBytes(true))
                .Concat(Bits.HexToByteArray().ReverseArray())
                .Concat(BlockTemplate.Nonce.HexToByteArray().ReverseArray())
                .Concat(BlockTemplate.Solution.HexToByteArray());

            return blockHeader.ToArray();
        }

        private static void Sha256D(Span<byte> input, Span<byte> output)
        {
            using(var hasher = System.Security.Cryptography.SHA256.Create())
            {
                hasher.TryComputeHash(input, output, out _);
                hasher.TryComputeHash(output, output, out _);
            }
        }
    }
}
