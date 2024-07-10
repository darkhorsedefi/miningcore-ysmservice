using System;
using MiningCore.Stratum;

namespace MiningCore.Blockchain.Koto
{
    public class KotoWorkerContext : StratumWorkerContext
    {
        public string ExtraNonce1 { get; set; }
        public int ExtraNonce2Size { get; set; }
    }
}
