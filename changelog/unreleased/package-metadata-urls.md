type: fix

Package metadata: `PackageProjectUrl` and `RepositoryUrl` now point at `github.com/Chris-Wolfgang/Etl-DbClient` (the old `Wolfgang.Etl.DbClient` slug 404s). The redundant direct `System.Memory` dependency is dropped; .NET Framework and .NET Standard 2.0 consumers still receive System.Memory 4.6.3 transitively through Microsoft.Extensions.Logging.Abstractions.
