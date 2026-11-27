type: breaking

The auto-generated INSERT for a record whose only mapped columns are database-generated (an identity key alone) now throws the existing "no mapped columns for INSERT" `InvalidOperationException` instead of writing the identity column, which most engines reject without `IDENTITY_INSERT`.
