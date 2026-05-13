# Roblox Instance Optimizer

Windows WPF tool for managing multiple Roblox instances.

## MVP Features

- Detects `RobloxPlayerBeta.exe` processes
- Shows PID, window name, current RAM, and CPU affinity
- Sets CPU priority
- Allocates logical CPU cores per instance
- Mem Reduct-style automatic RAM cleanup via `EmptyWorkingSet`
- Per-instance RAM threshold: when Roblox goes above the threshold, the app asks Windows to trim active memory
- Saved default settings applied automatically to new instances
- Automatic CPU core distribution across Roblox instances

## Usage

1. Start Roblox.
2. Run `RobloxInstanceOptimizer.App` as administrator.
3. Click `Scan`.
4. Set the RAM cleanup threshold in MB, core count, and CPU priority.
5. Click `Apply selected` or `Apply all`.

## Default Settings

The defaults panel controls values applied to new Roblox instances:

- RAM cleanup threshold
- core count
- CPU priority
- auto-scan
- automatic RAM cleanup
- automatic application to new instances
- automatic CPU core distribution

Click `Save defaults` to keep them for the next launch. The config is stored at `%AppData%\RobloxInstanceOptimizer\settings.json`.

## Automatic CPU Distribution

If `Auto-distribute cores` is enabled, the app assigns a different CPU core range to each instance.

Example with 36 cores and `Cores = 2`:

- instance 1: cores 0-1
- instance 2: cores 2-3
- instance 3: cores 4-5
- etc.

When all available ranges are used, the app loops back to the beginning.

Recommended starting values:

- Main account: 2048 to 3072 MB, 2 to 4 cores, `Normal` priority
- Lightweight alt: 1536 to 2048 MB, 1 to 2 cores, `BelowNormal` priority
- Avoid going below 1536 MB unless the game is very lightweight

## RAM Cleanup Note

The default mode does not hard-limit RAM. It uses `EmptyWorkingSet`, an approach similar to Mem Reduct.

When Roblox goes above the selected threshold, the app asks Windows to trim part of the process active memory. This is more stable than a hard limit, but it is not an absolute cap: Roblox can grow again, so the app repeats cleanup automatically.

A true Job Object hard limit can crash Roblox if the game needs more memory. That code is kept for future experimentation, but the interface now uses automatic RAM cleanup.
