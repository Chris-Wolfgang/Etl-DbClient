window.BENCHMARK_DATA = {
  "lastUpdate": 1790130995636,
  "repoUrl": "https://github.com/Chris-Wolfgang/Etl-DbClient",
  "entries": {
    "ExtractorBenchmarks (postgres)": [
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
        "date": 1778986391789,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 3011074.285714286,
            "unit": "ns",
            "range": "± 15372.466017172537"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3299706.089583333,
            "unit": "ns",
            "range": "± 21339.212103950114"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6122226.747767857,
            "unit": "ns",
            "range": "± 17723.809437837914"
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
        "date": 1779066195029,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2865158.1768973214,
            "unit": "ns",
            "range": "± 19032.180745851347"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3194380.115104167,
            "unit": "ns",
            "range": "± 24267.835370214496"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6668328.724330357,
            "unit": "ns",
            "range": "± 39887.69058460262"
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
        "date": 1782143968999,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2998042.32421875,
            "unit": "ns",
            "range": "± 14882.91426158797"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3303928.4010416665,
            "unit": "ns",
            "range": "± 40149.01695334189"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6155629.2953125,
            "unit": "ns",
            "range": "± 64499.791632666085"
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
        "date": 1782145672134,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2864073.3903459823,
            "unit": "ns",
            "range": "± 23788.378666797835"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3202733.8515625,
            "unit": "ns",
            "range": "± 20100.706239130603"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6665730.649479167,
            "unit": "ns",
            "range": "± 55440.244742222276"
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
        "date": 1782150861319,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2301707.904166667,
            "unit": "ns",
            "range": "± 15535.723571772045"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2549656.2921316964,
            "unit": "ns",
            "range": "± 20657.35842128644"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4721125.5234375,
            "unit": "ns",
            "range": "± 38001.38872867079"
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
        "date": 1782161731520,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2795341.533761161,
            "unit": "ns",
            "range": "± 9476.340695265102"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3114520.5150240385,
            "unit": "ns",
            "range": "± 15365.442266244307"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6463987.340104166,
            "unit": "ns",
            "range": "± 28892.74994649662"
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
        "date": 1783821436246,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2929000.5658854167,
            "unit": "ns",
            "range": "± 12819.749805022306"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3253837.8450520835,
            "unit": "ns",
            "range": "± 14953.672132826385"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6814587.0453125,
            "unit": "ns",
            "range": "± 44221.410960977664"
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
        "date": 1783861193932,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2457923.793526786,
            "unit": "ns",
            "range": "± 11313.527694489012"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2743042.4196428573,
            "unit": "ns",
            "range": "± 18729.660780600105"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5863866.118489583,
            "unit": "ns",
            "range": "± 47337.10787632131"
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
        "date": 1784555133322,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2902558.4888020833,
            "unit": "ns",
            "range": "± 26443.828991326107"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3299577.4192708335,
            "unit": "ns",
            "range": "± 57848.080213963534"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6838914.371354166,
            "unit": "ns",
            "range": "± 107338.1980187704"
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
        "date": 1786190423272,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2857020.8661458334,
            "unit": "ns",
            "range": "± 20367.67081165955"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3185402.6744791665,
            "unit": "ns",
            "range": "± 21716.49098585885"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6771663.3765625,
            "unit": "ns",
            "range": "± 45353.037794936565"
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
        "date": 1786195176008,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2822137.2061941964,
            "unit": "ns",
            "range": "± 19891.799240049764"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3156963.1010416667,
            "unit": "ns",
            "range": "± 23730.109683639017"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6575487.517857143,
            "unit": "ns",
            "range": "± 25860.84011162874"
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
        "date": 1786309449926,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2800322.091796875,
            "unit": "ns",
            "range": "± 12871.992728058325"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3130960.4871651786,
            "unit": "ns",
            "range": "± 15592.411056278344"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6567219.887834822,
            "unit": "ns",
            "range": "± 28855.80230959642"
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
        "date": 1786469969970,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 250106.57092285156,
            "unit": "ns",
            "range": "± 1050.8428038947166"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 541609.502092634,
            "unit": "ns",
            "range": "± 7849.749575198348"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3312863.451041667,
            "unit": "ns",
            "range": "± 58364.56705897653"
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
          "id": "a4d67852a1051e2abdffb5a1128cb51a9ab5cb09",
          "message": "Merge pull request #333 from Chris-Wolfgang/vNext\n\nRelease 0.9.0",
          "timestamp": "2026-08-13T14:26:10-04:00",
          "tree_id": "8c065a53ed0e335cb7b5e732d186fbab594bf142",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/a4d67852a1051e2abdffb5a1128cb51a9ab5cb09"
        },
        "date": 1786646160538,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 158779.98072916668,
            "unit": "ns",
            "range": "± 2035.754917845946"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 333755.9272085336,
            "unit": "ns",
            "range": "± 2484.0799547061083"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 2150780.5755974264,
            "unit": "ns",
            "range": "± 43668.385677476384"
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
          "id": "2759c09c3ac5da67fdb20a1ce085df230a788275",
          "message": "Merge pull request #344 from Chris-Wolfgang/vNext\n\nRelease v0.9.1 — code-scanning alert sweep",
          "timestamp": "2026-08-21T08:38:57-04:00",
          "tree_id": "4dd6f16fa71022db4d7894e7aec82427c8034991",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/2759c09c3ac5da67fdb20a1ce085df230a788275"
        },
        "date": 1787329579977,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 187816.16744290866,
            "unit": "ns",
            "range": "± 7642.789036722668"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 429577.53802083334,
            "unit": "ns",
            "range": "± 3356.145692614899"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 2983104.0170898438,
            "unit": "ns",
            "range": "± 56929.84144350135"
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
          "id": "6a05e3f7649da16741892650411cd094ee78a396",
          "message": "Merge pull request #405 from Chris-Wolfgang/vNext\n\nRelease v0.10.0 — options-record constructors, ORM-independent parameters, paging presets",
          "timestamp": "2026-09-05T09:53:41-04:00",
          "tree_id": "36aad2c86a8aeb73f4095f12d7b81dbd77ceb175",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/6a05e3f7649da16741892650411cd094ee78a396"
        },
        "date": 1788616884836,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 278780.10993840144,
            "unit": "ns",
            "range": "± 1358.56060021036"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 601574.955078125,
            "unit": "ns",
            "range": "± 2582.2704599832823"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3855853.260569853,
            "unit": "ns",
            "range": "± 65226.721234125645"
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
          "id": "cb69a0a2f9bbd3ac1e3d8eac8419384aff6e14c9",
          "message": "docs: examples and samples construct DbExtractor / DbLoader through the options record (#422) (#423)\n\n* docs: every example and sample constructs DbExtractor / DbLoader through the options record (#422)\n\nEvery constructor call in the README, the five example projects and the\nShadowConsumer sample bound an [Obsolete] overload (the record\nconstructors take the record positionally and none was passed) or set a\ndeprecated property in an object initializer. All now pass\nDbExtractorOptions / DbLoaderOptions: CommandTimeout, InsertBatchSize,\nReportingInterval, ValidateSchemaOnStart, and the paging trio in the\nsample. Every example and sample project builds with CS0618 un-\nsuppressed; the four runnable examples run.\n\nCloses #422\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n\n* docs(examples): drop the six file-level CS0618 suppressions and their stale justification (review on #423)\n\nEvery example and sample now builds with warnings as errors and no suppression, so a future deprecated binding fails the build instead of hiding.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n\n---------\n\nCo-authored-by: Claude Opus 5 <noreply@anthropic.com>",
          "timestamp": "2026-09-17T11:40:54-04:00",
          "tree_id": "b8aac9a351c99a2d1d6be1c8a160c229c0cf5e84",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/cb69a0a2f9bbd3ac1e3d8eac8419384aff6e14c9"
        },
        "date": 1789663567332,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 297412.2714146205,
            "unit": "ns",
            "range": "± 2488.3210969861398"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 620292.4538762019,
            "unit": "ns",
            "range": "± 2758.3026057256748"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3916351.894230769,
            "unit": "ns",
            "range": "± 42201.46482952573"
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
          "id": "7c325d5b4ef4a8d847afb81db1dcb707ea500327",
          "message": "release: ApiCompat suppressions for the 0.11.0 ADR-0009 breaks (release gate vs published 0.10.0) (#424)\n\nThe v0.11.0 release run failed at \"ApiCompat vs last NuGet\": the\nrelease-time gate runs the standalone apicompat tool against the\npublished package with compat-suppressions.txt, which is separate from\nthe PackageValidation CompatibilitySuppressions.xml and was empty. It\nalso reports CP0007 (DbExtractorOptions / DbLoaderOptions now derive\nfrom the Abstractions 0.24 base records) which PackageValidation did\nnot. Entries generated by replicating that step locally against the\npublished 0.10.0 package, per TFM, and merged: CP0007 x22, CP0008 x11\n(ISupportDryRun), CP0002 x10 (derived-record <Clone>$ on the pre-.NET 5\nTFMs). Every TFM passes the same command CI runs.\n\nCo-authored-by: Claude Opus 5 <noreply@anthropic.com>",
          "timestamp": "2026-09-17T13:45:30-04:00",
          "tree_id": "b2fa46e61399b8d246fcf8061d418719f5363306",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/7c325d5b4ef4a8d847afb81db1dcb707ea500327"
        },
        "date": 1789668214805,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 297778.69005408656,
            "unit": "ns",
            "range": "± 843.29877589638"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 631668.9271763393,
            "unit": "ns",
            "range": "± 2868.0462567432583"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3950337.4754464286,
            "unit": "ns",
            "range": "± 68548.33937551003"
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
          "id": "4ef8c8f78614168aada28cb590c14b7919720ece",
          "message": "ci(release): call the manifest-local tools as `dotnet <tool>` (#470)\n\nThe move from global tool installs to `.config/dotnet-tools.json` landed here, but the call\nsites did not move with it. A manifest-local tool is not on PATH, so after `dotnet tool restore`\nthe bare invocation dies with \"The term 'reportgenerator' is not recognized as a name of a\ncmdlet, function, script file, or executable program\" and the Validate job fails — taking Pack,\nDocs, Publish, Attest and Attach down as skipped with it.\n\nCaught by ETL-Csv's v0.10.0 release run (35800394348), which failed at \"Verify coverage\nthreshold\" after restoring all eight tools successfully.\n\nrepo-template already reads `dotnet tool restore` + `dotnet reportgenerator`; this catches the\nrepository up to it.\n\nCo-authored-by: Claude Opus 5 <noreply@anthropic.com>",
          "timestamp": "2026-09-22T21:03:19-04:00",
          "tree_id": "853bfdc9cf566b46c06468fb37034e306a6cf8f0",
          "url": "https://github.com/Chris-Wolfgang/Etl-DbClient/commit/4ef8c8f78614168aada28cb590c14b7919720ece"
        },
        "date": 1790130992828,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 294532.76998465403,
            "unit": "ns",
            "range": "± 1775.091085585782"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 621239.6457331731,
            "unit": "ns",
            "range": "± 4387.6082366560195"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3926540.440848214,
            "unit": "ns",
            "range": "± 37450.08652065014"
          }
        ]
      }
    ]
  }
}