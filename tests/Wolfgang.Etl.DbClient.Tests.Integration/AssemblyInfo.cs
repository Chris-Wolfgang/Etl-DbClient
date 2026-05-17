using Xunit;

// Integration test collections each spin up their own Docker container.
// Running them in parallel would mean multiple containers (SQL Server,
// Postgres, MySQL) booting concurrently — slow on dev boxes and prone
// to resource contention / flaky startup on shared CI runners. CI already
// invokes `dotnet test` once per RDBMS via `--filter Category=...`, so the
// parallelism gain is purely a local-dev concern, and the cost is large.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
