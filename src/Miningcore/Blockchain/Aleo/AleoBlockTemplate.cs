using Newtonsoft.Json;

namespace Miningcore.Blockchain.Aleo;

public class AleoBlockTemplate
{
    public string JobId { get; set; }
    public string EpochHash { get; set; }
    public ulong ProofTarget { get; set; }
    public uint Height { get; set; }
    public bool CleanJobs { get; set; } = true;
}
