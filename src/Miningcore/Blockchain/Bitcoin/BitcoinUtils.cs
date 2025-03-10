using System.Diagnostics;
using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Miningcore.Blockchain.Bitcoin;

public static class BitcoinUtils
{
    /// <summary>
    /// Bitcoin addresses are implemented using the Base58Check encoding of the hash of either:
    /// Pay-to-script-hash(p2sh): payload is: RIPEMD160(SHA256(redeemScript)) where redeemScript is a
    /// script the wallet knows how to spend; version byte = 0x05 (these addresses begin with the digit '3')
    /// Pay-to-pubkey-hash(p2pkh): payload is RIPEMD160(SHA256(ECDSA_publicKey)) where
    /// ECDSA_publicKey is a public key the wallet knows the private key for; version byte = 0x00
    /// (these addresses begin with the digit '1')
    /// The resulting hash in both of these cases is always exactly 20 bytes.
    /// </summary>
    public static IDestination AddressToDestination(string address, Network expectedNetwork)
    {
        var decoded = Encoders.Base58Check.DecodeData(address);
        var networkVersionBytes = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true);
        decoded = decoded.Skip(networkVersionBytes.Length).ToArray();
        var result = new KeyId(decoded);

        return result;
    }

    public static IDestination BechSegwitAddressToDestination(string address, Network expectedNetwork)
    {
        var encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);
        var decoded = encoder.Decode(address, out var witVersion);
        var result = new WitKeyId(decoded);

        Debug.Assert(result.GetAddress(expectedNetwork).ToString() == address);
        return result;
    }

    public static IDestination BCashAddressToDestination(string address, Network expectedNetwork)
    {
        var bcash = NBitcoin.Altcoins.BCash.Instance.GetNetwork(expectedNetwork.ChainName);
        var trashAddress = bcash.Parse<NBitcoin.Altcoins.BCash.BTrashPubKeyAddress>(address);
        return trashAddress.ScriptPubKey.GetDestinationAddress(bcash);
    }

    public static IDestination LitecoinAddressToDestination(string address, Network expectedNetwork)
    {
        var litecoin = NBitcoin.Altcoins.Litecoin.Instance.GetNetwork(expectedNetwork.ChainName);
        var encoder = litecoin.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);

        var decoded = encoder.Decode(address, out var witVersion);
        var result = new WitKeyId(decoded);

        Debug.Assert(result.GetAddress(litecoin).ToString() == address);
        return result;
    }

    public static IDestination DecredAddressToDestination(string address, Network expectedNetwork)
    {
        var decoded = Base58CheckDecode(address);
        var networkVersionBytes = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true);
        var netID = new byte[] { decoded[0], decoded[1] };
        var payload = decoded.Skip(2).ToArray();

        switch (BitConverter.ToUInt16(netID, 0))
        {
            case 0x073f: // MainNet PubKeyHashAddrID
                return new KeyId(payload);
            case 0x071a: // MainNet ScriptHashAddrID
                return new ScriptId(payload);
            default:
                throw new FormatException("Unknown address type");
        }
    }

        public static byte[] Base58CheckDecode(string input)
    {
        var bytes = Base58.Decode(input);
        if (bytes.Length < 4)
        {
            throw new FormatException("Invalid Base58Check string");
        }

        var data = bytes.Take(bytes.Length - 4).ToArray();
        var checksum = bytes.Skip(bytes.Length - 4).ToArray();

        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(sha256.ComputeHash(data));
            if (!checksum.SequenceEqual(hash.Take(4)))
            {
                throw new FormatException("Invalid checksum" + checksum + " != " + hash.Take(4));
            }
        }

        return data;
    }

}

public static class Base58
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly int[] Indexes = new int[128];

    static Base58()
    {
        for (int i = 0; i < Indexes.Length; i++)
        {
            Indexes[i] = -1;
        }
        for (int i = 0; i < Alphabet.Length; i++)
        {
            Indexes[Alphabet[i]] = i;
        }
    }

    public static byte[] Decode(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new byte[0];
        }

        var input58 = new int[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            var digit = c < 128 ? Indexes[c] : -1;
            if (digit < 0)
            {
                throw new FormatException($"Invalid Base58 character '{c}' at position {i}");
            }
            input58[i] = digit;
        }

        int leadingZeroes = input.TakeWhile(c => c == '1').Count();
        var decoded = new byte[input.Length];
        int j = decoded.Length;

        foreach (var t in input58)
        {
            int carry = t;
            for (int k = decoded.Length - 1; k >= 0; k--)
            {
                carry += 58 * decoded[k];
                decoded[k] = (byte)(carry % 256);
                carry /= 256;
            }
        }

        while (j < decoded.Length && decoded[j] == 0)
        {
            j++;
        }

        var result = new byte[decoded.Length - j + leadingZeroes];
        Array.Copy(decoded, j, result, leadingZeroes, result.Length - leadingZeroes);
        return result;
    }
}
