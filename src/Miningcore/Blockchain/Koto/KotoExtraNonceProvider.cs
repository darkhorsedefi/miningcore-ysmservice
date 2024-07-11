using System;
using Miningcore.Crypto;
using Miningcore.Extensions;
using NBitcoin;

namespace Miningcore.Blockchain.Koto
{
    public class KotoExtraNonceProvider : ExtraNonceProviderBase
    {
        private readonly int size;

        public KotoExtraNonceProvider(int size)
        {
            this.size = size;
        }

        public int Size => size;
    }
}
