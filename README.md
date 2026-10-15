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

WASD 移动，鼠标转向，Shift 无限奔跑，空格跳跃，E 交互，M 地图，R 返回南门，L 前往生活区，B 前往宿舍花园南入口，F 前往食堂前广场，T 前往教学广场，ESC 暂停并释放鼠标。

### 宿舍参考图改造

宿舍外立面采用参考照片中的浅灰小方砖、八层体量、内凹阳台、金属栏杆、晾衣、空调外机、中央窗带和玻璃门雨棚。北部五栋公寓现调整为北侧两栋、两翼围合、南侧开放的凹形庭院，主要楼间通道约 10～15 米；南部第六宿舍保留原位。这是为改善游戏空间而调整的设计，不是校区实测布局。楼高及未展示的背立面为美术推定；西侧楼仍未确认楼名。外观改造已完成；入口现已接通独立的空白室内占位场景，供后续制作内容。

庭院包含花坛、树木、座椅和路灯，外围采用沥青道路，步行环路及入口采用错缝灰色铺砖，附带分段路缘石与排水格栅。小地图、行走碰撞和相机墙体与新布局同步。`ResidenceCourtyardBuilder.cs` 负责布局、旧环境的局部裁切及庭院建模；其资源和运行地图位于 `Assets/ResidenceCourtyard`。每次重建都会从原始 `Assets/LivingRevision/CampusMap.json` 开始，避免累计位移。生成时自动验证校园任务路线、庭院环路、入口通路与出生点。

`Assets/Editor/DormitoryBuilder.cs` 是可重复执行的原生 Unity 建模工具。菜单 `Campus/Apply Reference Dormitories` 更新当前默认场景；替换后的网格与材质保存在 `Assets/DormitoryRevision`，原导入模型保留。重建不依赖旧网页版或外部建模软件。修改场景前请先保存自己的编辑。

### 食堂参考图改造

第六食堂按最新参考图改为三层石材侧翼、中央通高玻璃大厅和木色大屋檐。入口两侧使用竖向木色格栅，玻璃前设置金色中英文招牌，厅内可从外面看到两组环形吊灯、餐桌、座椅、就餐人物与绿植。门前保留雨棚、台阶、侧坡道，增加花草、行道树、长椅、自行车和题字景石；铺装控制在原前场范围内，避免占用西侧主路。

建筑占地仍约 36 × 20 米，中央屋顶高约 18.8 米，属于照片与游戏地图的美术适配，并非实测复原。按 F 可到食堂正门，靠近后按 E 进入已有空白室内场景。窗后陈设仅用于外部观察，独立室内场景留给同伴制作。

`Campus/Apply Reference Cafeteria` 可重建食堂；若场景已有图书馆，会按顺序重新应用图书馆外观，保留其前后门并避免旧网格重新出现。食堂资源在 `Assets/CafeteriaRevision`，图书馆存在时整合运行地图仍在 `Assets/LibraryRevision/CampusMap.json`。`Preview-Cafeteria.cmd` 可启动食堂预览。文字使用本机中文字体，Windows 推荐 Microsoft YaHei。

### 图书馆参考图改造

图书馆采用三层对称石材侧翼与中央通高玻璃大厅，补充木色竖向格栅、宽屋檐、金色馆名、入口雨棚、台阶、侧坡道和绿化铺装。窗后书架、阅览桌、灯带及弧形栏杆仅用于外观展示，`Assets/Interiors/Scenes/library.unity` 仍保持原来的空白占位场景。前后门均支持 E 交互，后门配有玻璃门、雨棚、台阶和两侧连接步道。两扇门共用 library.unity，退出时回到本次进入的门外。

外观约 48 × 30 米，中央高约 22.4 米，是参考照片与校园位置的设计适配，并非测绘复原。`Campus/Apply Reference Library` 可重建；若重建其他整片校园资源，按食堂、图书馆、体育馆顺序应用改造。最新整合运行地图在 `Assets/TeachingRevision/CampusMap.json`。`Preview-Library.cmd` 可打开图书馆广场预览。

### 体育馆参考外观

体育馆采用两端上扬的连续曲面屋顶、木色檐底、三层玻璃大厅与浅色石材侧翼，并加入运动剪影、木格栅、玻璃入口、宽台阶和门前花池、自行车、座椅、旗帜路灯。占地约 56 × 33 米，檐口约 22.3–24.5 米；尺寸适配原地图，保留相邻网球场。透过玻璃可见灯光、楼层栏杆和健身器械等外观陈设，独立室内 `gymnasium.unity` 仍为空白占位场景。

双击 `Preview-Gymnasium.cmd` 可到体育馆门前预览，走到中央台阶前按 E 进入。`Campus/Apply Reference Gymnasium` 可重新生成。体育馆最后裁剪合并外围场景；食堂、图书馆的单独重建命令会先恢复体育馆裁剪源，再依次重建，避免旧模型重新出现。整体重建后按食堂 → 图书馆 → 体育馆顺序应用。`-gymQA` 输出五个实际渲染视角至 `Builds/Windows/QA/Gymnasium`。

### ABC 教学楼参考外观与间距

ABC 保留原教学区的凹字形关系、各楼轮廓和编号，中央喷泉与原广场保留。基于原始地图，后侧 A 楼向北退 4 米；西侧 B 楼向西 5 米、向南 3 米；东侧 C 楼向东 5 米、向南 5 米。两处原本紧贴的楼角最小墙体间距约 3.8 米和 4.0 米，避免移动北侧楼侵占校外道路。

三栋楼统一为五层浅色石材外墙、深色窗框、透明玻璃和木色竖向格栅；各自配有玻璃入口大厅、檐顶、雨棚、台阶与实体楼号字牌。窗后课桌、学生和灯光仅作为外观景深；三份独立室内场景保持不变，入口交互点和碰撞跟随楼体移动。

双击 `Preview-Teaching.cmd` 可打开教学区预览，游戏中也可按 T 到教学广场。`Campus/Apply Reference Teaching Buildings` 根据保存的 `Assets/TeachingRevision/OriginalFootprints.json` 重建，重复执行不会继续平移楼体。完整改造顺序为食堂 → 图书馆 → 体育馆 → 教学区；前三项单独重建命令会保留并重建现有教学区改造。`-teachingQA` 输出教学区整体、三楼正面与俯视图至 `Builds/Windows/QA/Teaching`。

### 建筑入口与室内场景接口

图书馆、体育馆、食堂、ABC 教学楼和六栋宿舍共 13 个入口（图书馆含前后门）、12 个室内场景已接入 E 键交互。靠近正门显示“进入建筑”，进入后靠近出口按 E 返回原建筑门外。室内目前仅有空白测试房间，不包含正式陈设；角色移动、跳跃、相机、暂停、碰撞和进出流程可以直接测试。

各楼的独立场景在 `Assets/Interiors/Scenes`，对应配置在 `Assets/Interiors/Entrances`。同伴只需编辑各自场景并保留 `InteriorSceneRoot` 的出生点和出口引用，无需修改主校园场景。具体配置、生命周期事件、场景构建和协作说明见 [INTERIOR_HANDOFF.txt](INTERIOR_HANDOFF.txt)。双击 `Preview-Interiors.cmd` 可到食堂门外测试。

## 目录

- `Assets/Scripts`：角色移动、相机、任务、界面与碰撞代码。
- `Assets/Editor`：场景生成和 Windows 构建工具。
- `Assets/Scenes`：校园场景，默认使用 CampusLiving。
- `Assets/LivingRevision`：当前校园模型、材质、纹理和地图数据。
- `Assets/DormitoryRevision`、`Assets/ResidenceCourtyard`：新版宿舍、庭院道路、材质及运行地图。
- `Assets/CafeteriaRevision`：食堂及前场环境。
- `Assets/LibraryRevision`：图书馆、前场环境和材质。
- `Assets/GymnasiumRevision`：体育馆、前场环境和材质。
- `Assets/TeachingRevision`：ABC 教学楼、原始轮廓快照、材质及当前整合运行地图。
- `Assets/Characters`：校园人物与动画。
- `Assets/Shaders`：材质渲染代码。
- `Packages`、`ProjectSettings`：项目依赖和设置。

已生成资源可直接编辑和构建，不需要浏览器版源代码。`Campus/Build Windows Demo` 会构建当前配置的 CampusLiving 场景到 `Builds/Windows`。旧场景重新生成命令依赖原仓库的 `tools/unity` 和 `TempMigration` 导出数据，本独立仓库未包含这些中间数据，日常开发不需要运行重新生成命令。

## GitHub Desktop 协作

仓库已配置 Unity 忽略规则和 Git LFS。提交 Assets 时必须一起提交对应 `.meta` 文件。代码、场景、材质和设置都应进入版本控制；Library、Temp、Logs、Builds 等缓存和产物不上传。

项目包含较大的生成网格，首次上传、克隆需要传输较多资源。Git LFS 管理模型、纹理资源以及配置中列出的较大网格。若模型资源显示为短文本指针，应先完成 Git LFS 下载，再打开 Unity。

多人协作建议每项功能使用单独分支，尽量分开修改场景或预制体，完成后合并。新仓库与旧网页版的 main 完全独立，但新仓库内部多人修改同一文件时仍可能产生冲突。
