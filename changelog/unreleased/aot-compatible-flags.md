type: feature

The net8.0 and later assemblies are now marked `IsTrimmable` and `IsAotCompatible`: the library is built with the trim, AOT and single-file analyzers on, and a trimmed application treats it as trim-compatible (it is trimmed even under `TrimMode=partial`).
