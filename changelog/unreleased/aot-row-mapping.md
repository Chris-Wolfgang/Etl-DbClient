type: feature

`DbExtractor<TRecord>` now extracts rows in a Native AOT application (it used to abort on the first row with `PlatformNotSupportedException` from Dapper's Reflection.Emit row parser), and `TRecord` on `DbExtractor`, `DbLoader`, `DbSchemaValidator` and the pipeline extensions is annotated `[DynamicallyAccessedMembers(PublicConstructors | PublicProperties)]`, so generic callers that run the trim analyzer must annotate their own type parameter the same way.
