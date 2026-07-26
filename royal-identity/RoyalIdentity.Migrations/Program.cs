namespace RoyalIdentity.Migrations;

/// <summary>
/// Entry point of the migration runner. It is a named type inside the product namespace rather than top-level
/// statements: the runner is referenced by test projects that also reference the test host, and two assemblies
/// carrying a global <c>Program</c> cannot be referenced together.
/// </summary>
internal static class MigrationRunnerProgram
{
    private static async Task<int> Main(string[] args)
    {
        MigrationRunnerOptions? parsedOptions = null;
        try
        {
            parsedOptions = MigrationRunnerOptions.Parse(args);
            if (parsedOptions.ShowHelp)
            {
                Console.WriteLine(MigrationRunnerOptions.Usage);
                return 0;
            }

            var report = await StorageMigrationRunner.RunAsync(parsedOptions, CancellationToken.None);

            // Reported family by family, never as one outcome: the two may live in different databases, and even
            // in one database they are applied as two independent sequences (plan DF23).
            foreach (var family in report.Families)
            {
                if (family.Status is StorageMigrationStatus.Skipped)
                    continue;

                var line = $"RoyalIdentity {family.Family} migration: {family.Status} " +
                    $"(provider '{parsedOptions.ConfigurationProvider}').";

                if (family.Status is StorageMigrationStatus.Failed)
                {
                    Console.Error.WriteLine(
                        $"{line} {family.Failure?.GetType().Name}: " +
                        MigrationRunnerDiagnostics.Sanitize(
                            family.Failure?.Message ?? string.Empty, parsedOptions));
                }
                else
                {
                    Console.WriteLine(line);
                }
            }

            return report.Succeeded ? 0 : 1;
        }
        catch (MigrationRunnerUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(MigrationRunnerOptions.Usage);
            return 64;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"RoyalIdentity migration failed: {exception.GetType().Name}: " +
                MigrationRunnerDiagnostics.Sanitize(exception.Message, parsedOptions));
            return 1;
        }
    }
}
