using System.Runtime.InteropServices;

namespace Miningcore.Native;

public static unsafe class ZKSnark
{
    [DllImport("libzksnark", EntryPoint = "zksnark_generate_keypair", CallingConvention = CallingConvention.Cdecl)]
    public static extern void GenerateKeypair(byte* provingKey, byte* verificationKey);

    [DllImport("libzksnark", EntryPoint = "zksnark_generate_proof", CallingConvention = CallingConvention.Cdecl)]
    public static extern void GenerateProof(byte* provingKey, byte* input, void* proof);

    [DllImport("libzksnark", EntryPoint = "zksnark_verify", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Verify(byte* verificationKey, byte* proof, byte* input);

    [DllImport("libzksnark", EntryPoint = "zksnark_hash", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Hash(byte* input, void* output);

    public static void GenerateKeypair(ReadOnlySpan<byte> input, Span<byte> provingKey, Span<byte> verificationKey)
    {
        fixed(byte* pk = provingKey)
        fixed(byte* vk = verificationKey)
        {
            GenerateKeypair(pk, vk);
        }
    }

    public static void GenerateProof(ReadOnlySpan<byte> provingKey, ReadOnlySpan<byte> input, Span<byte> proof)
    {
        fixed(byte* pk = provingKey)
        fixed(byte* inp = input)
        fixed(byte* prf = proof)
        {
            GenerateProof(pk, inp, prf);
        }
    }

    public static bool Verify(ReadOnlySpan<byte> verificationKey, ReadOnlySpan<byte> proof, ReadOnlySpan<byte> input)
    {
        fixed(byte* vk = verificationKey)
        fixed(byte* prf = proof)
        fixed(byte* inp = input)
        {
            return Verify(vk, prf, inp);
        }
    }

    public static void Hash(ReadOnlySpan<byte> data, Span<byte> result)
    {
        fixed(byte* input = data)
        fixed(byte* output = result)
        {
            Hash(input, output);
        }
    }
}