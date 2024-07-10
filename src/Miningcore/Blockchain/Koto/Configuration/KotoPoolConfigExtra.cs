
using System;
using MiningCore.Blockchain.Common.Configuration;
using Newtonsoft.Json;

namespace MiningCore.Blockchain.Koto.Configuration
{
    public class KotoPoolConfigExtra : PoolConfigExtra
    {
        [JsonProperty("z-address")]
        public string ZAddress { get; set; }
    }
}
