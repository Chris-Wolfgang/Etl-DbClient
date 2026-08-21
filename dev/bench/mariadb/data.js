window.BENCHMARK_DATA = {
  "lastUpdate": 1787329900126,
  "repoUrl": "https://github.com/Chris-Wolfgang/Etl-DbClient",
  "entries": {
    "ExtractorBenchmarks (mariadb)": [
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
        "date": 1779066636631,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 2394547.172265625,
            "unit": "ns",
            "range": "± 1042519.9680409464"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 1837740.046875,
            "unit": "ns",
            "range": "± 7780.964699350235"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 7651257.22015625,
            "unit": "ns",
            "range": "± 1187294.7078838735"
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
        "date": 1782144277261,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1245218.963641827,
            "unit": "ns",
            "range": "± 10636.386910694859"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 1818300.4575520833,
            "unit": "ns",
            "range": "± 18492.34681264891"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 9004075.502083333,
            "unit": "ns",
            "range": "± 151224.10290329717"
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
        "date": 1782151209243,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1200300.1999162945,
            "unit": "ns",
            "range": "± 9929.338721681612"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 4021123.0013020835,
            "unit": "ns",
            "range": "± 84377.14384901802"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6733048.09375,
            "unit": "ns",
            "range": "± 123676.81993555455"
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
        "date": 1782162102857,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1157310.9427584135,
            "unit": "ns",
            "range": "± 11910.07791959731"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 4176067.2421875,
            "unit": "ns",
            "range": "± 122815.05777580208"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6393488.203645834,
            "unit": "ns",
            "range": "± 77986.99525174353"
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
        "date": 1783821788272,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1233977.1643629808,
            "unit": "ns",
            "range": "± 5847.039571260153"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3394369.45203125,
            "unit": "ns",
            "range": "± 1191133.7399369685"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6980425.102083334,
            "unit": "ns",
            "range": "± 128314.94436672931"
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
        "date": 1783861555943,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1279698.8821614583,
            "unit": "ns",
            "range": "± 9556.234802251549"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 1920410.8299479166,
            "unit": "ns",
            "range": "± 16310.693471316126"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 11240890.825,
            "unit": "ns",
            "range": "± 164927.56174495493"
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
        "date": 1784555484615,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1166916.3739483173,
            "unit": "ns",
            "range": "± 9103.358031121365"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 4979096.4384375,
            "unit": "ns",
            "range": "± 532883.7088082847"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6823017.301041666,
            "unit": "ns",
            "range": "± 111941.62081162636"
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
        "date": 1786190731532,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1202382.185546875,
            "unit": "ns",
            "range": "± 3002.374637044677"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3909696.9670758927,
            "unit": "ns",
            "range": "± 54312.580665234615"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 9100645.916294644,
            "unit": "ns",
            "range": "± 137227.68892110055"
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
        "date": 1786195537228,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1166612.4711538462,
            "unit": "ns",
            "range": "± 5533.757287133069"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 4728355.9269255055,
            "unit": "ns",
            "range": "± 307258.6594941969"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 6627417.920833333,
            "unit": "ns",
            "range": "± 86826.23394174025"
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
        "date": 1786309786238,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 885566.802734375,
            "unit": "ns",
            "range": "± 8340.490651836571"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3276935.086111111,
            "unit": "ns",
            "range": "± 124446.88917439594"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5081594.89983259,
            "unit": "ns",
            "range": "± 55072.59162850092"
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
        "date": 1786470310588,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 233426.8115641276,
            "unit": "ns",
            "range": "± 5791.574225974403"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 736615.4547991072,
            "unit": "ns",
            "range": "± 9997.087738842683"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4827885.867847711,
            "unit": "ns",
            "range": "± 229850.30839432016"
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
        "date": 1786646501777,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 298676.4259207589,
            "unit": "ns",
            "range": "± 2337.7980522427247"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 803863.059375,
            "unit": "ns",
            "range": "± 6076.718704887869"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 4984608.981829354,
            "unit": "ns",
            "range": "± 268379.0266831592"
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
        "date": 1787329898222,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 239992.72098214287,
            "unit": "ns",
            "range": "± 3118.161338746052"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 654032.7728097098,
            "unit": "ns",
            "range": "± 4112.094443327513"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 3938124.7524857954,
            "unit": "ns",
            "range": "± 122377.6683813904"
          }
        ]
      }
    ]
  }
}