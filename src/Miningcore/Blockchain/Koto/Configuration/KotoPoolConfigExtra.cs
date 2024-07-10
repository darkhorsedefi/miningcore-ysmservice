
using System;
using Miningcore.Blockchain.Common.Configuration;
using Newtonsoft.Json;

namespace Miningcore.Blockchain.Koto.Configuration
{
    public class KotoPoolConfigExtra : PoolConfigExtra
    {
        [JsonProperty("z-address")]
        public string ZAddress { get; set; }
    }
}
