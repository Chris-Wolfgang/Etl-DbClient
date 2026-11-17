type: fix

`DbExtractor` now writes `EtlParameter<T>` output and input-output values back after `ExtractAsync`; they were never set before.
