using System.Numerics;

namespace Miningcore.Blockchain.Aleo;

public static class AleoConstants
{
    public const uint TargetPadding = 64;
    public const uint CounterSize = 32;
    public const string AleoStratumVersion = "AleoStratum/3.0.0";
    
    public const string StatumZero = "0";
    public const decimal SmallestUnit = 1000000;  // 1 Aleo = 10^6 microAleos
    public const int PayoutMinBlockConfirmations = 20;
    
    public const int ExtranonceBits = 32;
    public const int ExtraNonceSize = ExtranonceBits / 8;
    
    public const string BlockTypeBlock = "block";
    public const string BlockTypeUncle = "uncle";
    
    public static readonly BigInteger Diff1 = BigInteger.Parse("00000000ffff0000000000000000000000000000000000000000000000000000", System.Globalization.NumberStyles.HexNumber);
    public static readonly double Pow2xDiff1TargetNumZero = 8; // 2^8 = 256 Leading zero bits in Diff1 target
    
    public const int DaemonRpcPortMainnet = 3030;
    public const int DaemonRpcPortTestnet = 3032;
    
    public const string WalletDaemonCategory = "wallet";
}
