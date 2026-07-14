# 開発メモ: `.git/index` 破損の原因と回避策

## 症状
Cowork/Linux サンドボックスから `git add` / `git commit` すると、不定期に
`error: bad signature 0x00000000 / fatal: index file corrupt` が発生する。
`ls` ではインデックスが見えるのに `head`/`cp` が「No such file」を返す、といった不整合も起きる。

## 原因（特定済み・2026-06-14）
このリポジトリの作業ツリーと `.git` は、Windows ホストのフォルダを **virtiofs/fuse** で
Linux サンドボックスへマウントしたもの（`mount` で `type fuse` / `fuseblk`）。
fuse 層が **`.git/index` の書き込み後の read-after-write 一貫性を保証しない**ため、
git があるプロセスで書いたインデックスを、次の git プロセスがゼロ埋め（先頭 `DIRC` 署名が壊れた状態）で
読み、`bad signature` になる。git 2.34 では `core.fsync=index` も無い。

重要:
- **ゲーム本体・コミット履歴には影響しない**。`.git/objects` / `refs` は内容アドレスで一度書けば壊れにくく、
  `git fsck` もクリーン。Windows ホスト側の git は NTFS をネイティブに読むため一貫性があり正常。
- 壊れるのは「インデックス（ステージング領域）」だけ。インデックスは `HEAD` からいつでも再生成でき、
  **履歴を一切含まない**ので、マウント外に置いても安全。

## 回避策（恒久・サンドボックスから git を使うとき）
インデックスをローカル ext4（`/tmp`）に置いて git を実行する。`scripts/git-safe.sh` を用意。

```bash
# 例
scripts/git-safe.sh add -A
scripts/git-safe.sh commit -m "..."
# または素の git に環境変数を渡す
GIT_INDEX_FILE=/tmp/acbr_git_index git read-tree HEAD   # 初回だけHEADから再生成
GIT_INDEX_FILE=/tmp/acbr_git_index git add <paths>
GIT_INDEX_FILE=/tmp/acbr_git_index git commit -m "..."
```

- マウント上の `.git/index` には触れない（＝壊さない）。Windows 側の git はそのまま使える。
- もしマウント上の `.git/index` が壊れたら、履歴は無事なので次のように修復するだけ:
  ```bash
  GIT_INDEX_FILE=/tmp/acbr_git_index git read-tree HEAD
  rm -f .git/index && cp /tmp/acbr_git_index .git/index && sync
  ```

## 適用済みの git 設定（軽減策・無害）
`core.preloadindex=false` / `index.threads=1` / `core.untrackedCache=false` /
`core.fsmonitor=false` / `core.fscache=false`。
ただし fuse の read-after-write 非一貫性は設定だけでは消えないため、上記のローカルインデックス運用が本命。
