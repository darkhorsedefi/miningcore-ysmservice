using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static class KotoUtil
{
    public static string CalculateMerkleRoot(string[] merkleBranches, string coinbaseHash)
    {
        byte[] coinbaseHashBytes = HexToBytes(coinbaseHash);
        byte[] hash = coinbaseHashBytes;

        foreach (var branch in merkleBranches)
        {
            byte[] branchBytes = HexToBytes(branch);
            byte[] concatenated;

            if (BitConverter.IsLittleEndian)
            {
                concatenated = ReverseBuffer(hash).Concat(ReverseBuffer(branchBytes)).ToArray();
            }
            else
            {
                concatenated = hash.Concat(branchBytes).ToArray();
            }

            hash = Sha256d(concatenated);
        }

        return BytesToHex(ReverseBuffer(hash));
    }
    // バージョンバイトを取得するメソッド
    public static byte[] GetVersionByte(string addr)
    {
        var decoded = Base58Decode(addr);
        return decoded.Take(1).ToArray();
    }

    // SHA256ハッシュを生成するメソッド
    public static byte[] Sha256(byte[] buffer)
    {
        using (var sha256 = SHA256.Create())
        {
            return sha256.ComputeHash(buffer);
        }
    }

    // ダブルSHA256ハッシュを生成するメソッド
    public static byte[] Sha256d(byte[] buffer)
    {
        return Sha256(Sha256(buffer));
    }

    // バッファを逆順にするメソッド
    public static byte[] ReverseBuffer(byte[] buff)
    {
        Array.Reverse(buff);
        return buff;
    }

    // 16進数文字列を逆順にするメソッド
    public static string ReverseHex(string hex)
    {
        var buffer = HexToBytes(hex);
        var reversedBuffer = ReverseBuffer(buffer);
        return BytesToHex(reversedBuffer);
    }

    // 16進数文字列をバイト配列に変換するメソッド
    public static byte[] HexToBytes(string hex)
    {
        return Enumerable.Range(0, hex.Length / 2)
                         .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                         .ToArray();
    }

    // バイト配列を16進数文字列に変換するメソッド
    public static string BytesToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    // Base58エンコード
    public static string Base58Encode(byte[] input)
    {
        const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var sb = new StringBuilder();
        var value = new System.Numerics.BigInteger(input.Reverse().Concat(new byte[] { 0 }).ToArray());
        while (value > 0)
        {
            var remainder = (int)(value % 58);
            value /= 58;
            sb.Insert(0, ALPHABET[remainder]);
        }
        foreach (var b in input)
        {
            if (b == 0)
                sb.Insert(0, '1');
            else
                break;
        }
        return sb.ToString();
    }

    // Base58デコード
    public static byte[] Base58Decode(string input)
    {
        const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var value = new System.Numerics.BigInteger(0);
        foreach (var c in input)
        {
            var index = ALPHABET.IndexOf(c);
            if (index < 0)
                throw new FormatException($"Invalid Base58 character `{c}`");
            value = value * 58 + index;
        }
        var bytes = value.ToByteArray().Reverse().ToArray();
        var leadingZeroCount = input.TakeWhile(c => c == '1').Count();
        return new byte[leadingZeroCount].Concat(bytes.SkipWhile(b => b == 0)).ToArray();
    }

    // アドレスからexAddressを生成するメソッド
    public static string AddressFromEx(string exAddress, string ripemd160Key)
    {
        try
        {
            var versionByte = GetVersionByte(exAddress);
            var addrBase = versionByte.Concat(HexToBytes(ripemd160Key)).ToArray();
            var checksum = Sha256d(addrBase).Take(4).ToArray();
            var address = addrBase.Concat(checksum).ToArray();
            return Base58Encode(address);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 追加のメソッド
    public static byte[] Uint256BufferFromHash(string hex)
    {
        var fromHex = HexToBytes(hex);

        if (fromHex.Length != 32)
        {
            var empty = new byte[32];
            Array.Fill(empty, (byte)0);
            Array.Copy(fromHex, empty, fromHex.Length);
            fromHex = empty;
        }

        return ReverseBuffer(fromHex);
    }

    public static string HexFromReversedBuffer(byte[] buffer)
    {
        return BytesToHex(ReverseBuffer(buffer));
    }

    public static byte[] VarIntBuffer(long n)
    {
        if (n < 0xfd)
        {
            return new byte[] { (byte)n };
        }
        else if (n < 0xffff)
        {
            var buff = new byte[3];
            buff[0] = 0xfd;
            BitConverter.GetBytes((ushort)n).CopyTo(buff, 1);
            return buff;
        }
        else if (n < 0xffffffff)
        {
            var buff = new byte[5];
            buff[0] = 0xfe;
            BitConverter.GetBytes((uint)n).CopyTo(buff, 1);
            return buff;
        }
        else
        {
            var buff = new byte[9];
            buff[0] = 0xff;
            BitConverter.GetBytes(n).CopyTo(buff, 1);
            return buff;
        }
    }

    public static byte[] VarStringBuffer(string str)
    {
        var strBuff = Encoding.UTF8.GetBytes(str);
        return VarIntBuffer(strBuff.Length).Concat(strBuff).ToArray();
    }

    public static byte[] SerializeNumber(long n)
    {
        if (n >= 1 && n <= 16) return new byte[] { (byte)(0x50 + n) };

        var l = 1;
        var buff = new byte[9];
        while (n > 0x7f)
        {
            buff[l++] = (byte)(n & 0xff);
            n >>= 8;
        }
        buff[0] = (byte)l;
        buff[l++] = (byte)n;
        return buff.Take(l).ToArray();
    }

    public static byte[] SerializeString(string s)
    {
        if (s.Length < 253)
        {
            return new byte[] { (byte)s.Length }.Concat(Encoding.UTF8.GetBytes(s)).ToArray();
        }
        else if (s.Length < 0x10000)
        {
            return new byte[] { 253 }.Concat(BitConverter.GetBytes((ushort)s.Length)).Concat(Encoding.UTF8.GetBytes(s)).ToArray();
        }
        else if ((long)s.Length < (long)0x100000000)
        {
            return new byte[] { 254 }.Concat(BitConverter.GetBytes((uint)s.Length)).Concat(Encoding.UTF8.GetBytes(s)).ToArray();
        }
        else
        {
            return new byte[] { 255 }.Concat(BitConverter.GetBytes(s.Length)).Concat(Encoding.UTF8.GetBytes(s)).ToArray();
        }
    }

    public static byte[] PackUInt16LE(ushort num)
    {
        return BitConverter.GetBytes(num);
    }

    public static byte[] PackInt32LE(int num)
    {
        return BitConverter.GetBytes(num);
    }

    public static byte[] PackInt32BE(int num)
    {
        var buff = BitConverter.GetBytes(num);
        Array.Reverse(buff);
        return buff;
    }

    public static byte[] PackUInt32LE(uint num)
    {
        return BitConverter.GetBytes(num);
    }

    public static byte[] PackUInt32BE(uint num)
    {
        var buff = BitConverter.GetBytes(num);
        Array.Reverse(buff);
        return buff;
    }

    public static byte[] PackInt64LE(long num)
    {
        var buff = new byte[8];
        BitConverter.GetBytes((uint)(num % Math.Pow(2, 32))).CopyTo(buff, 0);
        BitConverter.GetBytes((uint)(num / Math.Pow(2, 32))).CopyTo(buff, 4);
        return buff;
    }

    public static byte[] PubkeyToScript(string key)
    {
        if (key.Length != 66)
        {
            throw new ArgumentException("Invalid pubkey length");
        }

        var pubkey = new byte[35];
        pubkey[0] = 0x21;
        pubkey[34] = 0xac;
        HexToBytes(key).CopyTo(pubkey, 1);
        return pubkey;
    }

    public static byte[] MiningKeyToScript(string key)
    {
        var keyBuffer = HexToBytes(key);
        return new byte[] { 0x76, 0xa9, 0x14 }.Concat(keyBuffer).Concat(new byte[] { 0x88, 0xac }).ToArray();
    }

    public static byte[] AddressToScript(string addr)
    {
        var decoded = Base58Decode(addr);

        if (decoded.Length != 25 && decoded.Length != 26)
        {
            throw new ArgumentException("Invalid address length");
        }

        var pubkey = decoded.Skip(decoded.Length - 24).Take(20).ToArray();

        return new byte[] { 0x76, 0xa9, 0x14 }.Concat(pubkey).Concat(new byte[] { 0x88, 0xac }).ToArray();
    }

    public static string GetReadableHashRateString(double hashrate)
    {
        int i = -1;
        string[] byteUnits = { " KH", " MH", " GH", " TH", " PH" };
        do
        {
            hashrate /= 1024;
            i++;
        } while (hashrate > 1024);
        return hashrate.ToString("F2") + byteUnits[i];
    }

    public static byte[] ShiftMax256Right(int shiftRight)
    {
        var arr256 = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            arr256[i] = 0xff;
        }

        var shiftBytes = shiftRight / 8;
        var shiftBits = shiftRight % 8;

        var shifted = new byte[32];
        for (int i = 0; i < 32 - shiftBytes; i++)
        {
            shifted[i + shiftBytes] = (byte)(arr256[i] >> shiftBits);
            if (i + shiftBytes + 1 < 32)
            {
                shifted[i + shiftBytes + 1] |= (byte)(arr256[i] << (8 - shiftBits));
            }
        }

        return shifted;
    }

    public static byte[] BufferToCompactBits(byte[] startingBuff)
    {
        var bigNum = new System.Numerics.BigInteger(startingBuff);
        var buff = bigNum.ToByteArray();

        if (buff[0] > 0x7f)
        {
            buff = new byte[] { 0x00 }.Concat(buff).ToArray();
        }

        buff = new byte[] { (byte)buff.Length }.Concat(buff).ToArray();
        return buff.Take(4).ToArray();
    }

    public static System.Numerics.BigInteger BignumFromBitsBuffer(byte[] bitsBuff)
    {
        int numBytes = bitsBuff[0];
        var bigBits = new System.Numerics.BigInteger(bitsBuff.Skip(1).ToArray());
        var target = bigBits * System.Numerics.BigInteger.Pow(2, (numBytes - 3) * 8);
        return target;
    }

    public static System.Numerics.BigInteger BignumFromBitsHex(string bitsString)
    {
        var bitsBuff = HexToBytes(bitsString);
        return BignumFromBitsBuffer(bitsBuff);
    }

    public static byte[] ConvertBitsToBuff(byte[] bitsBuff)
    {
        var target = BignumFromBitsBuffer(bitsBuff);
        var resultBuff = target.ToByteArray();
        var buff256 = new byte[32];
        Array.Fill(buff256, (byte)0);
        Array.Copy(resultBuff, 0, buff256, buff256.Length - resultBuff.Length, resultBuff.Length);
        return buff256;
    }

    public static byte[] GetTruncatedDiff(int shift)
    {
        return ConvertBitsToBuff(BufferToCompactBits(ShiftMax256Right(shift)));
    }

    // Kotoの合意パラメータ
    private static dynamic consensusParams = new
    {
        nSubsidySlowStartInterval = 43200,
        nSubsidyHalvingInterval = 1051200,
        SubsidySlowStartShift = new Func<int>(() => consensusParams.nSubsidySlowStartInterval / 2)
    };

    public static void SetupKotoConsensusParams(dynamic options)
    {
        if (options.coin != null)
        {
            if (options.coin.nSubsidySlowStartInterval != null)
            {
                consensusParams.nSubsidySlowStartInterval = options.coin.nSubsidySlowStartInterval;
            }
            if (options.coin.nSubsidyHalvingInterval != null)
            {
                consensusParams.nSubsidyHalvingInterval = options.coin.nSubsidyHalvingInterval;
            }
        }
    }

    public static long GetKotoBlockSubsidy(long nHeight)
    {
        var COIN = new System.Numerics.BigInteger(100000000);
        var nSubsidy = COIN * 100;

        if (nHeight == 1)
        {
            nSubsidy = COIN * 3920000;
            return (long)nSubsidy;
        }

        if (nHeight < consensusParams.nSubsidySlowStartInterval / 2)
        {
            nSubsidy /= consensusParams.nSubsidySlowStartInterval;
            return (long)(nSubsidy * nHeight);
        }
        else if (nHeight < consensusParams.nSubsidySlowStartInterval)
        {
            nSubsidy /= consensusParams.nSubsidySlowStartInterval;
            return (long)(nSubsidy * (nHeight + 1));
        }

        var halvings = (nHeight - consensusParams.SubsidySlowStartShift()) / consensusParams.nSubsidyHalvingInterval;

        if (halvings >= 64)
            return 0;

        nSubsidy = System.Numerics.BigInteger.Divide(nSubsidy, System.Numerics.BigInteger.Pow(2, (int)halvings));
        return (long)nSubsidy;
    }

    public static byte[] GetFounderRewardScript(string addr)
    {
        var decoded = Base58Decode(addr);

        if (decoded.Length != 25 && decoded.Length != 26)
        {
            throw new ArgumentException("Invalid address length");
        }

        var pubkey = decoded.Skip(decoded.Length - 24).Take(20).ToArray();

        return new byte[] { 0xa9, 0x14 }.Concat(pubkey).Concat(new byte[] { 0x87 }).ToArray();
    }

    // その他のユーティリティメソッド
}

