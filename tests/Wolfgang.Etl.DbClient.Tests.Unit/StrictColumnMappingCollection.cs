using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Serializes the test classes that flip the process-wide
/// <see cref="DbClientOptions.StrictColumnMapping"/> static.
/// </summary>
/// <remarks>
/// Each such test restores the previous value in a <c>finally</c>, but xunit runs test classes
/// in parallel, so one class can reset the flag while another is relying on it. Parallelization
/// is disabled for this collection so its tests also never overlap any other test that extracts
/// rows while the flag is on.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StrictColumnMappingCollection
{
    public const string Name = "DbClientOptions.StrictColumnMapping";
}
