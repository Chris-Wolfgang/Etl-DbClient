type: fix

Server-side paging no longer adds `@PageOffset`/`@PageLimit` to the caller's `DynamicParameters`, so one instance can be reused for the next page.
