using System;
using Miningcore.Crypto;
using Miningcore.Extensions;
using NBitcoin;

namespace Miningcore.Blockchain.Koto
{
    public class KotoExtraNonceProvider : IExtraNonceProvider
    {
        private int counter = -1;
        private readonly int size;

        public KotoExtraNonceProvider(int size)
        {
            this.size = size;
        }

        public string Next()
        {
            var value = Interlocked.Increment(ref counter);

            var extraNonce = new byte[size];
            BitConverter.GetBytes(value).CopyTo(extraNonce, 0);

            return extraNonce.ToHexString();
        }

        public int Size => size;
    }
}
