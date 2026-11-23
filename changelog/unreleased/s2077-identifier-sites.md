type: internal

The three SQL sites that format a table identifier or wrap the caller's own command text carry a per-line, reasoned suppression of Sonar S2077 (identifiers cannot be bound as parameters); the EtlPipeline example seeds its rows with a parameterized `INSERT`.
