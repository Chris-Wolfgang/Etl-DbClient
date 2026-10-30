# ADR-0002: Native AOT support: annotate TRecord, keep Dapper on the JIT, map and bind without Reflection.Emit under AOT

- **Status**: Proposed
- **Date**: 2026-10-30
- **Deciders**: @Chris-Wolfgang
- **Tags**: `aot`, `trimming`, `api-shape`

## Context

Issue [#415](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/415) asks for `IsAotCompatible` / `IsTrimmable`. A real Native AOT publish of `examples/Wolfgang.Etl.DbClient.Example.AotSmoke` (linux-x64, `mcr.microsoft.com/dotnet/sdk:10.0.402`, the command from [#574](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/574)) on `main` @ `c930388` + #574 gives:

- **41 IL2xxx trim warnings and 13 IL3xxx AOT warnings** (warning lines in the publish log). All but three come from Dapper 2.1.66, which is not trim-annotated and builds its row parsers and parameter binders with Reflection.Emit (`SqlMapper.GetTypeDeserializerImpl`, `CreateParamInfoGenerator`, `DefaultTypeMap.FindConstructor`, `CommandDefinition.GetInit`, ...). The other three are this library's own `Type.GetProperties` calls on an unannotated `Type`/`TRecord` (`DbLoader<TRecord>.GetMappedProperties` IL2090, `DbSchemaValidator.ExtractMapping` IL2070, `ColumnAttributeTypeMapper.BuildLookup` IL2070).
- The published binary **aborts (exit 134)** on the first extracted row: `PlatformNotSupportedException: Dynamic code generation is not supported on this platform` from `Dapper.SqlMapper.GetTypeDeserializerImpl`, reached through `reader.GetRowParser<TRecord>()` in `DbExtractor<TRecord>.ExtractWorkerAsync`.

With the first step of this decision applied (annotations + the row mapper below), the same publish gives **15 IL2xxx + 8 IL3xxx warnings, all in Dapper, none in this library**, and the binary **runs to completion (exit 0)**, extracting all three rows. The remaining warnings are Dapper's parameter-object binder (`CreateParamInfoGenerator`, reachable from `DbLoader<TRecord>`), its type-handler registry (`AddTypeHandlerCore`), `CommandDefinition` setup and `DefaultTypeMap`.

So "works but slow" is not the situation here (unlike `Expression.Compile`, which falls back to an interpreter): Reflection.Emit has no fallback, and every Dapper path that materializes a `TRecord` or binds a `TRecord` as a parameter object fails under AOT.

The library is generic over `TRecord`, which is closed only in the consumer's code. That rules out fixing it at our call sites with a call-site generator: the call sites inside `DbExtractor<TRecord>` and `DbLoader<TRecord>` never see a concrete type.

## Decision

1. **Annotate `TRecord`** with `[DynamicallyAccessedMembers(PublicConstructors | PublicProperties)]` (`RecordMembers.Mapped`) on every public generic entry point that reflects over it: `DbExtractor<TRecord>`, `DbLoader<TRecord>`, `DbSchemaValidator.Validate<TRecord>` / `ValidateAsync<TRecord>`, and the `EtlPipelineDbClientExtensions.DbExtractor<T>` / `DbLoader<T>` extensions (plus the internal builders and helpers). The trimmer then keeps exactly the members the library reflects over, and the trim analyzer can verify this library's own reflection. On TFMs older than .NET 5 the attribute is an internal polyfill (`System.Diagnostics.CodeAnalysis`); only the net8.0+ assets are ever trimmed.
2. **Keep Dapper on the JIT; take a non-emitting path under AOT.** Each Dapper call that needs Reflection.Emit gets a reflection-only replacement, selected with `RuntimeFeature.IsDynamicCodeSupported`. That property is a constant `false` to the Native AOT compiler, so the Dapper branch and the Reflection.Emit code behind it are removed from an AOT application rather than merely skipped. On the JIT nothing changes: Dapper's emitted code is faster and stays the default.
   - **Rows (this PR):** `ReflectionRowMapper<TRecord>` builds `TRecord` with `ConstructorInfo.Invoke` / `PropertyInfo.SetValue`, resolving columns through the same lookup as the Dapper type map (`ColumnAttributeTypeMapper.BuildColumnLookup` / `ResolveColumn`, so `[Column]` names and `DbClientOptions.StrictColumnMapping` behave the same) and wrapping conversion failures in `DataException` (the type `ErrorPolicy` handling keys on).
   - **Parameters (next PR):** `DbLoader<TRecord>` passes `TRecord` as Dapper's parameter object (`CreateParamInfoGenerator`, Reflection.Emit). Under AOT it will bind through `DynamicParameters` filled from the annotated `TRecord` properties, the way the multi-row INSERT binder already does.
3. **A conformance test is the contract.** `ReflectionRowMapperTests` reads the same rows through Dapper's `GetRowParser<T>` and through the reflection mapper and requires identical results for each supported record shape (settable properties with `[Column]`, positional records, structs, scalars). The parameter binder gets the same treatment.
4. **Declare `IsAotCompatible` / `IsTrimmable` last**, once the smoke binary runs end to end (extract and a real load), together with raising the `Trimming` / `AOT` analyzer categories from `suggestion` to `warning` in `.editorconfig` (a protected file, so its own PR) so the declaration is enforced, and then making the AOT smoke check blocking ([#476](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/476)).

A source-generated fast path (extending `DbTableGenerator`, which already emits `Bind` helpers for `[DbTable]` types) can later replace the reflection mapper for `[DbTable]` types; it is an optimization on top of this design, not a prerequisite.

## Consequences

- **Positive**: AOT consumers get a working extractor (and, with the next PR, loader) without a new package or a new API; JIT consumers keep Dapper's speed and behaviour; this library's own reflection is analyzer-verified.
- **Negative**:
  - **Public-surface change.** The annotation flows to consumers: code that is itself generic over `T` and constructs `DbExtractor<T>` / `DbLoader<T>` or calls the pipeline extensions now gets `IL2091` from the trim analyzer until it annotates its own `T` the same way. Concrete types are unaffected, and nothing changes for consumers that do not run the trim analyzer. Not a compile-time or binary break.
  - Two row-mapping implementations. The reflection mapper is slower than Dapper's emitted parser and covers less: no value tuples, public fields, non-public properties, or Dapper custom type handlers (all still work on the JIT). It is more lenient than Dapper in one place: a constructor is matched by parameter name only, while Dapper also requires the column types to match exactly.
  - The Dapper warnings that sit behind calls the AOT path still makes (`ExecuteReaderAsync`, `ExecuteScalarAsync`, `CommandDefinition`) remain in the publish log until those calls are replaced or Dapper ships annotations.
- **Neutral**: `IsAotCompatible` is not declared yet; the package makes no AOT promise until step 4.

## Alternatives considered

- **Dapper.AOT** (build-time interceptors): generates code at call sites with concrete types. Our calls are inside generic types, so it has nothing to generate for; it also needs C# 12 interceptors (net8.0+ only) and adds a dependency.
- **`[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` on the Dapper-backed entry points plus an AOT-safe overload**: honest, but it marks essentially the whole public API (both constructors and the extensions) as AOT-incompatible and pushes every AOT consumer to a second API surface. Annotating `TRecord` and switching implementation internally keeps one API.
- **Replace Dapper entirely with hand-written ADO.NET**: removes all warnings but throws away Dapper's JIT speed and behaviour for every consumer to fix a problem only AOT consumers have.
- **Wait for Dapper to ship trim/AOT annotations**: would remove warnings, not the Reflection.Emit crash.

## Notes

- Issue: [#415](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/415). Smoke workflow fix: [#574](https://github.com/Chris-Wolfgang/Etl-DbClient/pull/574). Required check: [#476](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/476).
- `RuntimeFeature.IsDynamicCodeSupported` as an AOT feature switch: <https://learn.microsoft.com/dotnet/core/deploying/native-aot/>.
