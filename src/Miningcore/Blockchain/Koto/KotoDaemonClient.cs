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

        public async Task<DaemonResponse<KotoBlockTemplate>> GetBlockTemplateAsync()
        {
            return await rpcClient.ExecuteAsync<KotoBlockTemplate>(logger, KotoCommands.GetBlockTemplate);
        }

        public async Task<DaemonResponse<JToken>> SubmitBlockAsync(string blockHex)
        {
            return await rpcClient.ExecuteAsync<JToken>(logger, KotoCommands.SubmitBlock, new[] { blockHex });
        }

        public async Task<DaemonResponse<string>> SendManyAsync(string fromAddress, JArray recipients, int minConf, decimal fee)
        {
            var args = new JArray { fromAddress, recipients, minConf, fee };
            return await rpcClient.ExecuteAsync<string>(logger, KotoCommands.SendMany, args);
        }

        public async Task<DaemonResponse<string>> GetOperationResultAsync(string operationId)
        {
            var args = new JArray { new JArray { operationId } };
            return await rpcClient.ExecuteAsync<string>(logger, KotoCommands.GetOperationResult, args);
        }
    }
}
