type: feature

Built against `Wolfgang.Etl.Abstractions` 0.25.0 (and TestKit / TestKit.Xunit 0.25.0): the base-stage `ReportingInterval` / `MaximumItemCount` / `SkipItemCount` setters are deprecated fleet-wide in favour of the options record, and `IncrementCurrentItemCount(int)` / `IncrementCurrentSkippedItemCount(int)` are available to derived stages.
