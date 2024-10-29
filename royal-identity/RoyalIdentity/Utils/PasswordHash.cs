using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace RoyalIdentity.Utils;

public static class PasswordHash
{
    private const int Iterations = 100_000; // Padrão recomendado de iterações
    private const int SaltSize = 16; // Tamanho do salt em bytes
    private const int HashSize = 32; // Tamanho do hash em bytes
    private const string HashPrefix = "$PBKDF2$.";

    public static string Create(string password)
    {
        // Gerar salt criptograficamente seguro
        var salt = new byte[SaltSize];
        CryptoRandom.CreateRandomKey(salt);

        // Derivar a chave (hash) usando KeyDerivation.Pbkdf2
        var hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256, // Função PRF baseada em HMAC-SHA256
            iterationCount: Iterations,
            numBytesRequested: HashSize);

        // Codificar o salt e o hash como base64 para facilitar o armazenamento
        return $"{HashPrefix}{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string hash)
    {
        // Verificar se o hash está no formato esperado
        if (!hash.StartsWith(HashPrefix))
        {
            throw new ArgumentException("Invalid hash format.");
        }

        // Extrair salt e hash do formato armazenado
        var parts = hash[HashPrefix.Length..].Split('.');
        if (parts.Length is not 2)
        {
            throw new ArgumentException("Invalid hash format.");
        }

        var salt = Convert.FromBase64String(parts[0]);
        var storedHash = Convert.FromBase64String(parts[1]);

        // Derivar a chave com base na senha fornecida
        var computedHash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashSize
        );

        // Comparar o hash computado com o armazenado em tempo constante para evitar ataques de timing
        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }
}