using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Storage.EntityFramework.Operational;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;
using RoyalIdentity.Storage.EntityFramework.Storage;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Storage.EntityFramework.Extensions;

public static class OperationalServiceCollectionExtensions
{
    /// <summary>
    /// <para>
    ///     Registers the scoped Operational context seam and the stateless materialization services of the EF
    ///     adapter over <typeparamref name="TContext"/> (plan DF1/DF2). The context itself is registered by the
    ///     consumer, exactly as for Configuration, and may be a different context — with a different
    ///     connection, even a different database — or one combined context applying both mapping extensions.
    /// </para>
    /// <para>
    ///     This registration never provides <c>IStorage</c>, <c>IStorageProvider</c> or
    ///     <c>IStorageSession</c> — the complete gateway is an explicit opt-in composition of both families
    ///     (plan DF21) — and never applies migrations (plan DF23). Payload protection profiles are registered
    ///     separately and deliberately: no profile is implied here, so a realm selecting an unregistered
    ///     profile fails closed instead of silently writing unprotected data (plan DF30).
    /// </para>
    /// </summary>
    public static IServiceCollection AddEntityFrameworkOperationalStorage<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OperationalLookupDigest>();
        services.TryAddSingleton<AccessTokenPayloadSerializer>();
        services.TryAddSingleton<RefreshTokenPayloadSerializer>();
        services.TryAddSingleton<AuthorizationCodePayloadSerializer>();
        services.TryAddSingleton<ConsentPayloadSerializer>();
        services.TryAddSingleton<AuthorizeParametersPayloadSerializer>();
        services.TryAddSingleton<OperationalPayloadProtectorResolver>();
        services.TryAddSingleton<OperationalPayloadProtection>();
        services.TryAddSingleton<IAuthorizeParametersHandleGenerator, CryptoRandomAuthorizeParametersHandleGenerator>();
        services.TryAddScoped<IOperationalDbContextAccessor, OperationalDbContextAccessor<TContext>>();
        services.TryAddScoped<IOperationalStoreFactory, EntityFrameworkOperationalStoreFactory>();

        return services;
    }

    /// <summary>
    /// <para>
    ///     Configures the Operational maintenance and, in <see cref="CleanupExecutionMode.Hosted"/>, registers
    ///     the worker that drives it. In <see cref="CleanupExecutionMode.External"/> no worker is registered and
    ///     the same <see cref="IOperationalMaintenance"/> is left for a command or job — so the two schedulers
    ///     can never both be running (plan DF17).
    /// </para>
    /// <para>
    ///     There is no default mode: omitting the choice fails here, and so does selecting it twice — a second
    ///     call would leave the scheduler of the first registration running under the options of the second.
    ///     Invalid options fail here rather than at the first pass, and the <b>effective</b> options are
    ///     validated again when they are resolved, so a later <c>Configure</c> cannot slip past this.
    /// </para>
    /// </summary>
    public static IServiceCollection AddEntityFrameworkOperationalCleanup(
        this IServiceCollection services, Action<OperationalCleanupOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(OperationalCleanupRegistration)))
        {
            throw new InvalidOperationException(
                "The operational cleanup execution mode was already selected by this composition. " +
                "Selecting it twice is ambiguous: the scheduler of the first call would keep running under " +
                "the options of the second.");
        }

        var options = new OperationalCleanupOptions();
        configure(options);

        var errors = options.Validate();
        if (errors.Count is not 0)
            throw new InvalidOperationException($"Invalid operational cleanup options: {string.Join(" ", errors)}");

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IOperationalMaintenance, EntityFrameworkOperationalMaintenance>();
        services.AddOptions<OperationalCleanupOptions>();
        services.AddSingleton(new OperationalCleanupRegistration(options.Mode));
        services.Configure(configure);
        services.AddSingleton<IValidateOptions<OperationalCleanupOptions>>(
            new OperationalCleanupOptionsValidator(options.Mode));

        if (options.Mode is CleanupExecutionMode.Hosted)
            services.AddHostedService<OperationalCleanupWorker>();

        return services;
    }

    /// <summary>
    /// <para>
    ///     Composes the complete production gateway — <c>IStorage</c>, <c>IStorageProvider</c> and
    ///     <c>IStorageSession</c> — over Configuration EF plus Operational EF (plan DF21). It is an explicit
    ///     opt-in: the default host stays in-memory until Plano 4.
    /// </para>
    /// <para>
    ///     Both families must already be registered. The Operational family is the one that carries the atomic
    ///     capabilities MP-2/MP-3, which its store contracts guarantee at compile time (plan DF46) — so a
    ///     composition built this way can never reach the transitional fallback.
    /// </para>
    /// <para>
    ///     A cleanup execution mode must have been selected first (plan DF17). Persisting operational data with
    ///     no scheduler and no external job is not a default worth allowing: the data would grow forever, and
    ///     nothing would say so.
    /// </para>
    /// </summary>
    public static IServiceCollection AddEntityFrameworkStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(OperationalCleanupRegistration)))
        {
            throw new InvalidOperationException(
                "The complete EF gateway requires an explicit operational cleanup execution mode. Call " +
                $"{nameof(AddEntityFrameworkOperationalCleanup)} choosing {nameof(CleanupExecutionMode.Hosted)} " +
                $"or {nameof(CleanupExecutionMode.External)} before {nameof(AddEntityFrameworkStorage)}.");
        }

        services.TryAddScoped<IStorage, EntityFrameworkStorage>();
        services.TryAddSingleton<IStorageProvider, EntityFrameworkStorageProvider>();

        return services;
    }

    /// <summary>
    /// Registers an AES-GCM operational payload protection profile under <paramref name="profileId"/>. The key
    /// belongs to the composition (KMS, vault, or another secure source) and never enters the Configuration
    /// payload — realms only ever store the profile id (plan DF30).
    /// </summary>
    public static IServiceCollection AddOperationalAesGcmPayloadProtection(
        this IServiceCollection services, string profileId, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(key);

        services.AddSingleton<IOperationalPayloadProtector>(
            _ => new AesGcmOperationalPayloadProtector(profileId, key));

        return services;
    }

    /// <summary>
    /// Registers an ASP.NET Core Data Protection operational payload profile under
    /// <paramref name="profileId"/>. Production compositions must configure a persistent, shared key ring when
    /// more than one instance reads the same operational database.
    /// </summary>
    public static IServiceCollection AddOperationalDataProtectionPayloadProtection(
        this IServiceCollection services, string profileId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        services.AddSingleton<IOperationalPayloadProtector>(provider =>
            new DataProtectionOperationalPayloadProtector(
                profileId, provider.GetRequiredService<IDataProtectionProvider>()));

        return services;
    }

    /// <summary>
    /// Registers an unprotected operational payload profile under <paramref name="profileId"/>. It is never a
    /// fallback: registering it here and selecting its id on a realm are two independent, deliberate opt-ins,
    /// and it logs a warning when constructed (plan DF30).
    /// </summary>
    public static IServiceCollection AddOperationalPlainPayloadProtection(
        this IServiceCollection services, string profileId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        services.AddSingleton<IOperationalPayloadProtector>(provider =>
            new PlainOperationalPayloadProtector(
                profileId, provider.GetRequiredService<ILogger<PlainOperationalPayloadProtector>>()));

        return services;
    }
}
