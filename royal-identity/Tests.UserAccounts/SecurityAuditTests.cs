using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalCode.DomainEvents;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Infrastructure.Audit;
using RoyalIdentity.UserAccounts.Infrastructure.Events;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.UserAccounts.Sqlite;

namespace Tests.UserAccounts;

/// <summary>
/// Fase 9 (plan-users-security-lifecycle.md) — domain events are dispatched <b>post-commit</b> by the module
/// DbContext, and the <see cref="SecurityAuditObserver"/> turns the security ones into <see cref="SecurityAuditEntry"/>
/// records gated by the realm's enabled categories (Q8). Entries never carry secrets.
/// </summary>
public class SecurityAuditTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DomainEvents_AreDispatched_PostCommit_ToObservers()
    {
        var recording = new RecordingObserver();
        await using var provider = BuildProvider(observer: recording);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);
        await ChangePasswordAsync(provider, "r1", "alice", options, "secret", "renewed");

        // The created event and the password-changed event were both dispatched after their commits.
        Assert.Contains(recording.Events, e => e is RoyalIdentity.UserAccounts.Features.Accounts.Domain.UserAccountCreated);
        Assert.Contains(recording.Events, e => e is RoyalIdentity.UserAccounts.Features.Accounts.Domain.UserAccountPasswordChanged);
    }

    [Fact]
    public async Task PasswordChange_IsAudited_AsCredential()
    {
        var sink = new RecordingSink();
        await using var provider = BuildProvider(sink: sink);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);
        // The initial password set on create is itself a Credential operation; isolate the change with a delta.
        var before = sink.Entries.Count(e => e.Category == SecurityAuditCategories.Credential);

        await ChangePasswordAsync(provider, "r1", "alice", options, "secret", "renewed");

        var credential = sink.Entries.Where(e => e.Category == SecurityAuditCategories.Credential).ToList();
        Assert.Equal(before + 1, credential.Count);
        var entry = credential[^1];
        Assert.Equal("UserAccountPasswordChanged", entry.EventType);
        Assert.Equal("r1", entry.RealmId);
        Assert.Equal("alice", entry.SubjectId);
    }

    [Fact]
    public async Task AdminBlock_IsAudited_AsAdminSecurity()
    {
        var sink = new RecordingSink();
        await using var provider = BuildProvider(sink: sink);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);
        await BlockAsync(provider, "r1", "alice");

        Assert.Single(sink.Entries, e =>
            e.EventType == "UserAccountBlocked" && e.Category == SecurityAuditCategories.AdminSecurity);
    }

    [Fact]
    public async Task Audit_RespectsRealmCategories_SuppressingDisabledOnes()
    {
        var sink = new RecordingSink();
        // Only AdminSecurity enabled: a password change (Credential) is not audited; a block (AdminSecurity) is.
        var policy = new StubPolicyProvider(SecurityAuditCategories.AdminSecurity);
        await using var provider = BuildProvider(sink: sink, policy: policy);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);
        await ChangePasswordAsync(provider, "r1", "alice", options, "secret", "renewed");
        await BlockAsync(provider, "r1", "alice");

        Assert.DoesNotContain(sink.Entries, e => e.Category == SecurityAuditCategories.Credential);
        Assert.Contains(sink.Entries, e => e.Category == SecurityAuditCategories.AdminSecurity);
    }

    [Fact]
    public async Task PasswordReset_IsAudited_AsRecovery_NotCredential()
    {
        var sink = new RecordingSink();
        await using var provider = BuildProvider(sink: sink);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);
        var before = sink.Entries.Count;

        // A recovery reset routes by PasswordChangeReason.Reset to the Recovery category (P1).
        await MutateAsync(provider, "r1", "alice",
            a => a.ResetPassword("hashed:renewed", options.PasswordOptions, Start));

        var added = sink.Entries.Skip(before).ToList();
        Assert.Single(added, e =>
            e.EventType == "UserAccountPasswordChanged" && e.Category == SecurityAuditCategories.Recovery);
        Assert.DoesNotContain(added, e => e.Category == SecurityAuditCategories.Credential);
    }

    [Fact]
    public async Task RoleGrant_IsAudited_AsAdminSecurity()
    {
        var sink = new RecordingSink();
        await using var provider = BuildProvider(sink: sink);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);

        await MutateAsync(provider, "r1", "alice",
            a => a.AddRole(new UserAccountRole("r1", "admin", "ADMIN"), Start));

        Assert.Single(sink.Entries, e =>
            e.EventType == "UserAccountRoleAdded" && e.Category == SecurityAuditCategories.AdminSecurity);
    }

    [Fact]
    public async Task Deactivation_IsAudited_AsAdminSecurity()
    {
        var sink = new RecordingSink();
        await using var provider = BuildProvider(sink: sink);
        var options = Options();

        await CreateAsync(provider, "r1", "alice", options);

        await MutateAsync(provider, "r1", "alice", a => a.Deactivate(Start));

        Assert.Single(sink.Entries, e =>
            e.EventType == "UserAccountDeactivated" && e.Category == SecurityAuditCategories.AdminSecurity);
    }

    // ---- harness ----

    private static ServiceProvider BuildProvider(
        ISecurityAuditSink? sink = null,
        IDomainEventObserver? observer = null,
        ISecurityAuditPolicyProvider? policy = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeClock(Start));
        services.AddSingleton<IUserAccountPasswordHasher, FakeHasher>();

        // Registered before the module so the module's TryAdd defaults do not win.
        if (sink is not null)
            services.AddSingleton(sink);
        if (policy is not null)
            services.AddSingleton(policy);
        if (observer is not null)
            services.AddSingleton<IDomainEventObserver>(observer);

        services.AddUserAccountsSqliteInMemory();
        return services.BuildServiceProvider();
    }

    private static UserAccountsRealmOptions Options() => UserAccountsTestOptions.Relaxed(allowProvidedSubjectId: true);

    private static async Task CreateAsync(
        ServiceProvider provider, string realmId, string username, UserAccountsRealmOptions options)
    {
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICreateUserAccountHandler>();
        var result = await handler.HandleAsync(new CreateUserAccount
        {
            RealmId = realmId,
            Options = options,
            Username = username,
            SubjectId = username,
            Password = "secret"
        }, default);
        Assert.True(result.IsSuccess);
    }

    private static async Task ChangePasswordAsync(
        ServiceProvider provider, string realmId, string subjectId, UserAccountsRealmOptions options,
        string currentPassword, string newPassword)
    {
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IChangeUserAccountPasswordHandler>();
        var result = await handler.HandleAsync(new ChangeUserAccountPassword
        {
            RealmId = realmId,
            Options = options,
            SubjectId = subjectId,
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        }, default);
        Assert.True(result.IsSuccess);
    }

    private static async Task BlockAsync(ServiceProvider provider, string realmId, string subjectId)
    {
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IBlockUserAccountHandler>();
        var result = await handler.HandleAsync(new BlockUserAccount
        {
            RealmId = realmId,
            SubjectId = subjectId,
            Reason = "fraud"
        }, default);
        Assert.True(result.IsSuccess);
    }

    private static async Task MutateAsync(
        ServiceProvider provider, string realmId, string subjectId, Action<UserAccount> mutate)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserAccountsSqliteDbContext>();
        var account = await db.UserAccounts
            .Include(a => a.LocalCredential)
            .FirstAsync(a => a.RealmId == realmId && a.SubjectId == subjectId);
        mutate(account);
        await db.SaveChangesAsync();
    }

    private sealed class RecordingSink : ISecurityAuditSink
    {
        public List<SecurityAuditEntry> Entries { get; } = [];

        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObserver : IDomainEventObserver
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task OnEventAsync(IDomainEvent domainEvent, CancellationToken ct = default)
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPolicyProvider(SecurityAuditCategories categories) : ISecurityAuditPolicyProvider
    {
        public SecurityAuditCategories GetCategories(string realmId) => categories;
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => start;
    }

    private sealed class FakeHasher : IUserAccountPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }
}
