#!/bin/bash
# 风起 · 校园 —— 双击启动独立运行版
# 只负责打开已经构建好的游戏；不启动 Unity、不重新构建、不启动本地服务器。

set -u

# 定位本脚本所在目录（项目根目录），兼容路径中的空格与中文
cd "$(dirname "$0")" || exit 1
ROOT="$(pwd)"

APP="$ROOT/Builds/macOS/风起校园.app"

if [ ! -d "$APP" ]; then
  echo "=============================================================="
  echo "  未找到独立运行版：$APP"
  echo "=============================================================="
  echo
  echo "请先构建一次 Mac 运行版，任选其一："
  echo
  echo "  A. 双击本项目根目录的「构建游戏.command」"
  echo
  echo "  B. 在 Unity 编辑器菜单：Campus > Mac > Build macOS App"
  echo
  echo "详细说明见：$ROOT/README-Mac运行版.md"
  echo
  read -r -p "按回车键关闭…" _
  exit 1
fi

# 首次运行时清掉下载隔离标记，避免「无法验证开发者」拦截
if command -v xattr >/dev/null 2>&1; then
  xattr -dr com.apple.quarantine "$APP" >/dev/null 2>&1 || true
fi

echo "正在启动 风起 · 校园 …"
open "$APP"

# 确认进程真的起来了，避免静默失败
sleep 2
if pgrep -f "风起校园.app/Contents/MacOS/" >/dev/null 2>&1; then
  echo "已启动。"
else
  echo "启动命令已发出，但没检测到游戏进程。"
  echo "如果窗口没有出现，请直接双击：$APP"
  read -r -p "按回车键关闭…" _
  exit 1
fi
exit 0
