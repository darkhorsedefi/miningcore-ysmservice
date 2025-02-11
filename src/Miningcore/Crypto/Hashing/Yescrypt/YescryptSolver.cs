using System;
using Miningcore.Native;
using Miningcore.Extensions;

namespace Miningcore.Crypto.Hashing.Yescrypt
{
public unsafe class YescryptSolver : IYescryptSolver
{
private readonly int N;
private readonly int r;
private readonly string personalization;

    public YescryptSolver(int N, int r, string personalization)
    {
        this.N = N;
        this.r = r;
        this.personalization = personalization;
    }

    public bool Verify(string solution)
    {
        if (string.IsNullOrEmpty(solution))
            return false;

        var solutionBytes = solution.HexToByteArray();
        
        try
        {
            // Call the native verify function
            fixed(byte* input = solutionBytes)
            {
                return Multihash.yescrypt_verify(input, (uint)solutionBytes.Length, (uint)N, (uint)r);
            }
        }
        catch
        {
            return false;
        }
    }

    public byte[] Hash(byte[] data)
    {
        var result = new byte[32];

        fixed(byte* input = data)
        fixed(byte* output = result)
        {
            Multihash.yescrypt(input, output, (uint)data.Length);
        }

        return result;
    }
}
}
