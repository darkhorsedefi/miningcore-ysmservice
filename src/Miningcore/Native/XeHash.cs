using System.Runtime.InteropServices;

namespace Miningcore.Native;

public static unsafe class XeHash
{
    [DllImport("libxehash", EntryPoint = "xehash", CallingConvention = CallingConvention.Cdecl)]
    public static extern void XeHashHash(byte* input, void* output);
    
    public static void Hash(ReadOnlySpan<byte> data, Span<byte> result)
    {
        fixed(byte* input = data)
        {
            fixed(byte* output = result)
            {
                XeHashHash(input, output);
            }
        }
    }
}
