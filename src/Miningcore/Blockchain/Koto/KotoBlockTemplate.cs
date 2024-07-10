using System;
using Newtonsoft.Json;

namespace MiningCore.Blockchain.Koto
{
    public class KotoBlockTemplate : BlockTemplate
    {
        [JsonProperty("previousblockhash")]
        public string PrevBlockHash { get; set; }

        [JsonProperty("coinbaseaux")]
        public object CoinbaseAux { get; set; }

        [JsonProperty("coinbasevalue")]
        public long CoinbaseValue { get; set; }

        [JsonProperty("longpollid")]
        public string LongPollId { get; set; }

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
        public long CurrentTime { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}
