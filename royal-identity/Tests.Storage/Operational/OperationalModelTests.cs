using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using RoyalIdentity.Data.Operational;
using RoyalIdentity.Data.Operational.Entities;

namespace Tests.Storage.Operational;

/// <summary>
/// Model metadata of the Operational family (plan-data-operational-storage Fase 1, DF5/DF6/DF17/DF35/DF36).
/// These assertions read the built EF model only — no database, no connection — and pin the invariants the
/// SQLite/PostgreSQL migrations of the later phases must inherit: realm in every key, no cross-family foreign
/// key, exactly one structural foreign key, <c>artifact_type</c> inside the artifact key, and indexes aligned
/// with the cleanup predicates.
/// </summary>
public class OperationalModelTests
{
	private static readonly Type[] EntityTypes =
	[
		typeof(ProtocolArtifactEntity),
		typeof(ConsentEntity),
		typeof(UserSessionEntity),
		typeof(UserSessionClientEntity),
		typeof(AuthorizeParametersEntity),
	];

	private static IModel BuildModel()
	{
		var options = new DbContextOptionsBuilder<OperationalDbContext>()
			.UseSqlite("Data Source=:memory:")
			.Options;
		using var context = new OperationalDbContext(options);
		return context.GetService<IDesignTimeModel>().Model;
	}

	private static IEntityType Entity<TEntity>() => BuildModel().FindEntityType(typeof(TEntity))!;

	private static IReadOnlyList<string> KeyProperties<TEntity>()
		=> [.. Entity<TEntity>().FindPrimaryKey()!.Properties.Select(p => p.Name)];

	private static bool HasIndexOn(IEntityType entityType, params string[] properties)
		=> entityType.GetIndexes().Any(index =>
			index.Properties.Select(p => p.Name).SequenceEqual(properties, StringComparer.Ordinal));

	// DF36: five business tables — one shared, discriminated artifact table plus four of their own.
	[Fact]
	public void Model_MapsExactlyTheFiveOperationalTables()
	{
		var model = BuildModel();

		var mapped = model.GetEntityTypes().Select(t => t.ClrType).ToHashSet();
		var tables = model.GetEntityTypes().Select(t => t.GetTableName()).ToHashSet(StringComparer.Ordinal);

		Assert.Equal(EntityTypes.ToHashSet(), mapped);
		Assert.Equal(
			new HashSet<string?>(
				["protocol_artifacts", "consents", "user_sessions", "user_session_clients", "authorize_parameters"],
				StringComparer.Ordinal),
			tables);
	}

	// DF5: realm is in every primary key, and it comes first so a realm-bound sweep or purge starts on it.
	[Fact]
	public void EveryPrimaryKey_StartsWithTheRealm()
	{
		var model = BuildModel();

		foreach (var entityType in model.GetEntityTypes())
		{
			var key = entityType.FindPrimaryKey()!;

			Assert.Equal(nameof(ProtocolArtifactEntity.RealmId), key.Properties[0].Name);
		}
	}

	// DF36: artifact_type is part of the artifact identity, so a typed store can never address a row of
	// another lifecycle.
	[Fact]
	public void ProtocolArtifacts_AreKeyedByRealmTypeAndDigest()
	{
		Assert.Equal(
			[
				nameof(ProtocolArtifactEntity.RealmId),
				nameof(ProtocolArtifactEntity.ArtifactType),
				nameof(ProtocolArtifactEntity.LookupDigest),
			],
			KeyProperties<ProtocolArtifactEntity>());
	}

	// DF14/CN-01: the consent identity is the real composite key, never a concatenated string.
	[Fact]
	public void Consents_AreKeyedByRealmSubjectAndClient()
	{
		Assert.Equal(
			[
				nameof(ConsentEntity.RealmId),
				nameof(ConsentEntity.SubjectId),
				nameof(ConsentEntity.ClientId),
			],
			KeyProperties<ConsentEntity>());
	}

	[Fact]
	public void UserSessions_AreKeyedByRealmAndSid()
	{
		Assert.Equal(
			[nameof(UserSessionEntity.RealmId), nameof(UserSessionEntity.SessionId)],
			KeyProperties<UserSessionEntity>());
	}

	// DF15: deduplication per client is the key itself, not application logic.
	[Fact]
	public void UserSessionClients_AreKeyedByRealmSessionAndClient()
	{
		Assert.Equal(
			[
				nameof(UserSessionClientEntity.RealmId),
				nameof(UserSessionClientEntity.SessionId),
				nameof(UserSessionClientEntity.ClientId),
			],
			KeyProperties<UserSessionClientEntity>());
	}

	[Fact]
	public void AuthorizeParameters_AreKeyedByRealmAndHandleDigest()
	{
		Assert.Equal(
			[nameof(AuthorizeParametersEntity.RealmId), nameof(AuthorizeParametersEntity.HandleDigest)],
			KeyProperties<AuthorizeParametersEntity>());
	}

	// DF35: the only foreign key of the family is structural ownership of a session's clients, in the same
	// realm, with a shared lifecycle.
	[Fact]
	public void TheOnlyForeignKey_IsSessionClientToSession()
	{
		var model = BuildModel();

		var foreignKeys = model.GetEntityTypes()
			.SelectMany(entityType => entityType.GetForeignKeys())
			.ToList();

		var only = Assert.Single(foreignKeys);
		Assert.Equal(typeof(UserSessionClientEntity), only.DeclaringEntityType.ClrType);
		Assert.Equal(typeof(UserSessionEntity), only.PrincipalEntityType.ClrType);
		Assert.Equal(
			[nameof(UserSessionClientEntity.RealmId), nameof(UserSessionClientEntity.SessionId)],
			only.Properties.Select(p => p.Name));
		Assert.Equal(DeleteBehavior.Cascade, only.DeleteBehavior);
	}

	// DF35: session_id on an artifact is a logical link — indexed, never a relationship.
	[Fact]
	public void ArtifactSessionId_IsIndexedButNotARelationship()
	{
		var artifacts = Entity<ProtocolArtifactEntity>();

		Assert.Empty(artifacts.GetForeignKeys());
		Assert.True(HasIndexOn(
			artifacts,
			nameof(ProtocolArtifactEntity.RealmId),
			nameof(ProtocolArtifactEntity.SessionId)));
	}

	// DF17: the cleanup predicates have indexes; a global sweep starts by artifact type, a realm-bound one by
	// the realm through the primary key.
	[Fact]
	public void CleanupPredicates_AreIndexed()
	{
		var artifacts = Entity<ProtocolArtifactEntity>();

		Assert.True(HasIndexOn(
			artifacts,
			nameof(ProtocolArtifactEntity.ArtifactType),
			nameof(ProtocolArtifactEntity.ExpiresAtUtc)));
		Assert.True(HasIndexOn(
			artifacts,
			nameof(ProtocolArtifactEntity.ArtifactType),
			nameof(ProtocolArtifactEntity.ConsumedAtUtc)));

		Assert.True(HasIndexOn(Entity<ConsentEntity>(), nameof(ConsentEntity.ExpiresAtUtc)));
		Assert.True(HasIndexOn(Entity<UserSessionEntity>(), nameof(UserSessionEntity.ExpiresAtUtc)));
		Assert.True(HasIndexOn(Entity<UserSessionEntity>(), nameof(UserSessionEntity.EndedAtUtc)));
		Assert.True(HasIndexOn(
			Entity<AuthorizeParametersEntity>(),
			nameof(AuthorizeParametersEntity.RealmId),
			nameof(AuthorizeParametersEntity.ExpiresAtUtc)));
	}

	// AT-04/RT-05: subject-scoped removals never scan another artifact type or another realm.
	[Fact]
	public void SubjectScopedRemovals_AreIndexedByRealmAndArtifactType()
	{
		Assert.True(HasIndexOn(
			Entity<ProtocolArtifactEntity>(),
			nameof(ProtocolArtifactEntity.RealmId),
			nameof(ProtocolArtifactEntity.ArtifactType),
			nameof(ProtocolArtifactEntity.SubjectId),
			nameof(ProtocolArtifactEntity.ClientId)));

		Assert.True(HasIndexOn(
			Entity<UserSessionEntity>(),
			nameof(UserSessionEntity.RealmId),
			nameof(UserSessionEntity.SubjectId),
			nameof(UserSessionEntity.IsActive)));
	}

	// DF8/DF17: expiration is persisted data, never a query filter — an expired artifact, consent or session
	// stays readable until cleanup.
	[Fact]
	public void NoEntity_HasAQueryFilter()
	{
		var model = BuildModel();

		foreach (var entityType in model.GetEntityTypes())
			Assert.Empty(entityType.GetDeclaredQueryFilters());
	}

	// DF15/DF36: the session carries no opaque payload — everything about it is queryable.
	[Fact]
	public void UserSessions_HaveNoProtectedPayload()
	{
		var session = Entity<UserSessionEntity>();

		Assert.Null(session.FindProperty("ProtectedPayload"));
		Assert.Null(session.FindProperty("PayloadVersion"));
	}

	// DF36: every discriminator the model knows is declared in one place.
	[Fact]
	public void ArtifactTypes_CoverTheThreeSharedLifecycles()
	{
		Assert.Equal(
			new HashSet<string>(["access_token", "refresh_token", "authorization_code"], StringComparer.Ordinal),
			ProtocolArtifactTypes.All.ToHashSet(StringComparer.Ordinal));
	}
}
