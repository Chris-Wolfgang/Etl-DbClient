using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

[CollectionDefinition("CockroachDb")]
public class CockroachDbCollection : ICollectionFixture<CockroachDbFixture> { }



[Collection("CockroachDb")]
[Trait("Category", "cockroachdb")]
[ExcludeFromCodeCoverage]
public sealed class CockroachDbExtractorTests : DbExtractorIntegrationTestsBase
{
    private readonly CockroachDbFixture _fixture;
    public CockroachDbExtractorTests(CockroachDbFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("CockroachDb")]
[Trait("Category", "cockroachdb")]
[ExcludeFromCodeCoverage]
public sealed class CockroachDbLoaderTests : DbLoaderIntegrationTestsBase
{
    private readonly CockroachDbFixture _fixture;
    public CockroachDbLoaderTests(CockroachDbFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}
