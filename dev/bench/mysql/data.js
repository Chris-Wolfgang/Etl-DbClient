window.BENCHMARK_DATA = {
  "lastUpdate": 1783861424947,
  "repoUrl": "https://github.com/Chris-Wolfgang/Etl-DbClient",
  "entries": {
    "ExtractorBenchmarks (mysql)": [
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
        "date": 1779066352935,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1186706.82421875,
            "unit": "ns",
            "range": "± 9733.046710356608"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3057554.2471875,
            "unit": "ns",
            "range": "± 73159.40990688442"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 7786234.280975877,
            "unit": "ns",
            "range": "± 278324.4594304826"
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
        "date": 1782144145468,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1544960.3924278845,
            "unit": "ns",
            "range": "± 13076.352597513394"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2276933.466045673,
            "unit": "ns",
            "range": "± 11770.485214199847"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 8017040.1074892245,
            "unit": "ns",
            "range": "± 224275.7569686742"
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
        "date": 1782145843159,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1530384.9771634615,
            "unit": "ns",
            "range": "± 11597.911198763772"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2265547.1121651786,
            "unit": "ns",
            "range": "± 8645.327484567815"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 7852663.051988636,
            "unit": "ns",
            "range": "± 182205.3812551547"
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
        "date": 1782151046923,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1086712.622767857,
            "unit": "ns",
            "range": "± 13257.646906832217"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 3305132.102114899,
            "unit": "ns",
            "range": "± 819440.1184328686"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5684579.7219827585,
            "unit": "ns",
            "range": "± 153923.66845570388"
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
        "date": 1782161944534,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1416641.7940848214,
            "unit": "ns",
            "range": "± 12836.637749626674"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2928498.38953125,
            "unit": "ns",
            "range": "± 1180720.2816320346"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 7269300.713169643,
            "unit": "ns",
            "range": "± 177772.31580444536"
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
        "date": 1783821609323,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1545457.9798677885,
            "unit": "ns",
            "range": "± 6262.987384011898"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2391926.375300481,
            "unit": "ns",
            "range": "± 14600.80421714304"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 8527885.469594594,
            "unit": "ns",
            "range": "± 243809.45749386525"
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
        "date": 1783861423000,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 1414828.3350360577,
            "unit": "ns",
            "range": "± 8638.934630042068"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 2948950.404375,
            "unit": "ns",
            "range": "± 1193268.901938842"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 7495717.0632267445,
            "unit": "ns",
            "range": "± 137084.89782274285"
          }
        ]
      }
    ]
  }
}