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
        const int DECRED_VERSION_BYTES = 2;
        const int DECRED_P2PKH_VERSION = 0x073f;  // Ds prefix
        const int DECRED_P2SH_VERSION = 0x071a;   // Dc prefix
        const int CHECKSUM_LENGTH = 4;

        // First decode from base58 string
        var base58Decoded = Encoders.Base58.DecodeData(address);
        
        // Check minimum length (2 version + 20 hash + 4 checksum)
        if (base58Decoded.Length < DECRED_VERSION_BYTES + 20 + CHECKSUM_LENGTH)
            throw new ArgumentException("Invalid address length", nameof(address));

        // Verify checksum
        var withoutChecksum = base58Decoded.Take(base58Decoded.Length - CHECKSUM_LENGTH).ToArray();
        var checksum = base58Decoded.Skip(base58Decoded.Length - CHECKSUM_LENGTH).ToArray();
        var hash = NBitcoin.Crypto.Hashes.SHA256(withoutChecksum);
        var expectedChecksum = new byte[CHECKSUM_LENGTH];
        Buffer.BlockCopy(hash, 0, expectedChecksum, 0, CHECKSUM_LENGTH);

        if (!checksum.SequenceEqual(expectedChecksum))
            throw new ArgumentException("Invalid checksum", nameof(address));

        // Get version and payload
        var version = (base58Decoded[0] << 8) | base58Decoded[1];
        var addressBytes = base58Decoded.Skip(DECRED_VERSION_BYTES).Take(20).ToArray();

        // Handle based on version
        switch(version)
        {
            case DECRED_P2PKH_VERSION:
                return new KeyId(addressBytes);
            
            case DECRED_P2SH_VERSION:
                return new ScriptId(addressBytes);
                
            default:
                throw new ArgumentException("Invalid Decred address version", nameof(address));
        }
    }

}
