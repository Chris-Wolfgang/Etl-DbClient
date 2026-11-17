type: fix

`CommandTimeout` now rounds up to whole seconds and clamps to the `int` range, so a sub-second timeout no longer becomes 0 (no timeout).
