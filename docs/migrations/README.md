# Migration guides

Per-major-version upgrade paths for `Wolfgang.Etl.DbClient`. Each guide documents breaking changes, before/after code, deprecation timeline, and a fix-your-code cheat sheet for consumers.

## Convention

- **New major bumps land with the guide as part of release prep — never a follow-up.** The release PR that stamps the new `<Version>` in the csproj must also add a numbered migration guide here and link to it from CHANGELOG and the GitHub Release notes.
- **Deprecations get flagged one minor before the removal.** If `Z.0` removes something, `Y.n` (the last minor of the previous major) marks it `[Obsolete]` with a message pointing at the migration guide. Consumers who upgrade minor-by-minor never hit an undocumented break.
- **Guides are consumer-facing, not internal.** Explain the "before/after/why" so a downstream maintainer skimming the guide during their own upgrade window can act without opening the source.

## Index

*No major releases yet — the library is pre-1.0. This section will list guides once the first major bump ships.*

| From | To | Guide | Released |
|---|---|---|---|

## Adding a guide

1. Copy `TEMPLATE-major-version-migration.md` to `<PREV>-to-<NEXT>.md` (e.g. `0.x-to-1.0.md`, `1.x-to-2.0.md`).
2. Fill in the sections. Every breaking-change bullet gets **before / after / why / fix pattern**.
3. Add a row to the Index table above.
4. Link the guide from the release CHANGELOG entry and the GitHub Release notes.
5. Land it in the same PR as the `<Version>` bump.

## Why proactively file this template

Cheap to write pre-1.0 while there's nothing under time pressure. Expensive to retrofit after a major ships without one — consumers have already reverse-engineered the breaks by then, complained in issues, and abandoned the upgrade.

Refs [#149](https://github.com/Chris-Wolfgang/Etl-DbClient/issues/149).
