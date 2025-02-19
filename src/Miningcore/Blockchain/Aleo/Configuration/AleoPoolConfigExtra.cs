namespace Miningcore.Blockchain.Aleo.Configuration;

public class AleoPoolConfigExtra
{
    /// <summary>
    /// Maximum number of tracked jobs
    /// </summary>
    public int? MaxActiveJobs { get; set; }

    /// <summary>
    /// Optional custom address validation settings
    /// </summary>
    public string AddressValidationRegex { get; set; }
}
