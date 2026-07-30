using Microsoft.EntityFrameworkCore;
using RoyalIdentity.Data.Operational.Entities;

namespace RoyalIdentity.Data.Operational;

/// <summary>
/// Provider-neutral Operational mappings (plan DF1/DF2). Contexts never carry mappings themselves: the
/// default/provider contexts and third-party combined contexts all call a public extension — this neutral one
/// directly, or a provider extension that composes it with provider refinements.
/// <para>
/// Invariants pinned here: every key, unique constraint and index starts with <c>realm_id</c> (plan DF5); no
/// foreign key crosses into the Configuration family, because the two families may live in different databases
/// (plan DF6); the only foreign key at all is the structural ownership of session clients by their session
/// (plan DF35); and <c>artifact_type</c> is part of the protocol-artifact key so a typed store can never see a
/// row of another lifecycle (plan DF36). Collations that pin Ordinal comparison are provider refinements
/// (plan DF10) and land with each provider extension.
/// </para>
/// </summary>
public static class OperationalModelBuilderExtensions
{
    public static ModelBuilder ApplyRoyalIdentityOperationalMappings(
        this ModelBuilder modelBuilder, OperationalModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(options);

        var schema = options.Schema;

        modelBuilder.Entity<ProtocolArtifactEntity>(entity =>
        {
            entity.ToTable("protocol_artifacts", schema);
            entity.HasKey(e => new { e.RealmId, e.ArtifactType, e.LookupDigest });
            entity.Property(e => e.RealmId).HasColumnName("realm_id");
            entity.Property(e => e.ArtifactType).HasColumnName("artifact_type");
            entity.Property(e => e.LookupDigest).HasColumnName("lookup_digest");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.ClientId).HasColumnName("client_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.RedirectUri).HasColumnName("redirect_uri");
            entity.Property(e => e.AccessTokenType).HasColumnName("access_token_type");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(e => e.ConsumedAtUtc).HasColumnName("consumed_at_utc");
            entity.Property(e => e.StateVersion).HasColumnName("state_version");
            entity.Property(e => e.ClaimsMode).HasColumnName("claims_mode");
            entity.Property(e => e.PayloadVersion).HasColumnName("payload_version");
            entity.Property(e => e.ProtectedPayload).HasColumnName("protected_payload");

            // Subject-scoped removals (reference tokens by subject+client, refresh tokens by subject) never
            // scan another artifact type or another realm.
            entity.HasIndex(e => new { e.RealmId, e.ArtifactType, e.SubjectId, e.ClientId })
                .HasDatabaseName("ix_protocol_artifacts_subject");

            // session_id is a logical link (plan DF35): indexed, never a foreign key.
            entity.HasIndex(e => new { e.RealmId, e.SessionId })
                .HasDatabaseName("ix_protocol_artifacts_session");

            // Cleanup predicates (plan DF17): global sweeps start by type, realm-bound sweeps by realm.
            entity.HasIndex(e => new { e.ArtifactType, e.ExpiresAtUtc })
                .HasDatabaseName("ix_protocol_artifacts_expiration");
            entity.HasIndex(e => new { e.ArtifactType, e.ConsumedAtUtc })
                .HasDatabaseName("ix_protocol_artifacts_consumed");
        });

        modelBuilder.Entity<ConsentEntity>(entity =>
        {
            entity.ToTable("consents", schema);
            entity.HasKey(e => new { e.RealmId, e.SubjectId, e.ClientId });
            entity.Property(e => e.RealmId).HasColumnName("realm_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.ClientId).HasColumnName("client_id");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(e => e.PayloadVersion).HasColumnName("payload_version");
            entity.Property(e => e.ProtectedPayload).HasColumnName("protected_payload");
            entity.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("ix_consents_expiration");
        });

        modelBuilder.Entity<UserSessionEntity>(entity =>
        {
            entity.ToTable("user_sessions", schema);
            entity.HasKey(e => new { e.RealmId, e.SessionId });
            entity.Property(e => e.RealmId).HasColumnName("realm_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.SubjectId).HasColumnName("subject_id");
            entity.Property(e => e.AuthenticationMethod).HasColumnName("authentication_method");
            entity.Property(e => e.IdentityProvider).HasColumnName("identity_provider");
            entity.Property(e => e.StartedAtUtc).HasColumnName("started_at_utc");
            entity.Property(e => e.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(e => e.EndedAtUtc).HasColumnName("ended_at_utc");
            entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.HasIndex(e => new { e.RealmId, e.SubjectId, e.IsActive })
                .HasDatabaseName("ix_user_sessions_subject");
            entity.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("ix_user_sessions_expiration");
            entity.HasIndex(e => e.EndedAtUtc).HasDatabaseName("ix_user_sessions_ended");
        });

        modelBuilder.Entity<UserSessionClientEntity>(entity =>
        {
            entity.ToTable("user_session_clients", schema);
            entity.HasKey(e => new { e.RealmId, e.SessionId, e.ClientId });
            entity.Property(e => e.RealmId).HasColumnName("realm_id");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.ClientId).HasColumnName("client_id");
            entity.Property(e => e.FirstSeenAtUtc).HasColumnName("first_seen_at_utc");
            entity.Property(e => e.LastSeenAtUtc).HasColumnName("last_seen_at_utc");

            // The only foreign key of the family (plan DF35): structural ownership within the same realm.
            entity.HasOne<UserSessionEntity>()
                .WithMany()
                .HasForeignKey(e => new { e.RealmId, e.SessionId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthorizeParametersEntity>(entity =>
        {
            entity.ToTable("authorize_parameters", schema);
            entity.HasKey(e => new { e.RealmId, e.HandleDigest });
            entity.Property(e => e.RealmId).HasColumnName("realm_id");
            entity.Property(e => e.HandleDigest).HasColumnName("handle_digest");
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(e => e.PayloadVersion).HasColumnName("payload_version");
            entity.Property(e => e.ProtectedPayload).HasColumnName("protected_payload");
            // Cleanup predicate (plan DF17): the global sweep filters on expiration alone, so that is the
            // leading column. Everything realm-bound — the store's reads/deletes and the purge — is already
            // served by the primary key, which starts with realm_id.
            entity.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("ix_authorize_parameters_expiration");
        });

        modelBuilder.Entity<ReplayHandleEntity>(entity =>
        {
            entity.ToTable("replay_handles", schema);

            // The key is the protection (plan-replay-protection DF13): the second insert of the same identity
            // violates it, and that violation is what answers replay. It is the primary key rather than a
            // separate unique index because there is nothing else to identify a row by, and — like every table
            // in this family — it starts with realm_id (plan DF5).
            entity.HasKey(e => new { e.RealmId, e.Issuer, e.Purpose, e.HandleDigest });
            entity.Property(e => e.RealmId).HasColumnName("realm_id");
            entity.Property(e => e.Issuer).HasColumnName("issuer");
            entity.Property(e => e.Purpose).HasColumnName("purpose");
            entity.Property(e => e.HandleDigest).HasColumnName("handle_digest");
            entity.Property(e => e.ExpiresAtUtc).HasColumnName("expires_at_utc");

            // The only query this table ever serves besides the insert: the cleanup sweep, which filters on
            // expiration alone. Everything realm-bound is already served by the primary key.
            entity.HasIndex(e => e.ExpiresAtUtc).HasDatabaseName("ix_replay_handles_expiration");
        });

        return modelBuilder;
    }
}
