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
- Automatic update check on launch
- Temporary launch boost so new Roblox instances can start faster before default limits are restored

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
- automatic update check on launch
- launch boost duration, core count, and priority

Click `Save defaults` to keep them for the next launch. The config is stored at `%AppData%\RobloxInstanceOptimizer\settings.json`.

## Auto Update

If `Auto-update on launch` is enabled, the app checks the GitHub `VERSION` file at startup.

When a newer version is available, the app starts the installer from:

```text
https://raw.githubusercontent.com/6usss/roblox-opti/main/install.ps1
```

The updater replaces the installed files in `%LOCALAPPDATA%\RobloxInstanceOptimizer`.

## Launch Boost

Roblox can start slowly if CPU limits are applied immediately. `Launch boost` gives newly detected Roblox instances temporary CPU settings before restoring the normal defaults.

Default behavior:

- boost for 60 seconds
- use up to 4 cores
- use `Normal` priority
- restore the configured default core count and priority after the timer expires

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

## Linux RAM-Only Version

The Linux version is RAM-only. It does not manage CPU cores, CPU priority, or launch boost.

It runs as a small systemd daemon and uses cgroups v2:

- `memory.high` for a soft RAM pressure limit
- optional `memory.max` for a hard cap
- automatic process detection by command line patterns

Install on a Linux VPS:

```bash
curl -fsSL https://raw.githubusercontent.com/6usss/roblox-opti/main/install-linux-ram.sh | sudo bash
```

If `curl` is not installed:

```bash
wget -qO- https://raw.githubusercontent.com/6usss/roblox-opti/main/install-linux-ram.sh | sudo bash
```

Config file:

```text
/etc/roblox-opti-linux/config.json
```

Default config:

```json
{
  "memoryHighMb": 2048,
  "memoryMaxMb": 0,
  "scanIntervalSeconds": 5,
  "processMatch": [
    "RobloxPlayerBeta.exe",
    "RobloxPlayerBeta",
    "sober",
    "vinegar",
    "grapejuice"
  ]
}
```

Useful commands:

```bash
sudo systemctl status roblox-opti-ram.service
sudo journalctl -u roblox-opti-ram.service -f
sudo nano /etc/roblox-opti-linux/config.json
sudo systemctl restart roblox-opti-ram.service
```

`memoryHighMb` is recommended for stability. `memoryMaxMb` is disabled by default because a hard memory cap can kill or crash the process if the value is too low.
