type: docs

The paging documentation now states the real cost: `OFFSET n` is not a seek, so walking a table of `N` rows scans about `N² / (2 × PageSize)` rows in total, and paging buys bounded per-query work, shorter transactions and resumability rather than less work.
