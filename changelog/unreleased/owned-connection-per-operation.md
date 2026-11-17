type: fix

The extractor and loader built from a `DbProviderFactory` and connection string now use a fresh connection for each operation, so `CountAsync` then `ExtractAsync` (or running twice) works on SQL Server and PostgreSQL; a failed open no longer leaks the connection.
