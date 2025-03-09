using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Miningcore.Crypto.Hashing.Ethash;   // Ethash/ProgPoW用のモジュール（Miningcore内蔵）
using Miningcore.Stratum;
using Miningcore.Extensions;
using NLog;
using NBitcoin;

namespace Miningcore.Blockchain.Ethereum.Custom.Quai
{
    /// <summary>
    /// Quai用のマイニングジョブ。ProgPoWアルゴリズム（Ethash派生）を利用してハッシュ計算を行います。
    /// </summary>
    public class QuaiJob : EthereumJob
    {

        public QuaiJob(string id, EthereumBlockTemplate blockTemplate, ILogger logger, IEthashLight ethash, int shareMultiplier = 1) : base(id, blockTemplate, logger, ethash, shareMultiplier)
        {

            // ターゲット値（16進数文字列）を内部表現に変換
            string targetHex = blockTemplate.Target;
            if (targetHex.StartsWith("0x"))
                targetHex = targetHex.Substring(2);
            blockTarget = new uint256(targetHex.HexToReverseByteArray());
        }

        /// <summary>
        /// マイナーから送信されたnonceの重複チェックを行います。
        /// 同じワーカーから同じnonceが送信された場合は例外を投げます。
        /// </summary>
        private void RegisterNonce(StratumConnection worker, string nonceHex)
        {
            string nonceLower = nonceHex.ToLowerInvariant();
            if (!workerNonces.TryGetValue(worker.ConnectionId, out var nonceSet))
            {
                nonceSet = new HashSet<string>();
                workerNonces[worker.ConnectionId] = nonceSet;
            }
            if (nonceSet.Contains(nonceLower))
                throw new StratumException(StratumError.MinusOne, "duplicate share");
            nonceSet.Add(nonceLower);
        }

        /// <summary>
        /// マイナーからのシェア提出を処理します。
        /// nonceを用いたProgPoWハッシュ計算、難易度判定、ブロック候補判定を行います。
        /// </summary>
        /// <param name="worker">シェアを送信してきたマイナーのコネクション</param>
        /// <param name="workerName">マイナー名</param>
        /// <param name="nonceHex">64bitのnonce（16進文字列）</param>
        /// <param name="ethash">Ethash/ProgPoW計算エンジン（Miningcore内蔵）</param>
    public override async Task<SubmitResult> ProcessShareAsync(StratumConnection worker,
        string workerName, string fullNonceHex, string solution, CancellationToken ct)
    {
            // 重複nonceチェック
            lock (workerNonces)
            {
                RegisterNonce(worker, fullNonceHex);
            }

            // nonceの16進文字列をulongに変換
            if (!ulong.TryParse(fullNonceHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong nonce))
                throw new StratumException(StratumError.MinusOne, $"bad nonce {fullNonceHex}");

            // 現在のブロック高に基づくDAGを取得（ProgPoW用）
                    // get dag/light cache for block
        var dag = await ethash.GetCacheAsync(logger, BlockTemplate.Height, ct);
            if (dag == null)
                throw new StratumException(StratumError.MinusOne, "unable to load DAG");

            // HeaderHash（ブロックヘッダ部分）をバイト配列に変換
            byte[] headerHashBytes = BlockTemplate.Header.HexToByteArray();
            // ProgPoW計算を実施し、mixDigestおよび結果ハッシュを取得
            if (!dag.Compute(logger, headerHashBytes, nonce, out byte[] mixDigest, out byte[] resultBytes))
                throw new StratumException(StratumError.MinusOne, "bad hash (PoW computation failed)");

            // 結果ハッシュはビッグエンディアン。内部処理用にリトルエンディアンに変換
            Array.Reverse(resultBytes);
            var resultValue = new uint256(resultBytes);
            BigInteger resultValueBig = resultBytes.AsSpan().ToBigInteger();
            double shareDiff = (double)BigInteger.Divide(EthereumConstants.BigMaxValue, resultValueBig) / EthereumConstants.Pow2x32;

            // マイナーの現在の難易度を取得（EthereumWorkerContextを再利用）
            var context = worker.ContextAs<EthereumWorkerContext>();
            double minerDiff = context.Difficulty;
            bool isValidShare = true;
            double ratio = shareDiff / minerDiff * shareM;
            if (ratio < 0.99)
            {
                // VarDiff（可変難易度）変更直後の場合、前回の難易度で判定
                if (context.VarDiff?.LastUpdate != null && context.PreviousDifficulty.HasValue)
                {
                    double prevDiff = context.PreviousDifficulty.Value;
                    ratio = shareDiff / prevDiff;
                    if (ratio < 0.99)
                        isValidShare = false;
                    else
                        minerDiff = prevDiff;
                }
                else
                {
                    isValidShare = false;
                }
            }
            if (!isValidShare)
                throw new StratumException(StratumError.LowDifficultyShare, $"low difficulty share ({shareDiff:F3} < {minerDiff:F3})");

            // 結果ハッシュがネットワークターゲット未満ならブロック候補
            bool isBlockCandidate = (resultValue <= blockTarget);

            // Shareオブジェクトを作成
            var share = new Share
            {
                BlockHeight      = (long) BlockTemplate.Height,
                IpAddress        = worker.RemoteEndpoint?.Address?.ToString(),
                Miner            = context.Miner,
                Worker           = workerName,
                UserAgent        = context.UserAgent,
                Difficulty       = minerDiff * EthereumConstants.Pow2x32,
                IsBlockCandidate = isBlockCandidate
            };

            if (isBlockCandidate)
            {
                // ブロック候補の場合、ブロック提出用のパラメータを用意
                string headerHashHex = BlockTemplate.Header;
                string mixHashHex   = mixDigest.ToHexString(true);
                string nonceHexFull = "0x" + fullNonceHex.ToLowerInvariant();
                share.BlockHash = headerHashHex;
                share.TransactionConfirmationData = $"{nonceHexFull}:{mixHashHex}";
            }

            return new SubmitResult(share);
        }

        /// <summary>
        /// マイナーへのStratum通知用パラメータ（mining.notify形式）を生成します。
        /// Ethereum互換の形式なので、JobID, seed, header, クリーンジョブフラグを返します。
        /// </summary>
        public override object[] GetJobParamsForStratum()
        {
            return new object[]
            {
                Id,
                BlockTemplate.Seed.StripHexPrefix(),   // "0x"を除去
                BlockTemplate.Header.StripHexPrefix(),
                true   // クリーンジョブフラグ
            };
        }
    }
}
