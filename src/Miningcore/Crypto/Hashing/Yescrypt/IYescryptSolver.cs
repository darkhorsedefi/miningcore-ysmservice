namespace Miningcore.Crypto.Hashing.Yescrypt
{
public interface IYescryptSolver
{
bool Verify(string solution);
byte[] Hash(byte[] data);
}
}