type: fix

`DbLoader<T>` with `BatchCommitSize` set now stops reading the source once `MaximumItemCount` is reached, instead of enumerating the rest of the source and committing an empty transaction per chunk.
