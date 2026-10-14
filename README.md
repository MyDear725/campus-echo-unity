# 风起 · 校园 / Campus Echo Unity

以大连理工大学开发区校区为原型的第三人称 3D 校园探索项目。

这是从原 campus-demo 本地工程分离的独立 Unity 仓库，不包含 GitHub 网页版后来新增的学号、送学剧情、时间系统、宿舍分配或天桥功能。

## 打开项目

1. 安装 Unity **6000.3.26f1（Unity 6.3 LTS）**。
2. 使用 GitHub Desktop 克隆本仓库，确保 Git LFS 资源下载完成。
3. Unity Hub → 添加 → 从磁盘添加项目，选择本仓库根目录（包含 Assets、Packages 和 ProjectSettings 的这一层）。
4. 打开 `Assets/Scenes/CampusLiving.unity`，点击播放按钮。

默认场景是非像素版 `CampusLiving`。原始迁移场景 `Campus.unity` 和取消采用的像素样板 `CampusPixel.unity` 仅保留作历史备份，不在当前构建场景列表中。保留它们的脚本与资源是为了保持场景依赖完整，不代表继续采用像素风。

仓库没有复制 Library 缓存，首次打开时 Unity 会重新导入资源，需要耐心等待。该仓库根目录本身就是 Unity 工程，不需要再进入 UnityCampus 子目录。

## 内容与操作

包含校园建筑、ABC 教学楼与喷泉广场、生活区轮廓、植被与道路、湖泊、校园角色及骨骼动画。生活区依据参考图相对比例重建，不是米制测绘。西侧长条楼名称待确认，旧场景内部的第三公寓名称仅为历史暂记。

WASD 移动，鼠标转向，Shift 无限奔跑，空格跳跃，E 交互，M 地图，R 返回南门，L 前往生活区，T 前往教学广场，ESC 暂停并释放鼠标。

## 目录

- `Assets/Scripts`：角色移动、相机、任务、界面与碰撞代码。
- `Assets/Editor`：场景生成和 Windows 构建工具。
- `Assets/Scenes`：校园场景，默认使用 CampusLiving。
- `Assets/LivingRevision`：当前校园模型、材质、纹理和地图数据。
- `Assets/Characters`：校园人物与动画。
- `Assets/Shaders`：材质渲染代码。
- `Packages`、`ProjectSettings`：项目依赖和设置。

已生成资源可直接编辑和构建，不需要浏览器版源代码。`Campus/Build Windows Demo` 会构建当前配置的 CampusLiving 场景到 `Builds/Windows`。旧场景重新生成命令依赖原仓库的 `tools/unity` 和 `TempMigration` 导出数据，本独立仓库未包含这些中间数据，日常开发不需要运行重新生成命令。

## GitHub Desktop 协作

仓库已配置 Unity 忽略规则和 Git LFS。提交 Assets 时必须一起提交对应 `.meta` 文件。代码、场景、材质和设置都应进入版本控制；Library、Temp、Logs、Builds 等缓存和产物不上传。

项目包含较大的生成网格，首次上传、克隆需要传输较多资源。Git LFS 管理模型、纹理资源以及配置中列出的较大网格。若模型资源显示为短文本指针，应先完成 Git LFS 下载，再打开 Unity。

多人协作建议每项功能使用单独分支，尽量分开修改场景或预制体，完成后合并。新仓库与旧网页版的 main 完全独立，但新仓库内部多人修改同一文件时仍可能产生冲突。
