using System;
using Miningcore.Stratum;

namespace Miningcore.Blockchain.Koto
{
    public class KotoWorkerContext : StratumWorkerContext
    {
        public string ExtraNonce1 { get; set; }
        public int ExtraNonce2Size { get; set; }
    }
}
