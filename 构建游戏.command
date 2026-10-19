#!/bin/bash
# 风起 · 校园 —— 重新构建 Mac 独立运行版
# 修改了代码 / 场景 / 资源之后，双击本文件即可把最新内容打包进独立运行版。
# 构建过程不打开 Unity 编辑器界面，但需要本机安装 Unity 6000.3.26f1。

set -u

cd "$(dirname "$0")" || exit 1
ROOT="$(pwd)"

UNITY="/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity"
if [ ! -x "$UNITY" ]; then
  echo "未找到 Unity：$UNITY"
  echo "请确认已安装 Unity 6000.3.26f1，或修改本脚本开头的 UNITY 路径。"
  read -r -p "按回车键关闭…" _
  exit 1
fi

echo "=============================================================="
echo "  构建前请确认：Unity 编辑器里已经保存了全部修改"
echo "  （Cmd+S 保存场景 / 项目），否则构建出来的是旧内容。"
echo "=============================================================="
echo
echo "项目：$ROOT"
echo "输出：$ROOT/Builds/macOS/风起校园.app"
echo
echo "开始构建，通常需要 5–15 分钟，请勿关闭本窗口…"
echo

# Unity 的着色器 / 光照缓存在 ~/Library/Caches 下。把它的「用户主目录」重定向到
# 一个临时目录，既避免污染你的真实缓存，也让各种受限环境都能正常构建。
CACHE_HOME="/tmp/campus-unity-home-$$"
mkdir -p "$CACHE_HOME/Library/Caches" \
         "$CACHE_HOME/Library/Preferences" \
         "$CACHE_HOME/Library/Logs" \
         "$CACHE_HOME/Library/Application Support"

CFFIXED_USER_HOME="$CACHE_HOME" \
HOME="$CACHE_HOME" \
"$UNITY" -batchmode -quit -projectPath "$ROOT" \
  -logFile "$ROOT/Logs/mac-build.log" \
  -executeMethod MacPlayerBuilder.BuildFromCommandLine \
  -buildTarget OSX
STATUS=$?

rm -rf "$CACHE_HOME" 2>/dev/null || true

echo
if [ $STATUS -eq 0 ] && [ -d "$ROOT/Builds/macOS/风起校园.app" ]; then
  echo "✅ 构建完成：$ROOT/Builds/macOS/风起校园.app"
  echo "   之后双击「启动游戏.command」即可游玩。"
  open -R "$ROOT/Builds/macOS/风起校园.app" 2>/dev/null || true
else
  echo "❌ 构建失败（退出码 $STATUS）。"
  echo "   详细日志：$ROOT/Logs/mac-build.log"
fi
echo
read -r -p "按回车键关闭…" _
exit $STATUS
