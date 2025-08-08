using System.Diagnostics;
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
    /// 
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

    public static IDestination RincoinAddressToDestination(string address, Network expectedNetwork)
    {
        if(address.StartsWith("rin1", StringComparison.OrdinalIgnoreCase))
        {
            // Rincoin Bech32 address
            var encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);
            var decoded = encoder.Decode(address, out var witVersion);
            var result = new WitKeyId(decoded);

            Debug.Assert(result.GetAddress(expectedNetwork).ToString() == address);
            return result;
        }
        else
        {
            // Rincoin legacy address
            var legacy = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true);
            var decoded = Encoders.Base58.DecodeData(address);
            decoded = decoded.Skip(legacy.Length).ToArray();
            var result = new KeyId(decoded);

            Debug.Assert(result.GetAddress(expectedNetwork).ToString() == address);
            return result;
        }
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
        try
        {
            // Decred: Custom base58 decoding without checksum validation
            var data = Encoders.Base58.DecodeData(address);
            
            // Verify minimum length (2 version bytes + 20 bytes hash160)
            if(data.Length < 22)
                throw new FormatException("Invalid Decred address (too short)");

            // Extract the actual hash160 (skip 2 version bytes)
            var hash160 = new byte[20];
            Array.Copy(data, 2, hash160, 0, 20);
            
            return new KeyId(hash160);
        }
        catch(Exception)
        {
            throw new FormatException("Invalid Decred address format");
        }
    }
}
