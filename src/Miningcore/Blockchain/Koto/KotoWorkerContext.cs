using System;
using Miningcore.Mining;

namespace Miningcore.Blockchain.Koto
{
    public class KotoWorkerContext : WorkerContextBase
    {
        public override string Miner { get; set; }
        public override string Worker { get; set; }
        public string ExtraNonce1 { get; set; }
        public int ExtraNonce2Size { get; set; }
    }
}
