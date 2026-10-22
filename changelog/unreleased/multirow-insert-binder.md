type: internal

`DbLoader<T>` with `InsertBatchSize > 1` binds each cell through a compiled, cached property getter instead of `PropertyInfo.GetValue`, and builds the row-numbered parameter names once per load instead of once per cell per flush. The benchmarks gain a `Load1000RowsMultiRowInsert` case.
