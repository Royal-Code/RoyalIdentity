using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Options;
using RoyalIdentity.Utils.Caching;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultKeyManager : IKeyManager
{
    private readonly IKeyStore store;
    private readonly ServerOptions options;
    private readonly KeyCache cache;
    private readonly ILogger logger;

    private Func<CancellationToken, Task<IReadOnlyList<string>>>? getSigningKeyIds;
    private Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<SigningCredentials>>>? getSigningCredentials;

    private Func<CancellationToken, Task<IReadOnlyList<string>>>? getValidationKeyIds;
    private Func<IReadOnlyList<string>, CancellationToken, Task<ValidationKeysInfo>>? getValidationKeys;

    public DefaultKeyManager(
        IKeyStore store,
        IOptions<ServerOptions> options,
        KeyCache cache,
        ILogger<DefaultKeyManager> logger)
    {
        this.store = store;
        this.options = options.Value;
        this.cache = cache;
        this.logger = logger;
    }

    public ValueTask<IReadOnlyList<SigningCredentials>> GetAllSigningCredentialsAsync(CancellationToken ct)
    {
        getSigningKeyIds ??= GetSigningKeyIds;
        getSigningCredentials ??= GetSigningCredentials;
        return cache.SigningCredentials.GetOrCreateValue(getSigningKeyIds, getSigningCredentials, ct);
    }

    public async ValueTask<SigningCredentials?> GetSigningCredentialsAsync(
        ICollection<string> allowedIdentityTokenSigningAlgorithms, CancellationToken ct)
    {
        var credentials = await GetAllSigningCredentialsAsync(ct);
        if (credentials.Count is 0)
            return null;

        var credential = allowedIdentityTokenSigningAlgorithms.Count is 0
            ? credentials[0]
            : credentials.FirstOrDefault(x => allowedIdentityTokenSigningAlgorithms.Contains(x.Algorithm));

        return credential;
    }

    public async ValueTask<SigningCredentials?> GetSigningCredentialsAsync(CancellationToken ct)
    {
        var credentials = await GetAllSigningCredentialsAsync(ct);
        var alg = options.Keys.MainSigningCredentialsAlgorithm;
        var credential = credentials.FirstOrDefault(x => alg == x.Algorithm);
        return credential;
    }

    public ValueTask<ValidationKeysInfo> GetValidationKeysAsync(CancellationToken ct)
    {
        getValidationKeyIds ??= GetValidationKeyIds;
        getValidationKeys ??= GetValidationKeys;
        return cache.ValidationKeys.GetOrCreateValue(getValidationKeyIds, getValidationKeys, ct);
    }

    public async Task<SigningCredentials> CreateSigningCredentialsAsync(CancellationToken ct)
    {
        // create new key for SigningCredentials
        var alg = options.Keys.MainSigningCredentialsAlgorithm;
        var lifetime = options.Keys.DefaultSigningCredentialsLifetime;
        var keySize = options.Keys.RsaKeySizeInBytes;
        var key = KeyParameters.Create(alg, lifetime, keySize);

        // store the key
        await store.AddKeyAsync(key, ct);

        // updates the host's cache, other instances will update when the cache expires.
        await cache.SigningCredentials.Update(GetSigningKeyIds, GetSigningCredentials, ct);
        await cache.ValidationKeys.GetOrCreateValue(GetValidationKeyIds, GetValidationKeys, ct);

        return key.CreateSigningCredentials();
    }

    private async Task<IReadOnlyList<string>> GetSigningKeyIds(CancellationToken ct)
    {
        logger.LogDebug("Obtendo nomes dos segredos das chaves de assinatura.");

        // Gets all the secret names of the current keys,
        // which are fit for use on the specified day (today).
        return await store.ListAllCurrentKeysIdsAsync(ct: ct);
    }

    private async Task<IReadOnlyList<string>> GetValidationKeyIds(CancellationToken ct)
    {
        logger.LogDebug("Obtendo nomes dos segredos das chaves de validação.");

        // Gets all the secret names of the current and expired keys,
        // just doesn't include future keys.
        return await store.ListAllKeysIdsAsync(ct: ct);
    }

    private async Task<IReadOnlyList<SigningCredentials>> GetSigningCredentials(IReadOnlyList<string> keyNames, CancellationToken ct)
    {
        logger.LogDebug("Lendo as chaves de assinatura do banco de dados: {KeyNames}", keyNames);

        var parameters = await store.GetKeysAsync(keyNames, ct);

        return parameters.Select(x => x.CreateSigningCredentials()).ToList();
    }

    private async Task<ValidationKeysInfo> GetValidationKeys(IReadOnlyList<string> keyNames, CancellationToken ct)
    {
        logger.LogDebug("Lendo as chaves de validação do banco de dados: {KeyNames}", keyNames);

        var parameters = await store.GetKeysAsync(keyNames, ct);

        var lists = parameters
            .Select(s => s.GetValidationKey())
            .Aggregate((new List<SecurityKey>(), new List<JsonWebKey>()), (lists, pair) =>
            {
                lists.Item1.Add(pair.Item1);
                if (pair.Item2 is not null)
                    lists.Item2.Add(pair.Item2);
                return lists;
            });

        return new ValidationKeysInfo
        {
            Keys = lists.Item1,
            Jwks = lists.Item2
        };
    }
}
