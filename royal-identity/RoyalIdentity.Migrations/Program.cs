using RoyalIdentity.Migrations;

MigrationRunnerOptions? parsedOptions = null;
try
{
	parsedOptions = MigrationRunnerOptions.Parse(args);
	if (parsedOptions.ShowHelp)
	{
		Console.WriteLine(MigrationRunnerOptions.Usage);
		return 0;
	}

	await ConfigurationMigrationRunner.RunAsync(parsedOptions, CancellationToken.None);
	Console.WriteLine(
		$"RoyalIdentity Configuration migration completed for provider '{parsedOptions.ConfigurationProvider}'.");
	return 0;
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
		$"RoyalIdentity Configuration migration failed: {exception.GetType().Name}: " +
		MigrationRunnerDiagnostics.Sanitize(exception.Message, parsedOptions));
	return 1;
}
