type: fix

In `RowErrorHandling.Skip` on net5.0 and later, a load whose transaction the database aborted after a failed row (SQLSTATE 25P02 on PostgreSQL and CockroachDB) now stops with an `InvalidOperationException` that points at `BatchCommitSize = 1`, instead of reporting every later row as failed and committing nothing.
