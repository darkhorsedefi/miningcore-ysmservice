using System.Text.RegularExpressions;
using Miningcore.Configuration;
using Miningcore.Persistence;

namespace Miningcore.Mining
{
    public class PoolUserAuth
    {
        private readonly VerifyUserAddressRepository verifyUserAddressRepository;
        private static readonly Regex DifficultyRegex = new("d=([0-9]+(?:\\.[0-9]+)?)", RegexOptions.Compiled);
        private static readonly Regex MxDifficultyRegex = new("mx=([0-9]+(?:\\.[0-9]+)?)", RegexOptions.Compiled);

        public PoolUserAuth(IConnectionFactory connectionFactory)
        {
            verifyUserAddressRepository = new VerifyUserAddressRepository(connectionFactory, NLog.LogManager.GetCurrentClassLogger());
        }

        public async Task<(bool IsValid, string Address)> ValidateUser(string username, string coinType)
        {
            return await verifyUserAddressRepository.ValidateUser(username, coinType);
        }

        public async Task<(bool IsValid, string Address)> ValidateWorker(string username, string worker, string coinType)
        {
            return await verifyUserAddressRepository.ValidateWorker(username, worker, coinType);
        }

        public async Task UpdateWorkerAuth(string username, string worker, string coinType, string address)
        {
            await verifyUserAddressRepository.UpdateWorkerAuth(username, worker, coinType, address);
        }

        public static (double? difficulty, double? maxDifficulty) ParseDifficultyFromPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return (null, null);

            var diffMatch = DifficultyRegex.Match(password);
            var maxDiffMatch = MxDifficultyRegex.Match(password);

            double? difficulty = null;
            double? maxDifficulty = null;

            if (diffMatch.Success)
            {
                if (double.TryParse(diffMatch.Groups[1].Value, out var diff))
                    difficulty = diff;
            }

            if (maxDiffMatch.Success)
            {
                if (double.TryParse(maxDiffMatch.Groups[1].Value, out var maxDiff))
                    maxDifficulty = maxDiff;
            }

            return (difficulty, maxDifficulty);
        }

        public static async Task<(bool IsValid, string Address, double? Difficulty, double? MaxDifficulty)> ValidateUsername(
            string username, string password, string coinType, PoolUserAuth auth)
        {
            var (diff, maxDiff) = ParseDifficultyFromPassword(password);
            
            if (string.IsNullOrEmpty(username))
                return (false, null, diff, maxDiff);

            var parts = username.Split('.');
            var minerName = parts[0];
            var workerName = parts.Length > 1 ? parts[1] : "default";

            // ワーカー認証を確認
            var (isValidWorker, workerAddress) = await auth.ValidateWorker(minerName, workerName, coinType);
            if (isValidWorker)
            {
                return (true, workerAddress, diff, maxDiff);
            }

            // ユーザーの登録アドレスを確認
            var (isValidUser, userAddress) = await auth.ValidateUser(minerName, coinType);
            if (isValidUser)
            {
                // ワーカー情報を更新
                await auth.UpdateWorkerAuth(minerName, workerName, coinType, userAddress);
                return (true, userAddress, diff, maxDiff);
            }

            return (false, minerName, diff, maxDiff);
        }
    }
}