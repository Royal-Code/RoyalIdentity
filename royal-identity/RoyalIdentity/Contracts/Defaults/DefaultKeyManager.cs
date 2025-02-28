using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Utils.Caching;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultKeyManager : IKeyManager
{
    private readonly IStorage storage;
    private readonly RealmCaching caching;
    private readonly ILogger logger;

    private static readonly Func<Arguments, Task<IReadOnlyList<string>>> getSigningKeyIds = GetSigningKeyIds;
    private static readonly Func<IReadOnlyList<string>, Arguments, Task<IReadOnlyList<SigningCredentials>>> getSigningCredentials = GetSigningCredentials;

    private static readonly Func<Arguments, Task<IReadOnlyList<string>>> getValidationKeyIds = GetValidationKeyIds;
    private static readonly Func<IReadOnlyList<string>, Arguments, Task<ValidationKeysInfo>> getValidationKeys = GetValidationKeys;

    public DefaultKeyManager(
        IStorage storage,
        RealmCaching caching,
        ILogger<DefaultKeyManager> logger)
    {
        this.storage = storage;
        this.caching = caching;
        this.logger = logger;
    }

    public ValueTask<IReadOnlyList<SigningCredentials>> GetAllSigningCredentialsAsync(Realm realm, CancellationToken ct)
    {
        var cache = caching.GetKeyCache(realm);
        var args = new Arguments(realm, storage, logger, ct);
        return cache.SigningCredentials.GetOrCreateValue(getSigningKeyIds, getSigningCredentials, args);
    }

    public async ValueTask<SigningCredentials?> GetSigningCredentialsAsync(
        Realm realm, 
        ICollection<string> allowedIdentityTokenSigningAlgorithms,
        CancellationToken ct)
    {
        var credentials = await GetAllSigningCredentialsAsync(realm, ct);
        if (credentials.Count is 0)
            return null;

        var credential = allowedIdentityTokenSigningAlgorithms.Count is 0
            ? credentials[0]
            : credentials.FirstOrDefault(x => allowedIdentityTokenSigningAlgorithms.Contains(x.Algorithm));

        return credential;
    }

    public async ValueTask<SigningCredentials?> GetSigningCredentialsAsync(Realm realm, CancellationToken ct)
    {
        var credentials = await GetAllSigningCredentialsAsync(realm, ct);
        var alg = realm.Options.Keys.MainSigningCredentialsAlgorithm;
        var credential = credentials.FirstOrDefault(x => alg == x.Algorithm);
        return credential;
    }

    public ValueTask<ValidationKeysInfo> GetValidationKeysAsync(Realm realm, CancellationToken ct)
    {
        var cache = caching.GetKeyCache(realm);
        var args = new Arguments(realm, storage, logger, ct);
        return cache.ValidationKeys.GetOrCreateValue(getValidationKeyIds, getValidationKeys, args);
    }

    public async Task<SigningCredentials> CreateSigningCredentialsAsync(Realm realm, CancellationToken ct)
    {
        // create new key for SigningCredentials
        var key = KeyParameters.Create(realm.Options.Keys);

        // store the key
        var store = storage.GetKeyStore(realm);
        await store.AddKeyAsync(key, ct);

        // updates the host's cache, other instances will update when the cache expires.
        var cache = caching.GetKeyCache(realm);
        var args = new Arguments(realm, storage, logger, ct);
        await cache.SigningCredentials.Update(GetSigningKeyIds, GetSigningCredentials, args);
        await cache.ValidationKeys.GetOrCreateValue(GetValidationKeyIds, GetValidationKeys, args);

        return key.CreateSigningCredentials();
    }

    private static async Task<IReadOnlyList<string>> GetSigningKeyIds(Arguments args)
    {
        args.Logger.LogDebug("Obtendo nomes dos segredos das chaves de assinatura.");

        // Gets all the secret names of the current keys,
        // which are fit for use on the specified day (today).
        var store = args.Storage.GetKeyStore(args.Realm);
        return await store.ListAllCurrentKeysIdsAsync(ct: args.CancellationToken);
    }

    private static async Task<IReadOnlyList<string>> GetValidationKeyIds(Arguments args)
    {
        args.Logger.LogDebug("Obtendo nomes dos segredos das chaves de validação.");

        // Gets all the secret names of the current and expired keys,
        // just doesn't include future keys.
        var store = args.Storage.GetKeyStore(args.Realm);
        return await store.ListAllKeysIdsAsync(ct: args.CancellationToken);
    }

    private static async Task<IReadOnlyList<SigningCredentials>> GetSigningCredentials(IReadOnlyList<string> keyNames, Arguments args)
    {
        args.Logger.LogDebug("Lendo as chaves de assinatura do banco de dados: {KeyNames}", keyNames);

        var store = args.Storage.GetKeyStore(args.Realm);
        var parameters = await store.GetKeysAsync(keyNames, args.CancellationToken);

        return parameters.Select(x => x.CreateSigningCredentials()).ToList();
    }

    private static async Task<ValidationKeysInfo> GetValidationKeys(IReadOnlyList<string> keyNames, Arguments args)
    {
        args.Logger.LogDebug("Lendo as chaves de validação do banco de dados: {KeyNames}", keyNames);

        var store = args.Storage.GetKeyStore(args.Realm);
        var parameters = await store.GetKeysAsync(keyNames, args.CancellationToken);

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

    private readonly struct Arguments
    {
        public Realm Realm { get; }

        public IStorage Storage { get; }

        public ILogger Logger { get; }

        public CancellationToken CancellationToken { get; }

        public Arguments(Realm realm, IStorage storage, ILogger logger, CancellationToken ct)
        {
            Realm = realm;
            Storage = storage;
            Logger = logger;
            CancellationToken = ct;
        }
    }
}
