using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

public class Merkletree
{
    private List<byte[]> data;
    private List<byte[]> steps;

    public Merkletree(List<byte[]> data)
    {
        this.data = data;
        this.steps = CalculateSteps(data);
    }

    private List<byte[]> CalculateSteps(List<byte[]> data)
    {
        var L = new List<byte[]>(data);
        var steps = new List<byte[]>();
        var PreL = new List<byte[]> { null };
        var StartL = 2;
        var Ll = L.Count;

        if (Ll > 1)
        {
            while (true)
            {
                if (Ll == 1)
                    break;

                steps.Add(L[1]);

                if (Ll % 2 != 0)
                    L.Add(L[L.Count - 1]);

                var Ld = new List<byte[]>();
                for (int i = StartL; i < Ll; i += 2)
                {
                    Ld.Add(MerkleJoin(L[i], L[i + 1]));
                }

                L = PreL.Concat(Ld).ToList();
                Ll = L.Count;
            }
        }
        return steps;
    }

    private byte[] MerkleJoin(byte[] h1, byte[] h2)
    {
        using (var sha256 = SHA256.Create())
        {
            var joined = h1.Concat(h2).ToArray();
            var dhashed = sha256.ComputeHash(sha256.ComputeHash(joined));
            return dhashed;
        }
    }

    public List<string> GetStepsAsHex()
    {
        return steps.Select(step => BitConverter.ToString(step).Replace("-", "").ToLower()).ToList();
    }

    public byte[] WithFirst(byte[] first)
    {
        foreach (var step in steps)
        {
            using (var sha256 = SHA256.Create())
            {
                first = sha256.ComputeHash(sha256.ComputeHash(first.Concat(step).ToArray()));
            }
        }
        return first;
    }
}
