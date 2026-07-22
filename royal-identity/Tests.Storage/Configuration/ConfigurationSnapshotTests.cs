using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Options;
using RoyalIdentity.Responses.HttpResults;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// Behaviors of the configuration snapshot (plan Fase 3, DF7/DF26): atomic publish, defensive copies, the
/// fail-closed bootstrap, the last-known-good policy on a failed periodic refresh, and named-options
/// invalidation scoped to the default scheme plus the affected realm schemes.
/// </summary>
public class ConfigurationSnapshotTests
{
	private static readonly string DefaultScheme = Constants.Server.DefaultCookieAuthenticationScheme;

	private static string RealmScheme(string path) => $"{Constants.Server.RealmAuthenticationNamePrefix}{path}";

	[Fact]
	public void Reads_BeforeAnyLoad_Throw()
	{
		using var harness = new SnapshotTestHarness();

		Assert.False(harness.Snapshot.IsLoaded);
		Assert.Throws<InvalidOperationException>(() => harness.Snapshot.ServerOptions);
		Assert.Throws<InvalidOperationException>(() => harness.Snapshot.FindRealmByPath("server"));
	}

	[Fact]
	public async Task Refresh_PublishesSnapshot_AndInvalidatesDefaultAndRealmSchemes()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions { IssuerUri = "https://issuer.test" }, "server", "alpha");

		await harness.Refresher.RefreshAsync();

		Assert.True(harness.Snapshot.IsLoaded);
		Assert.Equal("https://issuer.test", harness.Snapshot.ServerOptions.IssuerUri);
		Assert.NotNull(harness.Snapshot.FindRealmByPath("alpha"));
		Assert.Null(harness.Snapshot.FindRealmByPath("unknown"));
		Assert.Equal(harness.Clock.Now, harness.Snapshot.LoadedAtUtc);

		Assert.Contains(DefaultScheme, harness.CookieCache.RemovedNames);
		Assert.Contains(RealmScheme("server"), harness.CookieCache.RemovedNames);
		Assert.Contains(RealmScheme("alpha"), harness.CookieCache.RemovedNames);
		// A cookie scheme outside RoyalIdentity is never invalidated.
		Assert.DoesNotContain("SomeForeignScheme", harness.CookieCache.RemovedNames);
	}

	[Fact]
	public async Task ServerOptions_And_Realm_AreDefensiveCopies()
	{
		using var harness = new SnapshotTestHarness();
		var authoritative = new ServerOptions { IssuerUri = "https://issuer.test" };
		authoritative.Discovery.CustomEntries["nested"] = new Dictionary<string, object?>
		{
			["value"] = "original",
		};
		var sourceData = SnapshotTestHarness.BuildData(authoritative, "alpha");
		harness.Source.Data = sourceData;
		await harness.Refresher.RefreshAsync();

		var serverOptions = harness.Snapshot.ServerOptions;
		serverOptions.IssuerUri = "https://mutated.test";
		serverOptions.DispatchEvents = true;

		var realm = harness.Snapshot.FindRealmByPath("alpha")!;
		realm.DisplayName = "mutated";
		realm.Options.Authentication.CookieName = ".mutated";
		realm.Options.ServerOptions.IssuerUri = "https://mutated-through-realm.test";

		var customEntries = harness.Snapshot.ServerOptions.Discovery.CustomEntries;
		var nested = Assert.IsType<Dictionary<string, object?>>(customEntries["nested"]);
		nested["value"] = "mutated";

		// The source is allowed to retain the graph it returned; publication must take ownership of a clone.
		sourceData.ServerOptions.IssuerUri = "https://mutated-through-source.test";
		sourceData.Realms[0].Options.Authentication.CookieName = ".source-mutated";

		Assert.Equal("https://issuer.test", harness.Snapshot.ServerOptions.IssuerUri);
		Assert.False(harness.Snapshot.ServerOptions.DispatchEvents);
		Assert.Equal("Realm alpha", harness.Snapshot.FindRealmByPath("alpha")!.DisplayName);
		Assert.NotEqual(".mutated", harness.Snapshot.FindRealmByPath("alpha")!.Options.Authentication.CookieName);
		Assert.NotEqual(".source-mutated", harness.Snapshot.FindRealmByPath("alpha")!.Options.Authentication.CookieName);
		var persistedNested = Assert.IsType<Dictionary<string, object?>>(
			harness.Snapshot.ServerOptions.Discovery.CustomEntries["nested"]);
		Assert.Equal("original", persistedNested["value"]);
	}

	[Fact]
	public async Task Publish_IsAtomic_LaterRefreshReplacesTheGraph()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions { IssuerUri = "https://first.test" }, "alpha");
		await harness.Refresher.RefreshAsync();

		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions { IssuerUri = "https://second.test" }, "beta");
		await harness.Refresher.RefreshAsync();

		Assert.Equal("https://second.test", harness.Snapshot.ServerOptions.IssuerUri);
		Assert.Null(harness.Snapshot.FindRealmByPath("alpha"));
		Assert.NotNull(harness.Snapshot.FindRealmByPath("beta"));
		// The previous realm scheme is invalidated too (previous ∪ new).
		Assert.Contains(RealmScheme("alpha"), harness.CookieCache.RemovedNames);
		Assert.Contains(RealmScheme("beta"), harness.CookieCache.RemovedNames);
	}

	[Fact]
	public async Task TryRefresh_OnFailure_KeepsLastKnownGood_RecordsFailure_AndDoesNotInvalidate()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions { IssuerUri = "https://good.test" }, "alpha");
		await harness.Refresher.RefreshAsync();
		harness.CookieCache.RemovedNames.Clear();

		harness.Clock.Advance(TimeSpan.FromMinutes(10));
		harness.Source.Failure = new InvalidOperationException("database unavailable");

		var published = await harness.Refresher.TryRefreshAsync();

		Assert.False(published);
		Assert.Equal("https://good.test", harness.Snapshot.ServerOptions.IssuerUri); // last-known-good preserved
		Assert.NotNull(harness.Snapshot.FindRealmByPath("alpha"));
		Assert.Equal(harness.Clock.Now, harness.Snapshot.LastRefreshFailureUtc);
		Assert.Empty(harness.CookieCache.RemovedNames); // nothing invalidated on failure
	}

	[Fact]
	public async Task SuccessfulRefresh_ClearsPreviousFailure()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions(), "alpha");
		await harness.Refresher.RefreshAsync();

		harness.Source.Failure = new InvalidOperationException("down");
		await harness.Refresher.TryRefreshAsync();
		Assert.NotNull(harness.Snapshot.LastRefreshFailureUtc);

		harness.Source.Failure = null;
		await harness.Refresher.TryRefreshAsync();

		Assert.Null(harness.Snapshot.LastRefreshFailureUtc);
	}

	[Fact]
	public async Task Refresh_WhenInitialLoadFails_Throws_AndDoesNotPublish()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Failure = new InvalidOperationException("no server_options");

		await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Refresher.RefreshAsync());

		Assert.False(harness.Snapshot.IsLoaded);
	}

	[Fact]
	public async Task TryRefresh_WhenNoSnapshotExists_ReturnsFalseWithoutThrowing()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Failure = new InvalidOperationException("no server_options");

		var published = await harness.Refresher.TryRefreshAsync();

		Assert.False(published);
		Assert.False(harness.Snapshot.IsLoaded);
		Assert.Equal(harness.Clock.Now, harness.Snapshot.LastRefreshFailureUtc);
	}

	[Fact]
	public async Task TryRefresh_WhenCallerCancels_PropagatesCancellation()
	{
		using var harness = new SnapshotTestHarness();
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => harness.Refresher.TryRefreshAsync(cancellation.Token));
	}

	[Fact]
	public async Task ConcurrentRefreshes_AreSerialized()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions(), "alpha");
		await harness.Refresher.RefreshAsync();

		var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var activeLoads = 0;
		var maximumActiveLoads = 0;
		harness.Source.Loader = async ct =>
		{
			var active = Interlocked.Increment(ref activeLoads);
			Interlocked.Exchange(ref maximumActiveLoads, Math.Max(maximumActiveLoads, active));
			firstEntered.TrySetResult();
			await release.Task.WaitAsync(ct);
			Interlocked.Decrement(ref activeLoads);
			return harness.Source.Data!;
		};

		var first = harness.Refresher.RefreshAsync();
		await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		var second = harness.Refresher.RefreshAsync();

		await Task.Delay(50);
		Assert.Equal(1, maximumActiveLoads);
		Assert.Equal(2, harness.Source.LoadCount); // bootstrap + first; second is waiting at the gate

		release.TrySetResult();
		await Task.WhenAll(first, second);

		Assert.Equal(1, maximumActiveLoads);
		Assert.Equal(3, harness.Source.LoadCount);
	}

	[Fact]
	public async Task CheckSessionResult_ReadsLatestSnapshotOnEveryExecution()
	{
		using var harness = new SnapshotTestHarness();
		var firstOptions = new ServerOptions();
		firstOptions.Authentication.CheckSessionCookieName = "check.first";
		harness.Source.Data = SnapshotTestHarness.BuildData(firstOptions, "server");
		await harness.Refresher.RefreshAsync();

		var result = new CheckSessionResult();
		var firstHtml = await ExecuteCheckSessionAsync(result, harness.Snapshot);

		var secondOptions = new ServerOptions();
		secondOptions.Authentication.CheckSessionCookieName = "check.second";
		harness.Source.Data = SnapshotTestHarness.BuildData(secondOptions, "server");
		await harness.Refresher.RefreshAsync();
		var secondHtml = await ExecuteCheckSessionAsync(result, harness.Snapshot);

		Assert.Contains("check.first", firstHtml);
		Assert.Contains("check.second", secondHtml);
		Assert.DoesNotContain("check.first", secondHtml);
	}

	private static async Task<string> ExecuteCheckSessionAsync(
		CheckSessionResult result, RoyalIdentity.Configuration.IConfigurationSnapshot snapshot)
	{
		var services = new ServiceCollection();
		services.AddSingleton(snapshot);
		await using var provider = services.BuildServiceProvider();
		await using var body = new MemoryStream();
		var context = new DefaultHttpContext
		{
			RequestServices = provider,
		};
		context.Response.Body = body;

		await result.ExecuteAsync(context);
		body.Position = 0;
		using var reader = new StreamReader(body);
		return await reader.ReadToEndAsync();
	}
}
