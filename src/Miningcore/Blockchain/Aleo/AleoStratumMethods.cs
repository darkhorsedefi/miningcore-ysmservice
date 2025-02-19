namespace Miningcore.Blockchain.Aleo;

public class AleoStratumMethods
{
    /// <summary>
    /// Used to subscribe to work
    /// </summary>
    public const string Subscribe = "mining.subscribe";

    /// <summary>
    /// Used to authorize workers
    /// </summary>
    public const string Authorize = "mining.authorize";

    /// <summary>
    /// Used to submit shares
    /// </summary>
    public const string SubmitShare = "mining.submit";

    /// <summary>
    /// Used to set difficulty per connection
    /// </summary>
    public const string SetDifficulty = "mining.set_difficulty"; 

    /// <summary>
    /// Used to notify about new jobs
    /// </summary>
    public const string MiningNotify = "mining.notify";
}
