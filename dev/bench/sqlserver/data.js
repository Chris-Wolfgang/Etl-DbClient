window.BENCHMARK_DATA = {
  "lastUpdate": 1786469818894,
  "repoUrl": "https://github.com/Chris-Wolfgang/Etl-DbClient",
  "entries": {
    "ExtractorBenchmarks (sqlserver)": [
      {
        "commit": {
          "author": {
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang",
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com"
          },
          "committer": {
            "name": "GitHub",
            "username": "web-flow",
            "email": "noreply@github.com"
          },
          "id": "1e3d0aa1210334485cd6a375a8fee8cf45e4d547",
          "message": "Merge pull request #64 from Chris-Wolfgang/feat/integration-tests-project\n\nfeat: per-RDBMS integration tests + benchmarks + README matrix",
          "timestamp": "2026-05-17T02:25:35Z",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/1e3d0aa1210334485cd6a375a8fee8cf45e4d547"
        },
        "date": 1778986267059,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 315210.57252604165,
            "unit": "ns",
            "range": "± 5616.67111936197"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 689302.9210526316,
            "unit": "ns",
            "range": "± 15178.307184638516"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4441967.626674107,
            "unit": "ns",
            "range": "± 45910.22674185078"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang",
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com"
          },
          "committer": {
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang",
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com"
          },
          "id": "9ab241bfaea42943e8e85219ff4916b9c91ffae1",
          "message": "feat(integration): add MariaDB to the matrix (#62) — non-workflow part\n\nFirst entry from the follow-up RDBMS list. MariaDB shares the MySQL wire\nprotocol so the existing MySqlConnector driver is reused — only the\ncontainer image differs.\n\nNon-workflow file changes\n-------------------------\n- New MariaDbFixture (clones MySqlFixture; uses Testcontainers.MariaDb 4.4.0,\n  image pinned to mariadb:11.4.4).\n- New MariaDbTests with [Trait(\"Category\", \"mariadb\")] and a CollectionDefinition.\n- Tests.Integration.csproj: + Testcontainers.MariaDb 4.4.0.\n- BenchmarkContext: + mariadb provider branch in OpenConnection /\n  ResetSchemaAsync, env var ETL_DBCLIENT_BENCHMARK_MARIADB.\n- scripts/build-pr.ps1 rdbmsList: + mariadb.\n- README \"Tested Databases\": + MariaDB row, version 11.4.\n\nThe matching workflow file changes (pr.yaml matrix entry, benchmarks.yaml\nnew mariadb job, MySQL benchmark connection-string fix) are PR #72,\nwhich lands first via maintainer admin-bypass.\n\nVerified locally on net10.0: 7/7 MariaDb integration tests pass against\na real mariadb:11.4.4 container.\n\nCloses part of #62; Oracle / CockroachDB / YugabyteDB / DB2 / Firebird\nremain on the issue.\n\nCo-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>",
          "timestamp": "2026-05-17T20:15:25Z",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/9ab241bfaea42943e8e85219ff4916b9c91ffae1"
        },
        "date": 1779066065371,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 421690.942452567,
            "unit": "ns",
            "range": "± 1985.4595242124606"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 829129.8744791667,
            "unit": "ns",
            "range": "± 5463.496406445195"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4754931.304086538,
            "unit": "ns",
            "range": "± 35968.47466255481"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "92a45d86e71cf2d040eab2da8c1cfd33795a6b62",
          "message": "Merge pull request #126 from Chris-Wolfgang/vNext\n\nRelease v0.2.1: canonical maintenance round + AssemblyVersion fix",
          "timestamp": "2026-06-22T11:37:05-04:00",
          "tree_id": "0c7ee1d93a9aa467ded28694a19b713f4acad598",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/92a45d86e71cf2d040eab2da8c1cfd33795a6b62"
        },
        "date": 1782143801968,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 413923.3922526042,
            "unit": "ns",
            "range": "± 4050.9325047320103"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 838025.3505161831,
            "unit": "ns",
            "range": "± 5224.7978527517"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4842815.70625,
            "unit": "ns",
            "range": "± 63496.41721777123"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9aaa7af819a0ad1a799c6edab1295c566e489db0",
          "message": "Merge pull request #186 from Chris-Wolfgang/fix/release-skip-integration-tests\n\nfix(release): skip integration tests in release validation",
          "timestamp": "2026-06-22T12:19:07-04:00",
          "tree_id": "2b3c7010d00afeb4ca521b159e1ea1cb13f81c2e",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/9aaa7af819a0ad1a799c6edab1295c566e489db0"
        },
        "date": 1782145529450,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 430392.7454427083,
            "unit": "ns",
            "range": "± 4292.417629979315"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 868792.7497907366,
            "unit": "ns",
            "range": "± 4616.4369763423765"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4898919.405691965,
            "unit": "ns",
            "range": "± 37841.15666849399"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang",
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com"
          },
          "committer": {
            "name": "GitHub",
            "username": "web-flow",
            "email": "noreply@github.com"
          },
          "id": "9aaa7af819a0ad1a799c6edab1295c566e489db0",
          "message": "Merge pull request #186 from Chris-Wolfgang/fix/release-skip-integration-tests\n\nfix(release): skip integration tests in release validation",
          "timestamp": "2026-06-22T16:19:07Z",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/9aaa7af819a0ad1a799c6edab1295c566e489db0"
        },
        "date": 1782150740499,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 335192.25723805145,
            "unit": "ns",
            "range": "± 6713.9679815517975"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 725349.8120404412,
            "unit": "ns",
            "range": "± 13881.66528740539"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4776333.1611328125,
            "unit": "ns",
            "range": "± 86427.52800791297"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a74b76d026470f11623cd4975c0f846e893c0050",
          "message": "Merge pull request #191 from Chris-Wolfgang/vNext\n\nRelease v0.4.0 — production-readiness knobs",
          "timestamp": "2026-06-22T16:48:57-04:00",
          "tree_id": "3b3a5faabe05fdc256d76c17887a02bfa3cd7228",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/a74b76d026470f11623cd4975c0f846e893c0050"
        },
        "date": 1782161601961,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 434798.72184244794,
            "unit": "ns",
            "range": "± 1604.9126109958554"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 865923.9534254808,
            "unit": "ns",
            "range": "± 6240.199566360357"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4924520.914583334,
            "unit": "ns",
            "range": "± 50122.47771235441"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "314ba6c43f00049a4e5793b21a93a40fd1bfd36b",
          "message": "Merge pull request #227 from Chris-Wolfgang/chore/release-v0.6.0\n\nchore: release v0.6.0 (MINOR — IsDryRun + source-gen + batching + paging)",
          "timestamp": "2026-07-11T20:53:18-04:00",
          "tree_id": "82b2087fec4bd96fb7a23b5a568a507e6a7d8d16",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/314ba6c43f00049a4e5793b21a93a40fd1bfd36b"
        },
        "date": 1783821298601,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 311025.76682692306,
            "unit": "ns",
            "range": "± 2507.930863158922"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 618353.7990373884,
            "unit": "ns",
            "range": "± 2739.919832765965"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3476574.5109375,
            "unit": "ns",
            "range": "± 36333.88910931545"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "91fb4aa99e6307a82c78674ee1b343ddd3e45755",
          "message": "Merge pull request #228 from Chris-Wolfgang/chore/nuget-trusted-publishing\n\nci(release): switch to NuGet Trusted Publishing (OIDC)",
          "timestamp": "2026-07-12T08:23:51-04:00",
          "tree_id": "664d560bbbfc2c200eb2e2ed847139f353642527",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/91fb4aa99e6307a82c78674ee1b343ddd3e45755"
        },
        "date": 1783861067034,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 390469.94200721156,
            "unit": "ns",
            "range": "± 1937.6982054947498"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 793279.5231584822,
            "unit": "ns",
            "range": "± 4877.620927715769"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4581071.507211538,
            "unit": "ns",
            "range": "± 37990.93194899802"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3dc4e6b24c973fc0e20a25589091ca101ea9068e",
          "message": "Merge pull request #274 from Chris-Wolfgang/ci/sourcelink-verify\n\nci: add SourceLink verification workflow",
          "timestamp": "2026-07-18T16:41:33-04:00",
          "tree_id": "77949d86a6daea94dfaaf0517724650a810bcc75",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/3dc4e6b24c973fc0e20a25589091ca101ea9068e"
        },
        "date": 1784554979950,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 392195.7051532452,
            "unit": "ns",
            "range": "± 2189.557767384977"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 802059.5005580357,
            "unit": "ns",
            "range": "± 5775.118899210269"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4596318.246651785,
            "unit": "ns",
            "range": "± 42702.40808661581"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "97d22f33055756a720c3c0c6b11b4b7a0f1cdf4c",
          "message": "Merge pull request #284 from Chris-Wolfgang/release/v0.7.0-code\n\nrelease: v0.7.0 code changes (workflows excluded, full CI)",
          "timestamp": "2026-08-08T07:27:52-04:00",
          "tree_id": "c5f2cc1201008e2b3764f6eac24a3d228946292f",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/97d22f33055756a720c3c0c6b11b4b7a0f1cdf4c"
        },
        "date": 1786190284230,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 430195.1057942708,
            "unit": "ns",
            "range": "± 4198.797112757292"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 875781.8604213169,
            "unit": "ns",
            "range": "± 7669.276161577647"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5078502.6859375,
            "unit": "ns",
            "range": "± 58569.66976537667"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9769222bce5a268db669aacc33c008e532241be3",
          "message": "Merge pull request #307 from Chris-Wolfgang/fix/apicompat-suppressions-xml\n\nfix(apicompat): convert compat-suppressions.txt to XML — unblocks v0.7.0 publish",
          "timestamp": "2026-08-08T08:30:54-04:00",
          "tree_id": "e59dec13a365e09341f9643a9f35bce3a682fe53",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/9769222bce5a268db669aacc33c008e532241be3"
        },
        "date": 1786195038056,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 432272.95570591517,
            "unit": "ns",
            "range": "± 2318.5697632151723"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 885688.8317522322,
            "unit": "ns",
            "range": "± 5242.2158682962"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5083451.8453125,
            "unit": "ns",
            "range": "± 62794.06004773229"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7db127be825b8bfc1c4da94c1443d0500dcfdf62",
          "message": "Merge pull request #311 from Chris-Wolfgang/ci/enable-package-validation\n\nci(packagevalidation): enable PackageValidation gate",
          "timestamp": "2026-08-09T16:16:57-04:00",
          "tree_id": "554b2fa9d0d54ff38e30f377c45e0d8f6679b85e",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/7db127be825b8bfc1c4da94c1443d0500dcfdf62"
        },
        "date": 1786309309126,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 427834.0059291295,
            "unit": "ns",
            "range": "± 6176.924165120621"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 868319.3635817308,
            "unit": "ns",
            "range": "± 4419.72605827171"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5127455.142708333,
            "unit": "ns",
            "range": "± 58599.022504728244"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "210299580+Chris-Wolfgang@users.noreply.github.com",
            "name": "Chris Wolfgang",
            "username": "Chris-Wolfgang"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "cd9a209e893ee6a0869435fb80379da276b8ac59",
          "message": "Merge pull request #319 from Chris-Wolfgang/vNext\n\nvNext: reproducible-build fix + shadow testing + SQLitePCLRaw GHSA fix",
          "timestamp": "2026-08-11T13:03:23-04:00",
          "tree_id": "4e65e690e5adc5d13812d44239ed87ac6fda2ed1",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/cd9a209e893ee6a0869435fb80379da276b8ac59"
        },
        "date": 1786469816385,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 385242.21026141825,
            "unit": "ns",
            "range": "± 2800.7825909386397"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 797040.2098858173,
            "unit": "ns",
            "range": "± 1817.0575833555486"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4697863.878645834,
            "unit": "ns",
            "range": "± 76806.50739726888"
          }
        ]
      }
    ]
  }
}