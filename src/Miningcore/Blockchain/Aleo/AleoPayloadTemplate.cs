using Newtonsoft.Json;

namespace Miningcore.Blockchain.Aleo;

public class AleoSubscribeRequest
{
    [JsonProperty("user_agent")]
    public string UserAgent { get; set; }

    [JsonProperty("protocol_version")] 
    public string ProtocolVersion { get; set; }

    [JsonProperty("session_id")]
    public string SessionId { get; set; }
}

public class AleoSubscribeResponse
{
    [JsonProperty("session_id")]
    public string SessionId { get; set; }

    [JsonProperty("server_nonce")]
    public string ServerNonce { get; set; }
}

public class AleoAuthorizeRequest
{
    [JsonProperty("worker_name")]
    public string WorkerName { get; set; }

    [JsonProperty("password")]
    public string Password { get; set; }
}

public class AleoSubmitShareRequest 
{
    [JsonProperty("worker_name")]
    public string WorkerName { get; set; }

    [JsonProperty("job_id")] 
    public string JobId { get; set; }
    
    [JsonProperty("counter")]
    public string Counter { get; set; }
}
