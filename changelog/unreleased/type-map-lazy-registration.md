type: internal

`ColumnAttributeTypeMapper` registers a record type's `[Column]` map through a once-only `Lazy` step instead of a lock, so the registration is both race-free and free of the synchronisation findings InspectCode reported on the previous fix.
