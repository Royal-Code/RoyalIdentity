namespace RoyalIdentity.Storage.EntityFramework.Operational.Maintenance;

/// <summary>Which scheduler drives the cleanup. Exactly one, never both (plan DF17).</summary>
public enum CleanupExecutionMode
{
    /// <summary>
    /// No scheduler chosen. This is the default so that omitting the choice fails validation instead of
    /// silently landing on one of the two (plan DF17: an absent configuration must fail).
    /// </summary>
    Unspecified = 0,

    /// <summary>A hosted worker inside this process runs the cleanup on the configured interval.</summary>
    Hosted,

    /// <summary>
    /// Nothing is scheduled here: the same maintenance is exposed to an external command or job. Choosing this
    /// and then scheduling nothing means the data grows forever, which is an operational decision, not a
    /// default.
    /// </summary>
    External,
}

/// <summary>
/// Cleanup configuration (plan DF17). There is deliberately no retention grace: a record becomes eligible when
/// it stops being semantically observable, and not a configurable while later.
/// </summary>
public sealed class OperationalCleanupOptions
{
    /// <summary>
    /// Which scheduler runs the maintenance. There is no default: the composition must choose, because both
    /// possible defaults are wrong to assume — one starts a worker nobody asked for, the other lets the data
    /// grow forever.
    /// </summary>
    public CleanupExecutionMode Mode { get; set; } = CleanupExecutionMode.Unspecified;

    /// <summary>How often the hosted worker runs. Ignored in <see cref="CleanupExecutionMode.External"/>.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Maximum rows removed per record type per pass, so one pass never locks the table for long.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// <para>
    ///     The largest post-consumption tolerance any client of this deployment may be configured with. A
    ///     consumed refresh token is only removed once this much time has passed, so cleanup can never delete a
    ///     token some client would still accept.
    /// </para>
    /// <para>
    ///     The tolerance itself is per-client Configuration data, and Operational may live in a different
    ///     database (plan DF6), so it cannot be joined here. <c>null</c> — the default — is the conservative
    ///     reading: consumed refresh tokens are removed only when they expire, exactly like a token nobody
    ///     consumed. That also covers <c>TimeSpan.MaxValue</c>, the reusable-token setting.
    /// </para>
    /// </summary>
    public TimeSpan? MaxRefreshTokenPostConsumedTolerance { get; set; }

    /// <summary>
    /// Validates internal consistency of the cleanup options.
    /// </summary>
    /// <returns>A list of configuration errors. Empty means valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (!Enum.IsDefined(Mode))
            errors.Add("Cleanup.Mode is not a valid execution mode.");
        else if (Mode is CleanupExecutionMode.Unspecified)
            errors.Add("Cleanup.Mode must be selected explicitly, either Hosted or External.");

        if (Mode is CleanupExecutionMode.Hosted && Interval <= TimeSpan.Zero)
            errors.Add("Cleanup.Interval must be greater than zero when the hosted worker is enabled.");

        if (BatchSize <= 0)
            errors.Add("Cleanup.BatchSize must be greater than zero.");

        if (MaxRefreshTokenPostConsumedTolerance is { } tolerance && tolerance < TimeSpan.Zero)
            errors.Add("Cleanup.MaxRefreshTokenPostConsumedTolerance cannot be negative.");

        return errors;
    }
}
