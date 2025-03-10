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
        // Decode base58 string
        var decoded = Base58.Decode(input);

        // Check minimum length (version + payload + checksum)
        if (decoded.Length < 6)
            throw new FormatException("Invalid Base58Check string");

        // Split decoded data
        var data = decoded.AsSpan();
        var checksum = data.Slice(data.Length - 4, 4);
        var payload = data.Slice(0, data.Length - 4);

        // Calculate checksum
        using var sha256 = SHA256.Create();
        var hash1 = sha256.ComputeHash(payload.ToArray());
        var hash2 = sha256.ComputeHash(hash1);
        
        // Verify checksum
        for(int i = 0; i < 4; i++)
        {
            if(checksum[i] != hash2[i])
                throw new FormatException("Invalid checksum");
        }

        return payload.ToArray();
    }
}

public static class Base58
{
    private const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly int[] INDEXES = BuildIndexes();

    private static int[] BuildIndexes()
    {
        var indexes = new int[128];
        for(int i = 0; i < indexes.Length; i++)
            indexes[i] = -1;
        
        for(int i = 0; i < ALPHABET.Length; i++)
            indexes[ALPHABET[i]] = i;
            
        return indexes;
    }

    public static byte[] Decode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<byte>();

        // Convert input to indexes
        int[] indexes = new int[input.Length];
        for(int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if(c >= 128 || (indexes[i] = INDEXES[c]) == -1)
                throw new FormatException($"Invalid Base58 character '{c}' at position {i}");
        }

        // Count leading zeros
        int leadingZeros = 0;
        while(leadingZeros < input.Length && input[leadingZeros] == '1')
            leadingZeros++;

        // Decode
        byte[] result = new byte[input.Length];
        int length = 0;

        for(int i = leadingZeros; i < input.Length; i++)
        {
            int carry = indexes[i];
            
            // Apply "b256 = b58 * 256"
            for(int j = 0; j < length; j++)
            {
                carry += result[j] * 58;
                result[j] = (byte)(carry & 0xff);
                carry >>= 8;
            }

            while(carry > 0)
            {
                result[length++] = (byte)(carry & 0xff);
                carry >>= 8;
            }
        }

        // Copy result
        var decoded = new byte[leadingZeros + length];
        Array.Copy(result, 0, decoded, leadingZeros, length);
        Array.Reverse(decoded, leadingZeros, length);

        return decoded;
    }
}
