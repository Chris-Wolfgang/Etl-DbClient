type: breaking

`ServerOffset` and `ServerLimit` (on `DbExtractorOptions`, `DbExtractor` and `IDbExtractorBuilder<T>`) are now deprecated aliases of `SkipItemCount` and `MaximumItemCount` (`ServerLimit` keeps its 0.12.0 meaning of total rows; use `PageSize` for rows per round-trip), the canonical property wins when a record sets both, and values the aliases cannot represent now throw `ArgumentOutOfRangeException` (a `ServerLimit` below 1, which used to return no rows, or a value beyond `Int32`), while an unset `DbExtractor.ServerOffset` now reads `0` instead of `null`.
