using Microsoft.Extensions.Options;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

/// <summary>
/// Records the cleanup execution mode this composition selected (plan DF17). Its presence in the service
/// collection is what makes the choice explicit and checkable: the complete gateway refuses to compose without
/// it, and selecting twice is refused rather than silently resolved by last-one-wins.
/// </summary>
internal sealed record OperationalCleanupRegistration(CleanupExecutionMode Mode);

/// <summary>
/// Validates the <b>effective</b> cleanup options, not the temporary instance the registration inspected. Any
/// later <c>Configure</c> that breaks them — or that flips the mode away from the one whose scheduler was
/// registered — fails the first time the options are resolved, instead of producing a composition with a worker
/// that believes it is not running or no worker at all (plan DF17).
/// </summary>
internal sealed class OperationalCleanupOptionsValidator(CleanupExecutionMode selected)
    : IValidateOptions<OperationalCleanupOptions>
{
    public ValidateOptionsResult Validate(string? name, OperationalCleanupOptions options)
    {
        List<string> errors = [.. options.Validate()];

        if (options.Mode != selected)
        {
            errors.Add(
                $"Cleanup.Mode resolved to '{options.Mode}', but the composition registered '{selected}'. " +
                "The scheduler is chosen at registration time, so changing the mode afterwards would leave " +
                "the deployment with the wrong scheduler, or none.");
        }

        return errors.Count is 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
