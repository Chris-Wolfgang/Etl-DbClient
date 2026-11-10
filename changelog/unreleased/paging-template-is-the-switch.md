type: feature

`PagingClauseTemplate` is now the switch for server-side paging: with a template set, `SkipItemCount` and `MaximumItemCount` are pushed into a single query (skipped rows are never fetched, and still count in `CurrentSkippedItemCount`), and without one they are applied client-side, with a warning logged for a client-side skip.
