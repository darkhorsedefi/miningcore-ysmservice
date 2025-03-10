using Newtonsoft.Json;

namespace Miningcore.Blockchain.Bitcoin.DaemonResponses;

public class GetWorkResponse
{
    public string Data { get; set; }
    public string Target { get; set; }
    public string Hash1 { get; set; }
    public string Midstate { get; set; }
}