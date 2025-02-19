using Miningcore.Blockchain.Bitcoin;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Time;
using NBitcoin;

namespace Miningcore.Blockchain.Aleo;

public class AleoJob
{
    public AleoJob(string jobId, AleoBlockTemplate blockTemplate, IMasterClock clock)
    {
        Id = jobId;
        BlockTemplate = blockTemplate;
        Created = clock.Now;
    }

    public string Id { get; }
    public AleoBlockTemplate BlockTemplate { get; }
    public double Difficulty { get; set; }
    public DateTime Created { get; }

    public object[] GetJobParams()
    {
        return new object[]
        {
            Id,
            BlockTemplate.EpochHash,
            BlockTemplate.ProofTarget,
            BlockTemplate.CleanJobs
        };
    }

    public void SetTarget(ulong target)
    {
        BlockTemplate.ProofTarget = target;
    }
}
