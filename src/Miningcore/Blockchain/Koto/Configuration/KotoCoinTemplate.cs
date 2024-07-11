using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Miningcore.Configuration;

namespace Miningcore.Blockchain.Koto.Configuration
{
    public partial class KotoCoinTemplate : CoinTemplate
    {
        [JsonProperty("networkParams")]
        public KotoNetworkParams NetworkParams { get; set; }

        public override string GetAlgorithmName()
        {
            // Return the appropriate algorithm name for Koto
            return "yescrypt";
        }
        public partial class KotoNetworkParams
        {
            [JsonProperty("diff1")]
            public string Diff1 { get; set; }

            [JsonProperty("solutionSize")]
            public int SolutionSize { get; set; } = 1344;

            [JsonProperty("solutionPreambleSize")]
            public int SolutionPreambleSize { get; set; } = 3;

            [JsonProperty("solver")]
            public JObject Solver { get; set; }
        }
    }
}
