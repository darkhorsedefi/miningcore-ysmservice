using System;
using System.Security.Cryptography;
using Isopoh.Cryptography.Argon2;
using Miningcore.Contracts;
using SHA3.Net;
using System.Text;

namespace Miningcore.Crypto.Hashing.Algorithms;

/// <summary>
/// RinHash: BLAKE3 → Argon2d → SHA3-256
/// </summary>
[Identifier("rinhash")]
public class RinHash : IHashAlgorithm
{
    private static readonly byte[] Salt;

    static RinHash()
    {
        // "RinCoinSalt" を UTF-8 エンコードして16バイトにする（ゼロパディング）
        Salt = new byte[16];
        var saltBytes = Encoding.UTF8.GetBytes("RinCoinSalt");
        Array.Copy(saltBytes, Salt, saltBytes.Length);
    }

    public void Digest(ReadOnlySpan<byte> data, Span<byte> result, params object[] extra)
    {
        Contract.Requires<ArgumentException>(result.Length >= 32);

        // 1. BLAKE3
        var blake3hasher =  new Blake3()
        var blake3Hashed = stackalloc byte[32];
        blake3hasher.Digest(data, blake3Hashed, extra);

        // 2. Argon2d
        var config = new Argon2Config
        {
            Type = Argon2Type.DataDependentAddressing,
            Version = Argon2Version.Nineteen,
            TimeCost = 2,
            MemoryCost = 65536, // 64 MB
            Lanes = 1,
            Threads = 1,
            Password = blake3Hashed,
            Salt = Salt,
            HashLength = 32
        };

        byte[] argon2Output;
        using (var argon2 = new Argon2(config))
        {
            argon2Output = argon2.Hash();
        }

        // 3. SHA3-256
        var sha3Hashed = Sha3.Sha3256().ComputeHash(argon2Output);

        sha3Hashed.CopyTo(result);
    }
}
