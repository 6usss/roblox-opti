#!/usr/bin/env python3
import json
import os
import signal
import time
from dataclasses import dataclass
from pathlib import Path


DEFAULT_CONFIG_PATH = Path("/etc/roblox-opti-linux/config.json")
CGROUP_ROOT = Path("/sys/fs/cgroup")
APP_CGROUP = CGROUP_ROOT / "roblox-opti"


@dataclass
class Config:
    memory_high_mb: int = 2048
    memory_max_mb: int = 0
    scan_interval_seconds: int = 5
    process_match: tuple[str, ...] = (
        "RobloxPlayerBeta.exe",
        "RobloxPlayerBeta",
        "sober",
        "vinegar",
        "grapejuice",
    )


def load_config(path: Path) -> Config:
    if not path.exists():
        return Config()

    with path.open("r", encoding="utf-8") as handle:
        raw = json.load(handle)

    process_match = raw.get("processMatch", Config.process_match)
    if isinstance(process_match, str):
        process_match = [process_match]

    return Config(
        memory_high_mb=int(raw.get("memoryHighMb", Config.memory_high_mb)),
        memory_max_mb=int(raw.get("memoryMaxMb", Config.memory_max_mb)),
        scan_interval_seconds=max(1, int(raw.get("scanIntervalSeconds", Config.scan_interval_seconds))),
        process_match=tuple(str(item) for item in process_match),
    )


def write_text(path: Path, value: str) -> None:
    path.write_text(value, encoding="utf-8")


def read_cmdline(pid: int) -> str:
    try:
        data = Path(f"/proc/{pid}/cmdline").read_bytes()
        return data.replace(b"\x00", b" ").decode("utf-8", errors="ignore")
    except (FileNotFoundError, ProcessLookupError, PermissionError):
        return ""


def read_comm(pid: int) -> str:
    try:
        return Path(f"/proc/{pid}/comm").read_text(encoding="utf-8", errors="ignore").strip()
    except (FileNotFoundError, ProcessLookupError, PermissionError):
        return ""


def find_matching_pids(config: Config) -> list[int]:
    pids: list[int] = []
    for entry in Path("/proc").iterdir():
        if not entry.name.isdigit():
            continue

        pid = int(entry.name)
        cmdline = read_cmdline(pid)
        comm = read_comm(pid)
        haystack = f"{comm} {cmdline}".lower()

        if any(pattern.lower() in haystack for pattern in config.process_match):
            pids.append(pid)

    return sorted(set(pids))


def ensure_root_cgroup() -> None:
    APP_CGROUP.mkdir(exist_ok=True)


def apply_memory_limits(cgroup: Path, config: Config) -> None:
    memory_high = max(1, config.memory_high_mb) * 1024 * 1024
    write_text(cgroup / "memory.high", str(memory_high))

    if config.memory_max_mb > 0:
        memory_max = config.memory_max_mb * 1024 * 1024
        write_text(cgroup / "memory.max", str(memory_max))
    else:
        write_text(cgroup / "memory.max", "max")


def move_pid_to_cgroup(pid: int, config: Config) -> None:
    cgroup = APP_CGROUP / f"pid-{pid}"
    cgroup.mkdir(exist_ok=True)
    apply_memory_limits(cgroup, config)
    write_text(cgroup / "cgroup.procs", str(pid))


def cleanup_dead_cgroups(live_pids: set[int]) -> None:
    if not APP_CGROUP.exists():
        return

    for child in APP_CGROUP.iterdir():
        if not child.is_dir() or not child.name.startswith("pid-"):
            continue

        try:
            pid = int(child.name[4:])
        except ValueError:
            continue

        if pid in live_pids:
            continue

        try:
            child.rmdir()
        except OSError:
            pass


def verify_cgroup_v2() -> None:
    if not (CGROUP_ROOT / "cgroup.controllers").exists():
        raise RuntimeError("cgroups v2 is required. /sys/fs/cgroup/cgroup.controllers was not found.")
    if not os.access(CGROUP_ROOT, os.W_OK):
        raise RuntimeError("Root privileges are required to manage cgroups. Run as root or via systemd.")


def run_once(config: Config) -> int:
    ensure_root_cgroup()
    pids = find_matching_pids(config)
    for pid in pids:
        move_pid_to_cgroup(pid, config)

    cleanup_dead_cgroups(set(pids))
    return len(pids)


def daemon(config_path: Path) -> None:
    stop = False

    def handle_stop(_signum, _frame) -> None:
        nonlocal stop
        stop = True

    signal.signal(signal.SIGTERM, handle_stop)
    signal.signal(signal.SIGINT, handle_stop)

    verify_cgroup_v2()
    print("roblox-opti-linux RAM daemon started", flush=True)

    while not stop:
        config = load_config(config_path)
        count = run_once(config)
        print(f"managed {count} matching process(es)", flush=True)
        time.sleep(config.scan_interval_seconds)


def main() -> int:
    config_path = Path(os.environ.get("ROBLOX_OPTI_CONFIG", DEFAULT_CONFIG_PATH))
    mode = os.environ.get("ROBLOX_OPTI_MODE", "daemon")

    try:
        config = load_config(config_path)
        verify_cgroup_v2()
        if mode == "once":
            count = run_once(config)
            print(f"managed {count} matching process(es)")
            return 0

        daemon(config_path)
        return 0
    except Exception as exc:
        print(f"error: {exc}", flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
