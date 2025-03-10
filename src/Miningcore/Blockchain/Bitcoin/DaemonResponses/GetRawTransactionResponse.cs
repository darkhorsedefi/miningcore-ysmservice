using Newtonsoft.Json;

namespace Miningcore.Blockchain.Bitcoin.DaemonResponses;

public class RawTransaction
{
    public string Hex { get; set; }
    public string TxId { get; set; }
    public string Hash { get; set; }
}