type: internal

`DbLoader<T>` builds its parameter binder or multi-row INSERT plan once per load instead of once per commit chunk, and constructor-mapped rows (positional records under Native AOT) no longer copy the constructor's parameter list for every row.
