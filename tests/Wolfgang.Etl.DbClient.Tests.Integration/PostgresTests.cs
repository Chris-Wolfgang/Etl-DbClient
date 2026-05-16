using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }



[Collection("Postgres")]
[Trait("Category", "postgres")]
[ExcludeFromCodeCoverage]
public sealed class PostgresExtractorTests : DbExtractorIntegrationTestsBase
{
    private readonly PostgresFixture _fixture;
    public PostgresExtractorTests(PostgresFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}



[Collection("Postgres")]
[Trait("Category", "postgres")]
[ExcludeFromCodeCoverage]
public sealed class PostgresLoaderTests : DbLoaderIntegrationTestsBase
{
    private readonly PostgresFixture _fixture;
    public PostgresLoaderTests(PostgresFixture fixture) => _fixture = fixture;
    protected override IDbProviderFixture Fixture => _fixture;
}
