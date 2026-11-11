type: internal

`ColumnAttributeTypeMapper.Register` now completes the Dapper `[Column]` type-map registration before any concurrent caller returns; a caller that arrived while another thread was registering the same type could build a row parser on the default type map, which made the Dapper/reflection row-mapper conformance test fail intermittently.
