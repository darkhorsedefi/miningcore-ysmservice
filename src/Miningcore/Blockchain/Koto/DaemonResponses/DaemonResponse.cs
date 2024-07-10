using Newtonsoft.Json;

namespace MiningCore.Blockchain.Koto.DaemonResponses
{
    public class DaemonResponse<T>
    {
        [JsonProperty("result")]
        public T Result { get; set; }

        [JsonProperty("error")]
        public DaemonError Error { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class DaemonError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
