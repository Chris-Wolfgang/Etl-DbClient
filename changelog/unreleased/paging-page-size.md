type: feature

`DbExtractorOptions.PageSize` sets the rows per round-trip, so one extractor walks the whole result set page by page instead of the caller writing the page loop; it requires a `PagingClauseTemplate` (without one, extraction throws `InvalidOperationException` rather than running unpaged), and the last page requests only the rows still needed.
