using System.Data;
using System.Threading.Tasks;
using Dapper;
using Miningcore.Extensions;
using Miningcore.Persistence;
using NLog;

namespace Miningcore.Mining
{
    public class VerifyUserAddressRepository
    {
        public VerifyUserAddressRepository(IConnectionFactory connectionFactory, ILogger logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger _logger;

        public async Task<(bool IsValid, string Address)> ValidateUser(string username, string coinType)
        {
            using var con = await _connectionFactory.OpenConnectionAsync();
            
            var query = "SELECT ma.address FROM wallet_addresses ma " +
                       "JOIN users u ON u.id = ma.user_id " +
                       "WHERE u.username = @username AND ma.symbol = @coinType";

            var result = await con.QueryFirstOrDefaultAsync<string>(query, new { username, coinType });
            
            return (result != null, result);
        }

        public async Task<(bool IsValid, string Address)> ValidateWorker(string username, string worker, string coinType)
        {
            using var con = await _connectionFactory.OpenConnectionAsync();
            
            var query = "SELECT address FROM worker_auth " +
                       "WHERE username = @username AND worker_name = @worker AND coin_type = @coinType";

            var result = await con.QueryFirstOrDefaultAsync<string>(query, new { username, worker, coinType });
            
            return (result != null, result);
        }

        public async Task UpdateWorkerAuth(string username, string worker, string coinType, string address)
        {
            using var con = await _connectionFactory.OpenConnectionAsync();
            
            var query = "INSERT INTO worker_auth (username, worker_name, coin_type, address) " +
                       "VALUES (@username, @worker, @coinType, @address) " +
                       "ON CONFLICT (username, worker_name, coin_type) DO UPDATE " +
                       "SET address = @address, last_access = CURRENT_TIMESTAMP";

            await con.ExecuteAsync(query, new { username, worker, coinType, address });
        }
    }
}