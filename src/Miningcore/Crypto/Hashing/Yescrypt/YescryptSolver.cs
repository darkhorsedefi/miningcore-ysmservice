using System;
using Miningcore.Native;
using Miningcore.Extensions;
using Miningcore.Crypto.Hashing.Algorithms;

namespace Miningcore.Crypto.Hashing.Yescrypt
{
    public unsafe class YescryptSolver : IYescryptSolver
    {
        private readonly YescryptR16 algorithm;

        public YescryptSolver(int N, int r, string personalization)
        {
            algorithm = new YescryptR16(); // Always use R16 for Koto
        }

        public bool Verify(string solution)
        {
            if (string.IsNullOrEmpty(solution))
                return false;

            try
            {
                var solutionBytes = solution.HexToByteArray();
                var output = new byte[32];
                
                algorithm.Digest(solutionBytes, output);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] Hash(byte[] data)
        {
            var result = new byte[32];
            algorithm.Digest(data, result);
            return result;
        }
    }
}
