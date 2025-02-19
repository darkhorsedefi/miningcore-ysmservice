using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.JsonRpc;
using Miningcore.Stratum;
using Miningcore.Messaging;
using Miningcore.Rest;
using Newtonsoft.Json.Linq;

namespace Miningcore.Blockchain.Aleo;

public class AleoClient
{
    public AleoClient(DaemonEndpointConfig endPoint, IMessageBus messageBus, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        this.endPoint = endPoint;
        this.messageBus = messageBus;
        this.logger = loggerFactory.CreateLogger<AleoClient>();
    
        var baseUrl = (endPoint.Ssl ? "https://" : "http://") + endPoint.Host + ":" + endPoint.Port;
        restClient = new SimpleRestClient(httpClientFactory, baseUrl);
    }
private readonly DaemonEndpointConfig endPoint;
private readonly IMessageBus messageBus;
private readonly SimpleRestClient restClient;
private readonly ILogger logger;
private string AleoNetworkVersion = "mainnet";

public void SetNetworkVersion(string version)
{
    AleoNetworkVersion = version;
}

    public async Task<bool> ValidateAddressAsync(string address)
    {
        var response = await restClient.Get<JObject>($"/mainnet/address/{address}", CancellationToken.None);
        return response != null;
    }

    public async Task<AleoBlockTemplate> GetBlockTemplateAsync()
    {
        var response = await restClient.Get<AleoBlockTemplate>("/mainnet/latest/block", CancellationToken.None);
        return response;
    }
    
    public async Task<bool> SubmitBlockAsync(string blockHex)
    {
        var response = await restClient.Post<JObject>("/mainnet/transaction/broadcast", new {transaction = blockHex}, CancellationToken.None);
        return response != null;
    }

    public async Task<bool> ValidatePartialSolutionAsync(string epochHash, string address, ulong nonce, CancellationToken ct)
    {
        try
        {
            var response = await restClient.Post<JObject>("/mainnet/validate", new
            {
                epoch_hash = epochHash,
                address = address,
                nonce = nonce
            }, ct);

            return response?["valid"]?.Value<bool>() ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ulong> GetProofTargetAsync(string epochHash, string poolAddress, ulong nonce)
    {
        try
        {
            var ct = CancellationToken.None;
            var response = await restClient.Get<JObject>($"/{AleoNetworkVersion}/target", ct,
                new Dictionary<string, string>
                {
                    { "epoch_hash", epochHash },
                    { "address", poolAddress },
                    { "counter", nonce.ToString() }
                });

            var target = response?["target"]?.Value<ulong>();
            if (!target.HasValue)
                throw new Exception("Invalid response from target endpoint");

            return target.Value;
        }
        catch (Exception ex)
        {
            logger.LogError("Error getting proof target: {0}", ex.Message);
            throw;
        }
    }

    public async Task<bool> NotifyAsync(StratumConnection connection, string jobId, string epochHash, string address, bool cleanJobs)
    {
        try
        {
            await connection.NotifyAsync(AleoStratumMethods.MiningNotify, new object[] 
            { 
                jobId,
                epochHash,
                address,
                cleanJobs
            });
            
            return true;
        }
        catch(Exception ex)
        {
            logger.LogError("Error during notify: {0}", ex.Message);
            return false;
        }
    }

    public async Task<bool> SetDifficultyAsync(StratumConnection connection, double difficulty)
    {
        try
        {
            await connection.NotifyAsync(AleoStratumMethods.SetDifficulty, new object[] { difficulty });
            return true;
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error during SetDifficulty");
            return false;
        }
    }
}
