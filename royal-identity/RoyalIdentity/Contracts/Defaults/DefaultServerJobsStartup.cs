using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultServerJobsStartup : IHostedService
{
    private readonly IServiceProvider applicationServices;
    private readonly ILogger logger;
    private readonly List<Task> tasks = [];

    private IServiceScope? scope;

    public DefaultServerJobsStartup(IServiceProvider applicationServices, ILogger<DefaultServerJobsStartup> logger)
    {
        this.applicationServices = applicationServices;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        scope = applicationServices.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IEnumerable<IServerJob>>();

        foreach (var job in jobs)
        {
            if (job.Background)
            {
                tasks.Add(TryRun(job, logger, cancellationToken));
            }
            else
            {
                await TryRun(job, logger, cancellationToken);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(tasks);

        scope?.Dispose();
        scope = null;
    }

    private static async Task TryRun(IServerJob job, ILogger logger, CancellationToken applicationStopping)
    {
        string jobName = "unknow";
        try
        {
            jobName = job.Name;
            if (job.Background)
                return;

            logger.LogDebug("Running the job: {JobName}", jobName);

            await job.RunAsync(applicationStopping);

            logger.LogDebug("Job '{JobName}' completed", jobName);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Job throws a exception [{JobName}]", jobName);
        }
    }
}
