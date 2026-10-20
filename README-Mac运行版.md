# Mac 独立运行版

把 Unity 校园项目打包成**双击即玩**的 macOS 应用。日常启动不打开 Unity 编辑器、不重新构建、不启动本地服务器。

---

## 一、游戏在哪

```
Builds/macOS/风起校园.app       （约 1.1 GB）
```

- 架构：**通用二进制**（arm64 + x86_64），Apple Silicon 原生运行
- 启动场景：`Assets/Scenes/CampusLiving.unity`（校园主场景，构建列表第 1 个）
- 内含 **13 个场景**：校园主场景 + 图书馆、食堂、体育馆、教学楼 A/B/C、宿舍 1/2/西门/4/5/6 的室内场景，以及全部资源、中文字体与脚本
- `Builds/` 已在 `.gitignore` 中，不会提交

实测启动日志确认：Metal 设备初始化正常（Apple M5 Pro），Mono 程序集加载正常，无异常。

---

## 二、平时怎么启动

**双击项目根目录的：**

```
启动游戏.command
```

脚本会：定位项目根目录（兼容空格和中文路径）→ 检查 `.app` 是否存在（不存在会明确提示先构建）→ 清除 macOS 下载隔离标记 → 打开游戏 → 确认进程真的起来了。

**桌面快捷方式**（沙盒不允许我写你的桌面，请执行一次）：

```bash
ln -s "/Users/iewuui/Desktop/数媒比赛/启动游戏.command" ~/Desktop/风起校园.command
```

之后双击桌面上的 `风起校园.command` 即可。

**游戏内退出**：按 `ESC` 打开暂停菜单 → 点「**退出游戏**」。

---

## 三、修改后如何更新运行版

**独立应用是构建那一刻的快照。** 改了代码、场景或资源后，必须重新构建，`.app` 里才会有新内容。

**A. 双击项目根目录的：**

```
构建游戏.command
```

**B. 或在 Unity 编辑器菜单**：`Campus > Mac > Build macOS App`

> 构建前请先在 Unity 里 **Cmd+S 保存** 场景和项目。脚本不会替你保存未保存的编辑器修改。

构建日志：`Logs/mac-build.log`

### 关于构建缓存（重要）

Unity 构建时需要写 `~/Library/Caches/com.unity3d.UnityEditor/`（着色器与光照缓存）。在受限环境里该目录可能不可写，构建会卡在同一进度、日志里刷 `Failed to create GICache directory`。

`构建游戏.command` 已内置解决方式——把 Unity 的「用户主目录」用 `CFFIXED_USER_HOME` 重定向到临时目录：

```bash
CFFIXED_USER_HOME=/tmp/campus-unity-home-$$ \
HOME=/tmp/campus-unity-home-$$ \
"$UNITY" -batchmode -quit -projectPath "$ROOT" \
  -logFile "$ROOT/Logs/mac-build.log" \
  -executeMethod MacPlayerBuilder.BuildFromCommandLine -buildTarget OSX
```

这个变量是 macOS CoreFoundation 的标准机制，会覆盖 `NSHomeDirectory()`，而 Unity 的缓存路径正是基于它——这是让构建在受限环境里跑通的关键。

---

## 四、技术细节

| 项目 | 值 |
|---|---|
| 构建入口 | `Assets/Editor/MacPlayerBuilder.cs` |
| 菜单 | `Campus/Mac/Build macOS App` |
| 命令行 | `-executeMethod MacPlayerBuilder.BuildFromCommandLine -buildTarget OSX` |
| 构建目标 | `BuildTarget.StandaloneOSX`，Development 构建 |
| 场景来源 | `EditorBuildSettings` 中所有已启用场景（当前 13 个） |
| 应用显示名 | 构建后自动把 `Info.plist` 的 `CFBundleName` / `CFBundleDisplayName` 改为「风起校园」 |
| 暂停菜单 | 新增「退出游戏」按钮（`CampusGame.cs`），保存进度后 `Application.Quit()` |

---

## 五、常见问题

**双击 `.command` 提示没有执行权限**
```bash
chmod +x "/Users/iewuui/Desktop/数媒比赛/启动游戏.command" \
         "/Users/iewuui/Desktop/数媒比赛/构建游戏.command"
```

**双击 `.app` 提示"无法验证开发者"**
`启动游戏.command` 会自动执行 `xattr -dr com.apple.quarantine`；手动方式：
```bash
xattr -dr com.apple.quarantine "/Users/iewuui/Desktop/数媒比赛/Builds/macOS/风起校园.app"
```

**构建卡住不动**
确认 Unity 编辑器已关闭（同一工程不能被两个 Unity 实例打开），并查看 `Logs/mac-build.log` 末尾。
