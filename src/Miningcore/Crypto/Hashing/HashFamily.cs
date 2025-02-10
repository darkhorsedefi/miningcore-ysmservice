using System.Runtime.CompilerServices;

namespace Miningcore.Crypto.Hashing
{
    public enum HashFamily : int
    {
        SHA256 = 1,
        Scrypt = 2,
        X11 = 3,
        Equihash = 4,
        Ethash = 5,
        Groestl = 6,
        Lyra2 = 7,
        Yescrypt = 8  // Yescryptを追加
    }
}
