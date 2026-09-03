# Third-Party Notices

`Wolfgang.Etl.DbClient` redistributes no third-party code, but its published package
declares the runtime dependencies below. Their licences are reproduced or linked here
for attribution.

Reflects the runtime dependency versions declared in
`src/Wolfgang.Etl.DbClient/Wolfgang.Etl.DbClient.csproj`, which are listed explicitly below
rather than tied to a package version that would drift. The
`license-audit` workflow regenerates this list and fails the build if any dependency
declares a licence outside `.github/license/allowed-licenses.json`.

## Runtime dependencies

| Package | Version | Licence |
|---|---|---|
| [Dapper](https://www.nuget.org/packages/Dapper) | 2.1.66 | Apache-2.0 |
| [Microsoft.Bcl.AsyncInterfaces](https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces) | 10.0.11 | MIT |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) | 10.0.11 | MIT |
| [System.Memory](https://www.nuget.org/packages/System.Memory) | 4.6.3 | MIT |

`Wolfgang.Etl.Abstractions` is also a runtime dependency. It is first-party — part of
this same project family, MIT licensed — so it is listed separately rather than as a
third-party notice.

`JetBrains.Annotations` is referenced with `PrivateAssets="all"`: it is a
compile-time-only aid, is not part of the published dependency graph, and consumers
never acquire it.

## Licence texts

### Apache-2.0 — Dapper

Licensed under the Apache License, Version 2.0. You may obtain a copy at
<https://www.apache.org/licenses/LICENSE-2.0>.

Distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
express or implied. See the licence for the specific language governing permissions and
limitations.

### MIT — Microsoft.Bcl.AsyncInterfaces, Microsoft.Extensions.Logging.Abstractions, System.Memory

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify,
merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be included in all copies
or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR
THE USE OR OTHER DEALINGS IN THE SOFTWARE.
