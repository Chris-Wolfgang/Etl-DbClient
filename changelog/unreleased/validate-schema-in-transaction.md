type: fix

`ValidateSchemaOnStart` now runs its probe inside the transaction passed to the extractor or loader, so it works with a caller transaction on SQL Server.
