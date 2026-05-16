#!/usr/bin/env bash
set -euo pipefail

REPO_OWNER="6usss"
REPO_NAME="roblox-opti"
BRANCH="main"
INSTALL_BIN="/usr/local/bin/roblox-opti-ram"
CONFIG_DIR="/etc/roblox-opti-linux"
CONFIG_FILE="$CONFIG_DIR/config.json"
SERVICE_FILE="/etc/systemd/system/roblox-opti-ram.service"
TMP_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

step() {
  printf '\033[36m==> %s\033[0m\n' "$1"
}

require_root() {
  if [[ "${EUID}" -ne 0 ]]; then
    echo "Please run as root: sudo bash install-linux-ram.sh"
    exit 1
  fi
}

require_cgroup_v2() {
  if [[ ! -f /sys/fs/cgroup/cgroup.controllers ]]; then
    echo "cgroups v2 is required. /sys/fs/cgroup/cgroup.controllers was not found."
    exit 1
  fi
}

download_repo() {
  local zip_path="$TMP_DIR/repo.zip"
  local url="https://github.com/$REPO_OWNER/$REPO_NAME/archive/refs/heads/$BRANCH.zip"

  if command -v curl >/dev/null 2>&1; then
    curl -fsSL "$url" -o "$zip_path"
  elif command -v wget >/dev/null 2>&1; then
    wget -q "$url" -O "$zip_path"
  else
    echo "curl or wget is required."
    exit 1
  fi

  if command -v unzip >/dev/null 2>&1; then
    unzip -q "$zip_path" -d "$TMP_DIR"
  else
    echo "unzip is required."
    exit 1
  fi
}

require_root
require_cgroup_v2

step "Downloading $REPO_OWNER/$REPO_NAME"
download_repo

SOURCE_DIR="$TMP_DIR/$REPO_NAME-$BRANCH"

step "Installing RAM manager"
install -m 0755 "$SOURCE_DIR/linux/roblox-opti-ram.py" "$INSTALL_BIN"

step "Installing config"
mkdir -p "$CONFIG_DIR"
if [[ ! -f "$CONFIG_FILE" ]]; then
  install -m 0644 "$SOURCE_DIR/linux/config.example.json" "$CONFIG_FILE"
else
  echo "Keeping existing config: $CONFIG_FILE"
fi

step "Installing systemd service"
install -m 0644 "$SOURCE_DIR/linux/roblox-opti-ram.service" "$SERVICE_FILE"
systemctl daemon-reload
systemctl enable --now roblox-opti-ram.service

step "Done"
echo "Config: $CONFIG_FILE"
echo "Service: roblox-opti-ram.service"
echo ""
echo "Useful commands:"
echo "  sudo systemctl status roblox-opti-ram.service"
echo "  sudo journalctl -u roblox-opti-ram.service -f"
echo "  sudo nano $CONFIG_FILE"
echo "  sudo systemctl restart roblox-opti-ram.service"
