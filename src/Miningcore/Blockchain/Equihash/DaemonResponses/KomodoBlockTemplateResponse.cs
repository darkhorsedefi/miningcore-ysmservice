using Newtonsoft.Json;
using Miningcore.Serialization;

namespace Miningcore.Blockchain.Equihash.DaemonResponses
{
    public class KomodoBlockTemplateResponse : EquihashBlockTemplateResponse
    {
        [JsonProperty("finalsaplingroothash")]
        public string FinalSaplingRoot { get; set; }

        // Komodo specific reward
        [JsonProperty("miner")]
        public decimal MinerReward { get; set; }

        public override decimal Reward => MinerReward * 100000000;

        [JsonProperty("longpollid")]
        public string LongPollId { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("mintime")]
        public uint MinTime { get; set; }

        [JsonProperty("mutable")]
        public string[] Mutable { get; set; }

        [JsonProperty("noncerange")]
        public string NonceRange { get; set; }

        [JsonProperty("capabilities")]
        public string[] Capabilities { get; set; }

        [JsonProperty("sol_size")]
        public uint SolutionSize { get; set; } = 1344u;
    }
}
