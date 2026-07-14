#!/usr/bin/env bash
# git-safe.sh — fuse/virtiofs マウント上の .git/index 破損を回避して git を実行するラッパー。
# 原因と詳細: docs/DEV_GIT_NOTES.md
#
# インデックスをローカル ext4 (/tmp) に置く。履歴(.git/objects, refs)はマウント上のまま＝安全。
# 使い方:
#   scripts/git-safe.sh add -A
#   scripts/git-safe.sh commit -m "message"
#   scripts/git-safe.sh status
# 初回や不整合時は HEAD からインデックスを再生成してから実行する。
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# ローカル ext4 上のインデックス（マウント外）。
export GIT_INDEX_FILE="${ACBR_GIT_INDEX:-/tmp/acbr_git_index}"

# インデックスが無い／壊れている場合は HEAD から再生成（履歴には触れない）。
if [ ! -s "$GIT_INDEX_FILE" ] || ! git ls-files >/dev/null 2>&1; then
  git read-tree HEAD 2>/dev/null || true
fi

exec git "$@"
