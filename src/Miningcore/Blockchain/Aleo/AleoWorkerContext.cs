using Miningcore.Mining;

namespace Miningcore.Blockchain.Aleo
{
    public class AleoWorkerContext : WorkerContextBase
    {
        public AleoWorkerContext()
        {
            Jobs = new List<AleoJob>();
        }

        /// <summary>
        /// Cached job parameters returned to client in response to mining.subscribe
        /// </summary>
        public object[] JobParams { get; set; }

        public List<AleoJob> Jobs { get; private set; }

        public AleoJob GetJob(string jobId)
        {
            return Jobs.FirstOrDefault(x => x.Id == jobId);
        }

        public double Difficulty { get; set; }
        public string UserAgent { get; set; }
        public bool IsSubscribed { get; set; }
        public bool IsAuthorized { get; set; }
        public string Miner { get; set; }
        public string Worker { get; set; }
    }
}
