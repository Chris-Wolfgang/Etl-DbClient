type: internal

The options constructor assigns the stage's backing fields directly instead of going through the deprecated setters, so the `CS0618` suppressions that covered those writes are gone. The six validating setters' guards (`DbExtractorOptions.CommandTimeout`; `DbLoaderOptions.CommandTimeout` / `InsertBatchSize` / `MaxErrorCount` / `BatchCommitSize` / `BatchSize`) now also run on the records' init accessors, with tests; the extractor's `Parameters` backing field is named `_parameterOverride` because `_parameters` already holds the constructor dictionary. (#441)
