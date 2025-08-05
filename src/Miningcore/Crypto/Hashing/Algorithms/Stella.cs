using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Miningcore.Contracts;

namespace Miningcore.Crypto.Hashing.Algorithms;

/// <summary>
/// Stella PoW hashing algorithm
/// Based on the GetHashForPoW implementation from Stella blockchain
/// </summary>
[Identifier("stella")]
public class Stella : IHashAlgorithm
{
    public void Digest(ReadOnlySpan<byte> data, Span<byte> result, params object[] extra)
    {
        Contract.Requires<ArgumentException>(result.Length >= 32);
        Contract.Requires<ArgumentException>(data.Length >= 80);

        // Create a buffer for the block data (80 bytes)
        Span<byte> blockData = stackalloc byte[80];
        
        // Extract components from the input data
        // Assuming the input data contains: nVersion(4) + hashPrevBlock(32) + hashMerkleRoot(32) + nTime(8) + nBits(4) + nNonce(4)
        var nVersion = MemoryMarshal.Read<uint>(data.Slice(0, 4));
        var hashPrevBlock = data.Slice(4, 32);
        var hashMerkleRoot = data.Slice(36, 32);
        var nTime = MemoryMarshal.Read<ulong>(data.Slice(68, 8));
        var nBits = MemoryMarshal.Read<uint>(data.Slice(76, 4));
        
        // For nNonce, we need to check if it's provided in extra parameters or use a default
        uint nNonce = 0;
        if (extra != null && extra.Length > 0 && extra[0] is uint nonceValue)
        {
            nNonce = nonceValue;
        }

        // Copy data according to Stella's GetHashForPoW logic
        MemoryMarshal.Write(blockData.Slice(0, 4), ref nVersion);
        hashPrevBlock.CopyTo(blockData.Slice(4, 32));
        hashMerkleRoot.CopyTo(blockData.Slice(36, 32));

        // Apply Stella's special logic for nTime and nBits placement
        // "Legacy" PoW: the hash is done after swapping nTime and nBits
        // if ((nNonce & 1) == 1 || (nNonce & 65535) == 0)
        if ((nNonce & 1) == 1 || (nNonce & 0xFFFF) == 0)
        {
            // Legacy mode: nBits at offset 68, nTime at offset 72
            MemoryMarshal.Write(blockData.Slice(68, 4), ref nBits);
            MemoryMarshal.Write(blockData.Slice(72, 8), ref nTime);
        }
        else
        {
            // Normal mode: nTime at offset 68, nBits at offset 76
            MemoryMarshal.Write(blockData.Slice(68, 8), ref nTime);
            MemoryMarshal.Write(blockData.Slice(76, 4), ref nBits);
        }

        // Hash the Block Header without nNonce (using SHA-256 double hash like Bitcoin)
        using (var hasher = SHA256.Create())
        {
            hasher.TryComputeHash(blockData, result, out _);
            hasher.TryComputeHash(result, result, out _);
        }
    }

    /// <summary>
    /// Alternative method that takes structured block header data
    /// </summary>
    public void DigestBlockHeader(uint nVersion, ReadOnlySpan<byte> hashPrevBlock, ReadOnlySpan<byte> hashMerkleRoot,
        ulong nTime, uint nBits, uint nNonce, Span<byte> result)
    {
        Contract.Requires<ArgumentException>(result.Length >= 32);
        Contract.Requires<ArgumentException>(hashPrevBlock.Length == 32);
        Contract.Requires<ArgumentException>(hashMerkleRoot.Length == 32);

        // Create a buffer for the block data (80 bytes)
        Span<byte> blockData = stackalloc byte[80];

        // Copy data according to Stella's GetHashForPoW logic
        MemoryMarshal.Write(blockData.Slice(0, 4), ref nVersion);
        hashPrevBlock.CopyTo(blockData.Slice(4, 32));
        hashMerkleRoot.CopyTo(blockData.Slice(36, 32));

        // Apply Stella's special logic for nTime and nBits placement
        if ((nNonce & 1) == 1 || (nNonce & 0xFFFF) == 0)
        {
            // Legacy mode: nBits at offset 68, nTime at offset 72
            MemoryMarshal.Write(blockData.Slice(68, 4), ref nBits);
            MemoryMarshal.Write(blockData.Slice(72, 8), ref nTime);
        }
        else
        {
            // Normal mode: nTime at offset 68, nBits at offset 76
            MemoryMarshal.Write(blockData.Slice(68, 8), ref nTime);
            MemoryMarshal.Write(blockData.Slice(76, 4), ref nBits);
        }

        // Hash the Block Header without nNonce (using SHA-256 double hash)
        using (var hasher = SHA256.Create())
        {
            hasher.TryComputeHash(blockData, result, out _);
            hasher.TryComputeHash(result, result, out _);
        }
    }
}
