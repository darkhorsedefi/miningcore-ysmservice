using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Miningcore.Blockchain.Koto.DaemonResponses;
using Miningcore.Blockchain;
using Miningcore.Configuration;
using Miningcore.Contracts;
using Miningcore.Stratum;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Miningcore.Blockchain.Koto
{
    public class KotoJob
    {
        public KotoBlockTemplate BlockTemplate { get; private set; }
        public PoolConfig PoolConfig { get; private set; }
        public string PreviousBlockHash { get; private set; }
        public string CoinbaseTransaction { get; private set; }
        public string[] Transactions { get; private set; }
        public string MerkleRoot { get; private set; }
        public string Bits { get; private set; }
        public string Time { get; private set; }
        public string Nonce { get; private set; }
        public double Difficulty { get; set; }
        protected EquihashCoinTemplate coin;
        protected Network network;
        protected Money rewardToPool;
        protected byte[] coinbaseInitial;
        private readonly ConcurrentDictionary<string, bool> submits = new(StringComparer.OrdinalIgnoreCase);

        public KotoJob(string id, KotoBlockTemplate blockTemplate, PoolConfig poolConfig) : base(id)
        {
            BlockTemplate = blockTemplate;
            coin = poolConfig.Template.As<EquihashCoinTemplate>();
            networkParams = coin.GetNetwork(network.ChainName);
            Difficulty = (double) new BigRational(networkParams.Diff1BValue, BlockTemplate.Target.HexToReverseByteArray().AsSpan().ToBigInteger());
            PoolConfig = poolConfig;
            PreviousBlockHash = blockTemplate.PrevBlockHash;
            CoinbaseTransaction = CreateCoinbaseTransaction();
            Transactions = blockTemplate.Transactions;
            MerkleRoot = CalculateMerkleRoot();
            Bits = blockTemplate.Bits;
            Time = blockTemplate.Time;
            Nonce = blockTemplate.Nonce;
        }

        private string CreateCoinbaseTransaction()
        {
            var extraNoncePlaceholder = new byte[4]; // Placeholder for extraNonce

            var p1 = SerializeCoinbasePart1(extraNoncePlaceholder);
            //var p2 = SerializeCoinbasePart2();
            //coinbaseInitial = p1;
                    txOut = CreateOutputTransaction();

        using(var stream = new MemoryStream())
        {   
            var bs = new BitcoinStream(stream, true);

            if(isOverwinterActive)
            {
                uint mask = (isOverwinterActive ? 1u : 0u );
                uint shiftedMask = mask << 31;
                uint versionWithOverwinter = txVersion | shiftedMask;

                // version
                bs.ReadWrite(ref versionWithOverwinter);
            }
            else
            {
                // version
                bs.ReadWrite(ref txVersion);
            }

            if(isOverwinterActive || isSaplingActive)
            {
                bs.ReadWrite(ref txVersionGroupId);
            }

            // serialize (simulated) input transaction
            bs.ReadWriteAsVarInt(ref txInputCount);
            bs.ReadWrite(sha256Empty);
            bs.ReadWrite(ref coinbaseIndex);
            bs.ReadWrite(ref script);
            bs.ReadWrite(ref coinbaseSequence);

            // serialize output transaction
            var txOutBytes = SerializeOutputTransaction(txOut);
            bs.ReadWrite(txOutBytes);

            // misc
            bs.ReadWrite(ref txLockTime);

            if(isOverwinterActive || isSaplingActive)
            {
                txExpiryHeight = (uint) BlockTemplate.Height;
                bs.ReadWrite(ref txExpiryHeight);
            }

            if(isSaplingActive)
            {
                bs.ReadWrite(ref txBalance);
                bs.ReadWriteAsVarInt(ref txVShieldedSpend);
                bs.ReadWriteAsVarInt(ref txVShieldedOutput);
            }

            if(isOverwinterActive || isSaplingActive)
            {
                bs.ReadWriteAsVarInt(ref txJoinSplits);
            }

            // done
            coinbaseInitial = stream.ToArray();

            coinbaseInitialHash = new byte[32];
            sha256D.Digest(coinbaseInitial, coinbaseInitialHash);
            p2 = coinbaseInitialHash;
        }
            return p1;//Combine(p1, extraNoncePlaceholder, p2);
        }

        private byte[] SerializeCoinbasePart1(byte[] extraNoncePlaceholder)
        {
            var txVersion = 1;
            var txInputsCount = 1;
            var txOutputsCount = 1;
            var txLockTime = 0;
            var txInPrevOutHash = new byte[32]; // "0" in hex
            var txInPrevOutIndex = BitConverter.GetBytes(uint.MaxValue);
            var txInSequence = BitConverter.GetBytes(uint.MaxValue);

            var scriptSigPart1 = Combine(
                SerializeNumber(BlockTemplate.Height),
                SerializeNumber(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                new[] { (byte)extraNoncePlaceholder.Length }
            );

            var nVersionGroupId = GetVersionGroupId(txVersion);

            var p1 = Combine(
                BitConverter.GetBytes(txVersion),
                nVersionGroupId,
                new byte[0], // txTimestamp for POS coins
                VarIntBuffer(txInputsCount),
                txInPrevOutHash,
                txInPrevOutIndex,
                VarIntBuffer(scriptSigPart1.Length + extraNoncePlaceholder.Length),
                scriptSigPart1
            );

            return p1;
        }

/*        private byte[] SerializeCoinbasePart2()
        {
            var txInSequence = BitConverter.GetBytes(uint.MaxValue);
            var txLockTime = BitConverter.GetBytes(0);
            var outputTransactions = GenerateOutputTransactions();
            var txComment = new byte[0]; // For coins that support/require transaction comments

            var nExpiryHeight = GetExpiryHeight(BlockTemplate.Version);
            var valueBalance = GetValueBalance(BlockTemplate.Version);
            var vShieldedSpend = GetShieldedSpend(BlockTemplate.Version);
            var vShieldedOutput = GetShieldedOutput(BlockTemplate.Version);
            var nJoinSplit = GetJoinSplit(BlockTemplate.Version);

            var p2 = Combine(
                SerializeString(GetBlockIdentifier()),
                txInSequence,
                outputTransactions,
                txLockTime,
                nExpiryHeight,
                valueBalance,
                vShieldedSpend,
                vShieldedOutput,
                nJoinSplit,
                txComment
            );

            return p2;
        }*/

        private byte[] GetVersionGroupId(int txVersion)
        {
            if ((txVersion & 0x7fffffff) == 3)
                return BitConverter.GetBytes(0x2e7d970);
            if ((txVersion & 0x7fffffff) == 4)
                return BitConverter.GetBytes(0x9023e50a);
            return new byte[0];
        }

        private byte[] GetExpiryHeight(int txVersion)
        {
            return (txVersion & 0x7fffffff) >= 3 ? BitConverter.GetBytes(0) : new byte[0];
        }

        private byte[] GetValueBalance(int txVersion)
        {
            return (txVersion & 0x7fffffff) >= 4 ? BitConverter.GetBytes(0L) : new byte[0];
        }

        private byte[] GetShieldedSpend(int txVersion)
        {
            return (txVersion & 0x7fffffff) >= 4 ? new byte[] { 0 } : new byte[0];
        }

        private byte[] GetShieldedOutput(int txVersion)
        {
            return (txVersion & 0x7fffffff) >= 4 ? new byte[] { 0 } : new byte[0];
        }

        private byte[] GetJoinSplit(int txVersion)
        {
            return (txVersion & 0x7fffffff) >= 2 ? new byte[] { 0 } : new byte[0];
        }

    protected virtual byte[] CreateOutputTransaction()
    {
        var txNetwork = Network.GetNetwork(networkParams.CoinbaseTxNetwork);
        var tx = Transaction.Create(txNetwork);

        // set versions
        tx.Version = txVersion;

        // calculate outputs
        if(networkParams.PayFundingStream)
        {
            rewardToPool = new Money(Math.Round(blockReward * (1m - (networkParams.PercentFoundersReward) / 100m)) + rewardFees, MoneyUnit.Satoshi);
            tx.Outputs.Add(rewardToPool, poolAddressDestination);

            foreach(FundingStream fundingstream in BlockTemplate.Subsidy.FundingStreams)
            {
                var amount = new Money(Math.Round(fundingstream.ValueZat / 1m), MoneyUnit.Satoshi);
                var destination = FoundersAddressToScriptDestination(fundingstream.Address);
                tx.Outputs.Add(amount, destination);
            }
        }
        else if(networkParams.vOuts)
        {
            rewardToPool = new Money(Math.Round(blockReward * (1m - (networkParams.vPercentFoundersReward) / 100m)) + rewardFees, MoneyUnit.Satoshi);
            tx.Outputs.Add(rewardToPool, poolAddressDestination);
            var destination = FoundersAddressToScriptDestination(networkParams.vTreasuryRewardAddress);
            var amount = new Money(Math.Round(blockReward * (networkParams.vPercentTreasuryReward / 100m)), MoneyUnit.Satoshi);
            tx.Outputs.Add(amount, destination);
            destination = FoundersAddressToScriptDestination(networkParams.vSecureNodesRewardAddress);
            amount = new Money(Math.Round(blockReward * (networkParams.percentSecureNodesReward / 100m)), MoneyUnit.Satoshi);
            tx.Outputs.Add(amount, destination);
            destination = FoundersAddressToScriptDestination(networkParams.vSuperNodesRewardAddress);
            amount = new Money(Math.Round(blockReward * (networkParams.percentSuperNodesReward / 100m)), MoneyUnit.Satoshi);
            tx.Outputs.Add(amount, destination);
        }
        else if(networkParams.PayFoundersReward &&
                (networkParams.LastFoundersRewardBlockHeight >= BlockTemplate.Height ||
                    networkParams.TreasuryRewardStartBlockHeight > 0))
        {
            // founders or treasury reward?
            if(networkParams.TreasuryRewardStartBlockHeight > 0 &&
               BlockTemplate.Height >= networkParams.TreasuryRewardStartBlockHeight)
            {
                // pool reward (t-addr)
                rewardToPool = new Money(Math.Round(blockReward * (1m - (networkParams.PercentTreasuryReward) / 100m)) + rewardFees, MoneyUnit.Satoshi);
                tx.Outputs.Add(rewardToPool, poolAddressDestination);

                // treasury reward (t-addr)
                var destination = FoundersAddressToScriptDestination(GetTreasuryRewardAddress());
                var amount = new Money(Math.Round(blockReward * (networkParams.PercentTreasuryReward / 100m)), MoneyUnit.Satoshi);
                tx.Outputs.Add(amount, destination);
            }

            else
            {
                // pool reward (t-addr)
                rewardToPool = new Money(Math.Round(blockReward * (1m - (networkParams.PercentFoundersReward) / 100m)) + rewardFees, MoneyUnit.Satoshi);
                tx.Outputs.Add(rewardToPool, poolAddressDestination);

                // founders reward (t-addr)
                var destination = FoundersAddressToScriptDestination(GetFoundersRewardAddress());
                var amount = new Money(Math.Round(blockReward * (networkParams.PercentFoundersReward / 100m)), MoneyUnit.Satoshi);
                tx.Outputs.Add(amount, destination);
            }
        }

        else
        {
            // no founders reward
            // pool reward (t-addr)
            rewardToPool = new Money(blockReward + rewardFees, MoneyUnit.Satoshi);
            tx.Outputs.Add(rewardToPool, poolAddressDestination);
        }

        tx.Inputs.Add(TxIn.CreateCoinbase((int) BlockTemplate.Height));

        return tx;
    }

        private byte[] SerializeNumber(long value)
        {
            return BitConverter.GetBytes(value);
        }

        private string GetBlockIdentifier()
        {
            return "https://github.com/zone117x/node-stratum";
        }

        private string Sha256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private string ComputeMerkleRoot(List<string> txHashes)
        {
            if (txHashes.Count == 0)
                return string.Empty;

            while (txHashes.Count > 1)
            {
                if (txHashes.Count % 2 != 0)
                    txHashes.Add(txHashes.Last());

                var newLevel = new List<string>();

                for (int i = 0; i < txHashes.Count; i += 2)
                {
                    var left = txHashes[i];
                    var right = txHashes[i + 1];
                    newLevel.Add(Sha256Hash(left + right));
                }

                txHashes = newLevel;
            }

            return txHashes[0];
        }

    public virtual (Share Share, string BlockHex) ProcessShare(StratumConnection worker, string extraNonce2, string nTime, string solution)
    {
        Contract.RequiresNonNull(worker);
        Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(extraNonce2));
        Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(nTime));
        Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(solution));

        var context = worker.ContextAs<BitcoinWorkerContext>();

        // validate nTime
        if(nTime.Length != 8)
            throw new StratumException(StratumError.Other, "incorrect size of ntime");

        var nTimeInt = uint.Parse(nTime.HexToReverseByteArray().ToHexString(), NumberStyles.HexNumber);
        if(nTimeInt < BlockTemplate.CurTime || nTimeInt > ((DateTimeOffset) clock.Now).ToUnixTimeSeconds() + 7200)
            throw new StratumException(StratumError.Other, "ntime out of range");

        var nonce = context.ExtraNonce1 + extraNonce2;

        // validate nonce
        if(nonce.Length != 64)
            throw new StratumException(StratumError.Other, "incorrect size of extraNonce2");

        // validate solution
        if(solution.Length != (networkParams.SolutionSize + networkParams.SolutionPreambleSize) * 2)
            throw new StratumException(StratumError.Other, "incorrect size of solution");

        // dupe check
        if(!RegisterSubmit(nonce, solution))
            throw new StratumException(StratumError.DuplicateShare, "duplicate share");

        return ProcessShareInternal(worker, nonce, nTimeInt, solution);
    }

        private byte[] SerializeCoinbase(byte[] extraNonce1, byte[] extraNonce2)
        {
            var p1 = SerializeCoinbasePart1(extraNonce1);
            var p2 = SerializeCoinbasePart2();

            return Combine(p1, extraNonce1, extraNonce2, p2);
        }

        private byte[] SerializeHeader(byte[] merkleRoot, string nTime, string nonce)
        {
            int headerSize = BlockTemplate.Version == 5 ? 112 : 80;
            var header = new byte[headerSize];
            int position = 0;

            if (BlockTemplate.Version == 5)
            {
                var saplingRoot = Encoders.Hex.DecodeData(BlockTemplate.FinalSaplingRootHash);
                Array.Copy(saplingRoot, 0, header, position, saplingRoot.Length);
                position += saplingRoot.Length;
            }

            var nonceBytes = Encoders.Hex.DecodeData(nonce);
            var nTimeBytes = Encoders.Hex.DecodeData(nTime);
            var bitsBytes = Encoders.Hex.DecodeData(BlockTemplate.Bits);
            var prevHashBytes = Encoders.Hex.DecodeData(BlockTemplate.PreviousBlockHash);
            var versionBytes = BitConverter.GetBytes(BlockTemplate.Version);

            Array.Copy(nonceBytes, 0, header, position, nonceBytes.Length);
            position += nonceBytes.Length;

            Array.Copy(bitsBytes, 0, header, position, bitsBytes.Length);
            position += bitsBytes.Length;

            Array.Copy(nTimeBytes, 0, header, position, nTimeBytes.Length);
            position += nTimeBytes.Length;

            Array.Copy(merkleRoot, 0, header, position, merkleRoot.Length);
            position += merkleRoot.Length;

            Array.Copy(prevHashBytes, 0, header, position, prevHashBytes.Length);
            position += prevHashBytes.Length;

            Array.Copy(versionBytes, 0, header, position, versionBytes.Length);

            Array.Reverse(header);
            return header;
        }

        private byte[] SerializeBlock(byte[] header, byte[] coinbase)
        {
            var transactions = BlockTemplate.Transactions.Select(Encoders.Hex.DecodeData).ToList();
            var transactionCount = (ulong)(transactions.Count + 1);

            var block = new List<byte>(header);
            block.AddRange(VarIntBuffer(transactionCount));
            block.AddRange(coinbase);
            transactions.ForEach(tx => block.AddRange(tx));

            // POSコインの場合は0バイトを追加
//            if (PoolConfig.Template.Reward == RewardType.POS)
//            {
//                block.Add(0);
 //           }

            return block.ToArray();
        }

        private bool RegisterSubmit(string extraNonce1, string extraNonce2, string nTime, string nonce)
        {
            var submission = extraNonce1 + extraNonce2 + nTime + nonce;
            return submits.TryAdd(submission, true);
        }

        private byte[] ReverseBytes(byte[] bytes)
        {
            Array.Reverse(bytes);
            return bytes;
        }

        private byte[] Combine(params byte[][] arrays)
        {
            var combined = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (var array in arrays)
            {
                Buffer.BlockCopy(array, 0, combined, offset, array.Length);
                offset += array.Length;
            }
            return combined;
        }

        private byte[] VarIntBuffer(ulong value)
        {
            if (value < 0xfd)
                return new byte[] { (byte)value };

            if (value <= 0xffff)
            {
                var buffer = new byte[3];
                buffer[0] = 0xfd;
                Array.Copy(BitConverter.GetBytes((ushort)value), 0, buffer, 1, 2);
                return buffer;
            }

            if (value <= 0xffffffff)
            {
                var buffer = new byte[5];
                buffer[0] = 0xfe;
                Array.Copy(BitConverter.GetBytes((uint)value), 0, buffer, 1, 4);
                return buffer;
            }

            var buffer64 = new byte[9];
            buffer64[0] = 0xff;
            Array.Copy(BitConverter.GetBytes(value), 0, buffer64, 1, 8);
            return buffer64;
        }

        private byte[] SerializeString(string str)
        {
            var strBytes = Encoding.UTF8.GetBytes(str);
            var length = VarIntBuffer((ulong)strBytes.Length);
            return Combine(length, strBytes);
        }
            protected virtual (Share Share, string BlockHex) ProcessShareInternal(StratumConnection worker, string nonce,
        uint nTime, string solution)
    {
        var context = worker.ContextAs<BitcoinWorkerContext>();
        var solutionBytes = (Span<byte>) solution.HexToByteArray();

        // serialize block-header
        var headerBytes = SerializeHeader(nTime, nonce);

        // verify solution
        if(!solver.Verify(headerBytes, solutionBytes[networkParams.SolutionPreambleSize..]))
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

    }
}
