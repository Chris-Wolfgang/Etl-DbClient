type: fix

Multi-row INSERT (`InsertBatchSize > 1`) no longer misreads `VALUES` inside identifiers or `@` inside string literals, and rejects non-`@` parameter markers with a clear message.
