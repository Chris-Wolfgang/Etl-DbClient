window.BENCHMARK_DATA = {
  "lastUpdate": 1778986144829,
  "repoUrl": "https://github.com/Chris-Wolfgang/Etl-DbClient",
  "entries": {
    "ExtractorBenchmarks (sqlite)": [
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
        "date": 1778986143165,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 100)",
            "value": 62439.05718524639,
            "unit": "ns",
            "range": "± 111.43300706177634"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 1000)",
            "value": 556632.7953404018,
            "unit": "ns",
            "range": "± 2527.46741802202"
          },
          {
            "name": "Wolfgang.Etl.DbClient.Benchmarks.ExtractorBenchmarks.ExtractAsync(RecordCount: 10000)",
            "value": 5411117.636979166,
            "unit": "ns",
            "range": "± 39613.89614636281"
          }
        ]
      }
    ]
  }
}