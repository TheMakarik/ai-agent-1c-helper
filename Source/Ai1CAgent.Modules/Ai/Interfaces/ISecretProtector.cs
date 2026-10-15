namespace Ai1CAgent.Modules.Ai.Interfaces;

public interface ISecretProtector
{
    string Protect(string plaintext);

    bool TryUnprotect(string protectedValue, out string plaintext);
}
