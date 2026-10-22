type: internal

Examples, samples, benchmarks and the GC workload: orphaned "deprecated setters" comments and an unused `CS0618` pragma removed, the redundant inline `nosemgrep` dropped (Semgrep scans `src/` only and `.semgrepignore` already lists `benchmarks/`), two wrong comments fixed, and the GC workload now prints its summary when the duration timer ends a cycle mid-way.
