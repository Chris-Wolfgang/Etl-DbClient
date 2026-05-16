using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

[CollectionDefinition("MySql")]
public class MySqlCollection : ICollectionFixture<MySqlFixture> { }



[Collection("MySql")]
[Trait("Category", "mysql")]
[ExcludeFromCodeCoverage]
public sealed class MySqlExtractorTests : DbExtractorIntegrationTestsBase
{
    private readonly MySqlFixture _fixture;
    public MySqlExtractorTests(MySqlFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("MySql")]
[Trait("Category", "mysql")]
[ExcludeFromCodeCoverage]
public sealed class MySqlLoaderTests : DbLoaderIntegrationTestsBase
{
    private readonly MySqlFixture _fixture;
    public MySqlLoaderTests(MySqlFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}
