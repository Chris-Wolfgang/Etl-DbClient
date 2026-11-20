type: fix

`RowErrorHandling.Skip` now aborts when a failure leaves the connection closed instead of skipping every remaining row; the PostgreSQL/CockroachDB need for `BatchCommitSize = 1` is documented.
