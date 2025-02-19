using Miningcore.Configuration;
using Newtonsoft.Json.Linq;

namespace Miningcore.Blockchain.Aleo.Configuration
{
    public class AleoCoinTemplate : CoinTemplate
    {
        public override string GetAlgorithmName()
        {
            return "zkSnark";
        }

        public string AleoNetworkVersion { get; set; } = "mainnet"; // デフォルトはtestnet3
        
        public int MinimumConfirmations { get; set; } = 10;

        public JToken ExtraConfig { get; set; }

        public void Parse(JObject extra)
        {
            if(extra == null)
                return;

            ExtraConfig = extra;
        }
    }
}
