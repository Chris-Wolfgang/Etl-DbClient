using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

[CollectionDefinition("MariaDb")]
public class MariaDbCollection : ICollectionFixture<MariaDbFixture> { }



[Collection("MariaDb")]
[Trait("Category", "mariadb")]
public sealed class MariaDbExtractorTests : DbExtractorIntegrationTestsBase
{
    private readonly MariaDbFixture _fixture;
    public MariaDbExtractorTests(MariaDbFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("MariaDb")]
[Trait("Category", "mariadb")]
public sealed class MariaDbLoaderTests : DbLoaderIntegrationTestsBase
{
    private readonly MariaDbFixture _fixture;
    public MariaDbLoaderTests(MariaDbFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}
