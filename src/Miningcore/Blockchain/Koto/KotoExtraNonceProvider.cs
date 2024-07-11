using System;
using Miningcore.Crypto;
using Miningcore.Extensions;
using NBitcoin;

namespace Miningcore.Blockchain.Koto
{
    public class KotoExtraNonceProvider : ExtraNonceProviderBase
    {
        private readonly int size;
        public KotoExtraNonceProvider(string poolId, byte? clusterInstanceId) : base(poolId, 3, clusterInstanceId)
        {
        }

        public int Size => extranonceBytes;
    }
}
