using System;
using Miningcore.Crypto;
using Miningcore.Extensions;
using NBitcoin;

namespace Miningcore.Blockchain.Koto
{
    public class KotoExtraNonceProvider : ExtraNonceProviderBase
    {
        private readonly int size;
        public KotoExtraNonceProvider(string poolId, int initialNonce, byte? version) : base(poolId, initialNonce, version)
        {
        }

        public int Size => extranonceBytes;
    }
}
