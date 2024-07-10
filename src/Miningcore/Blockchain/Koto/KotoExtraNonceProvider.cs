using System;
using MiningCore.Crypto;
using MiningCore.Extensions;
using NBitcoin;

namespace MiningCore.Blockchain.Koto
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
