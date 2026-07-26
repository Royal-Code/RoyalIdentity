using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

/// <summary>
/// Runs <see cref="IOperationalMaintenance.CleanupAsync"/> on an interval. It is registered only in
/// <see cref="CleanupExecutionMode.Hosted"/>; in <see cref="CleanupExecutionMode.External"/> the very same
/// maintenance is exposed to a command or job and no worker exists, so the two schedulers can never both run
/// (plan DF17).
/// <para>
/// A failing pass is logged and the loop continues: cleanup is idempotent, so the next pass simply picks up what
/// the previous one left. Logs carry counts by type, never handles, subjects or payloads (plan DF28).
/// </para>
/// </summary>
internal sealed class OperationalCleanupWorker(
    IServiceProvider services,
    IOptions<OperationalCleanupOptions> options,
    TimeProvider clock,
    ILogger<OperationalCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.Interval, clock);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                // A scope per pass: the maintenance and its DbContext are scoped, exactly like a request.
                await using var scope = services.CreateAsyncScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<IOperationalMaintenance>();

                var report = await maintenance.CleanupAsync(
                    clock.GetUtcNow().UtcDateTime, settings.BatchSize, stoppingToken);

                if (report.Total is not 0)
                {
                    logger.LogInformation(
                        "Operational cleanup removed {Total} records: {AccessTokens} access tokens, " +
                        "{RefreshTokens} refresh tokens, {AuthorizationCodes} authorization codes, " +
                        "{Consents} consents, {UserSessions} sessions, {AuthorizeParameters} authorize parameters.",
                        report.Total,
                        report.AccessTokens,
                        report.RefreshTokens,
                        report.AuthorizationCodes,
                        report.Consents,
                        report.UserSessions,
                        report.AuthorizeParameters);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The operational cleanup pass failed; the next pass will retry.");
            }
        }
    }
}
