using Miningcore.Native;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Miningcore.Crypto.Hashing.Algorithms;

public unsafe class ZKSnark : IHashAlgorithm
{
    // キャッシュサイズ（実際の要件に応じて調整）
    private const int PROVING_KEY_SIZE = 1024;    // 仮の値
    private const int VERIFICATION_KEY_SIZE = 512; // 仮の値
    private const int PROOF_SIZE = 256;           // 仮の値
    private const int HASH_SIZE = 32;             // 最終ハッシュサイズ

    private static readonly ConcurrentDictionary<int, byte[]> _provingKeys = new();
    private static readonly ConcurrentDictionary<int, byte[]> _verificationKeys = new();

    public void Digest(ReadOnlySpan<byte> data, Span<byte> result, params object[] extra)
    {
        // スレッドIDを使用してキーを管理
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        // キーペアを取得またはキャッシュから生成
        var provingKey = _provingKeys.GetOrAdd(threadId, _ => new byte[PROVING_KEY_SIZE]);
        var verificationKey = _verificationKeys.GetOrAdd(threadId, _ => new byte[VERIFICATION_KEY_SIZE]);
        
        Span<byte> proof = stackalloc byte[PROOF_SIZE];

        // 1. 必要に応じてキーペアを生成
        if (!_provingKeys.ContainsKey(threadId))
        {
            Miningcore.Native.ZKSnark.GenerateKeypair(data, provingKey, verificationKey);
        }

        // 2. 証明を生成
        Miningcore.Native.ZKSnark.GenerateProof(provingKey, data, proof);

        // 3. 証明を検証
        if (!Miningcore.Native.ZKSnark.Verify(verificationKey, proof, data))
        {
            throw new Exception("ZKSnark proof verification failed");
        }

        // 4. 証明からハッシュを生成
        Miningcore.Native.ZKSnark.Hash(proof, result);
    }

    public void Dispose()
    {
        // キャッシュをクリア
        _provingKeys.Clear();
        _verificationKeys.Clear();
    }
}