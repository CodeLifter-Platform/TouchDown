namespace TouchDown.Tests.TestSupport;

/// <summary>
/// Every test class that boots the real app through <see cref="HostedApp"/> joins this
/// collection, which turns off parallel execution between them. Hangfire publishes its
/// storage through the static <c>JobStorage.Current</c>, so two hosted apps alive at once
/// share one global: the dashboard of one can read the SQLite storage the other has just
/// disposed, which surfaced in CI as a 500 from <c>/hangfire</c> that never reproduced
/// locally. Serialising the hosted classes removes the race without touching the product.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HostedAppCollection
{
    public const string Name = "HostedApp";
}
