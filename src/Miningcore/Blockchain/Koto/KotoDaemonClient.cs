using System.Threading.Tasks;
using Miningcore.Blockchain.Koto.DaemonResponses;
using Miningcore.Rpc;
using Newtonsoft.Json.Linq;
using NLog;

namespace Miningcore.Blockchain.Koto
{
    public class KotoDaemonClient
    {
        private readonly RpcClient rpcClient;
        private readonly ILogger logger;

        public KotoDaemonClient(RpcClient rpcClient, ILogger logger)
        {
            this.rpcClient = rpcClient;
            this.logger = logger;
        }

        public async Task<RpcResponse<KotoBlockTemplate>> GetBlockTemplateAsync(CancellationToken ct)
        {
            return await rpcClient.ExecuteAsync<KotoBlockTemplate>(logger, KotoCommands.GetBlockTemplate, ct);
        }

        public async Task<RpcResponse<JToken>> SubmitBlockAsync(string blockHex, CancellationToken ct)
        {
            return await rpcClient.ExecuteAsync<JToken>(logger, KotoCommands.SubmitBlock, ct, new[] { blockHex });
        }

        public async Task<RpcResponse<string>> SendManyAsync(string fromAddress, JArray recipients, int minConf, decimal fee, CancellationToken ct)
        {
            var args = new JArray { fromAddress, recipients, minConf, fee };
            return await rpcClient.ExecuteAsync<string>(logger, KotoCommands.SendMany, ct, args);
        }

        public async Task<RpcResponse<string>> GetOperationResultAsync(string operationId, CancellationToken ct)
        {
            var args = new JArray { new JArray { operationId } };
            return await rpcClient.ExecuteAsync<string>(logger, KotoCommands.GetOperationResult, ct, args);
        }
    }
}
