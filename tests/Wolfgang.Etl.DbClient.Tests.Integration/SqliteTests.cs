using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

[CollectionDefinition("Sqlite")]
public class SqliteCollection : ICollectionFixture<SqliteFixture> { }



[Collection("Sqlite")]
[Trait("Category", "sqlite")]
public sealed class SqliteExtractorTests : DbExtractorIntegrationTestsBase
{
    private readonly SqliteFixture _fixture;
    public SqliteExtractorTests(SqliteFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("Sqlite")]
[Trait("Category", "sqlite")]
public sealed class SqliteLoaderTests : DbLoaderIntegrationTestsBase
{
    private readonly SqliteFixture _fixture;
    public SqliteLoaderTests(SqliteFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}
