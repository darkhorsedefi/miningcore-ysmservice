using Newtonsoft.Json;

namespace Miningcore.Blockchain.Koto.DaemonResponses
{
    public class KotoBlockTemplate
    {
        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("coinbasetxn")]
        public CoinbaseTransaction CoinbaseTxn { get; set; }

        [JsonProperty("finalsaplingroothash")]
        public string FinalSaplingRootHash { get; set; }

        [JsonProperty("transactions")]
        public Transaction[] Transactions { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("coinbasevalue")]
        public long CoinbaseValue { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("mintime")]
        public long MinTime { get; set; }

        [JsonProperty("mutable")]
        public string[] Mutable { get; set; }

        [JsonProperty("noncerange")]
        public string NonceRange { get; set; }

        [JsonProperty("sigoplimit")]
        public int SigOpLimit { get; set; }

        [JsonProperty("sizelimit")]
        public int SizeLimit { get; set; }

        [JsonProperty("curtime")]
        public long CurTime { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("height")]
        public ulong Height { get; set; }
    }

    public class CoinbaseTransaction
    {
        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class Transaction
    {
        [JsonProperty("data")]
        public string Data { get; set; }
    }
}
