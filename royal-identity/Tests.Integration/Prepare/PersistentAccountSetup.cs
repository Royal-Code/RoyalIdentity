using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.UseCases;
using RoyalIdentity.UserAccounts.Infrastructure.Data;
using RoyalIdentity.UserAccounts.Integration;
using Tests.UserAccounts;

namespace Tests.Integration.Prepare;

/// <summary>
/// Test-only account operations over the real module. Seed, account state and dynamic claims all execute module
/// behavior; no scenario mutates EF rows or a fake account object directly.
/// </summary>
internal sealed class PersistentAccountSetup(
    IServiceProvider services,
    UserAccountsDbContext db,
    UserAccountReader reader,
    IUserAccountsRealmOptionsResolver optionsResolver,
    TimeProvider clock)
{
    public async Task SeedAsync(
        string realmId,
        TestSubjectHandle subject,
        bool active,
        CancellationToken ct)
    {
        await UserAccountsModuleSeed.SeedAccountAsync(
            services,
            realmId,
            optionsResolver.Resolve(realmId),
            subject.SubjectId,
            subject.Username,
            subject.Username,
            $"{subject.Username}@example.test",
            subject.Password,
            active,
            ["admin"],
            clock.GetUtcNow(),
            ct);
    }

    public async Task SetActiveAsync(
        string realmId,
        string subjectId,
        bool active,
        CancellationToken ct)
    {
        var account = await reader.FindBySubjectIdAsync(realmId, subjectId, ct)
            ?? throw new InvalidOperationException(
                $"Subject '{subjectId}' was not found in realm '{realmId}'.");

        if (active)
            account.Activate(clock.GetUtcNow());
        else
            account.Deactivate(clock.GetUtcNow());

        await db.SaveChangesAsync(ct);
    }

    public async Task SetClaimAsync(
        string realmId,
        string subjectId,
        string scopeName,
        string claimType,
        IReadOnlyList<string> values,
        CancellationToken ct)
    {
        var result = await services
            .GetRequiredService<ISetUserAccountScopePropertyHandler>()
            .HandleAsync(new SetUserAccountScopeProperty
            {
                RealmId = realmId,
                SubjectId = subjectId,
                ScopeName = scopeName,
                ClaimType = claimType,
                Values = values,
            }, ct);

        if (result.HasProblems(out var problems))
        {
            throw new InvalidOperationException(
                $"Could not set claim '{claimType}' for subject '{subjectId}': {problems}");
        }
    }
}
