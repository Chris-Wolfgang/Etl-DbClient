using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }



[Collection("SqlServer")]
[Trait("Category", "sqlserver")]
public sealed class SqlServerExtractorTests : DbExtractorIntegrationTestsBase
{
    private readonly SqlServerFixture _fixture;
    public SqlServerExtractorTests(SqlServerFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("SqlServer")]
[Trait("Category", "sqlserver")]
public sealed class SqlServerLoaderTests : DbLoaderIntegrationTestsBase
{
    private readonly SqlServerFixture _fixture;
    public SqlServerLoaderTests(SqlServerFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}
