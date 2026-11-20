type: internal

Update Wolfgang.Etl.Abstractions, ErrorPolicies, TestKit and TestKit.Xunit to 0.27.0. Abstractions no longer brings System.ComponentModel.Annotations in transitively, so the library now references it directly on .NET Framework and netstandard2.0.
