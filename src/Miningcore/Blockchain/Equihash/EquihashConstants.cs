using System.Globalization;

namespace Miningcore.Blockchain.Equihash;

public class EquihashConstants
{
    public const int TargetPaddingLength = 32;

    public static readonly System.Numerics.BigInteger ZCashDiff1b =
        System.Numerics.BigInteger.Parse("0007ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", NumberStyles.HexNumber);

    // Koto-specific constants
    // Komodo-specific constants
    public const string KomodoSymbol = "KMD";
    public const int KomodoMainnetRpcPort = 7771;
    public const int KomodoTestnetRpcPort = 17771;

    public static readonly string[] KomodoDaemonCommands = new[]
    {
        "getblocktemplate",
        "submitblock",
        "getblock",
        "getblockhash",
        "getdifficulty",
        "getmininginfo",
        "getnetworkinfo"
    };

    public const string KotoSymbol = "KOTO";
    public const int KotoMainnetRpcPort = 8432;
    public const int KotoTestnetRpcPort = 18432;
    public const int KotoRegtestRpcPort = 18444;

    public static readonly string[] KotoDaemonCommands = new[]
    {
        "getblocktemplate",
        "submitblock",
        "getblock",
        "getblockhash",
        "getdifficulty"
    };
}


public class VeruscoinConstants
{
    public const int SolutionSlice = 6;
    public const string HashVersion2b2 = "2b2"; // PBaaS detection
    public const string HashVersion2b2o = "2b2o";
    public const string HashVersion2b1 = "2b1";
    public const string HashVersion2b = "2b";
    public const string HashVersion2 = "2";
}

public enum ZOperationStatus
{
    Queued,
    Executing,
    Success,
    Cancelled,
    Failed
}

public static class EquihashCommands
{
    public const string ZGetBalance = "z_getbalance";
    public const string ZGetTotalBalance = "z_gettotalbalance";
    public const string ZGetListAddresses = "z_listaddresses";
    public const string ZValidateAddress = "z_validateaddress";
    public const string ZShieldCoinbase = "z_shieldcoinbase";
    
    /// <summary>
    /// Some projects like Veruscoin does not require shielding before being able to spend coins.
    /// They can also sends coins from a t-address to t-addresses and z-addresses
    /// Returns an operation-id. You use the operationid value with z_getoperationstatus and
    /// z_getoperationresult to obtain the result of sending funds, which if successful, will be a txid.
    /// </summary>
    public const string SendCurrency = "sendcurrency";

    /// <summary>
    /// Returns an operationid. You use the operationid value with z_getoperationstatus and
    /// z_getoperationresult to obtain the result of sending funds, which if successful, will be a txid.
    /// </summary>
    public const string ZSendMany = "z_sendmany";

    public const string ZGetOperationStatus = "z_getoperationstatus";
    public const string ZGetOperationResult = "z_getoperationresult";
}

public static class VeruscoinCommands
{
    public const string SubmitMergedBlock = "submitmergedblock";
}
