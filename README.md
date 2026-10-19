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

建筑占地仍约 36 × 20 米，中央屋顶高约 18.8 米，属于照片与游戏地图的美术适配，并非实测复原。按 F 可到食堂正门，靠近后按 E 进入。

**室内已建成三层**（36 × 32 米，层高 4.3 米，总高 12.9 米，`Campus/Interiors/Build Cafeteria Three Floors` 重建）。一层为大众快餐大厅、选餐台、餐盘回收与感应洁手台；二层为特色风味餐厅与休闲水吧岛台；三层为鲜果自选超市（多层水果货架与电子秤）、特色直排档口与教工餐厅立面。双跑西南楼梯与直跑东南楼梯贯通三层，全馆配有 16 处 `[E]` 交互点（`CafeteriaIndoorInteractions.cs`）。独立更新包位于 `Updates/Cafeteria_Interior_Package`。

`Campus/Apply Reference Cafeteria` 可重建食堂；若场景已有图书馆，会按顺序重新应用图书馆外观，保留其前后门并避免旧网格重新出现。食堂资源在 `Assets/CafeteriaRevision`，图书馆存在时整合运行地图仍在 `Assets/LibraryRevision/CampusMap.json`。`Preview-Cafeteria.cmd` 可启动食堂预览。文字使用本机中文字体，Windows 推荐 Microsoft YaHei。

### 生活费与档口点餐系统

**生活费**是全局唯一的余额：右上角常驻显示“生活费：¥xxx.xx”，校园、图书馆、食堂、任意楼层与所有面板打开时都可见，全屏只有这一处余额显示。余额以整数“分”结算，只在该存档从未记录过余额时初始化一次，进入建筑、切换楼层、开关菜单、重启游戏都不会重置。食堂点餐、水吧、水果摊与图书馆两台售卖机全部从这份余额扣费，不足时拒绝交易并提示差额，不会扣成负数。代码位于 `Assets/Scripts/CampusWallet.cs` 与 `Assets/Scripts/CampusHud.cs`。

**13 个档口各自独立**（`Assets/Scripts/CafeteriaShops.cs`，稳定 ID 写入存档）：

| 楼层 | 档口 | 类型 |
| --- | --- | --- |
| 1F | 1号·精美冷盘 / 2号·经典小炒 / 3号·大工现煮面 / 4号·蒸点主食 | 点餐 |
| 1F | 水吧 | 即时购买·饮品 |
| 2F | 1号·川湘热炒 / 2号·瓦罐砂锅 / 3号·铁板炙烤 / 4号·风味面点 | 点餐 |
| 3F | 1号·砂锅原汤与蒸点 / 2号·精品热炒与主食 / 3号·广式烧味与铁板 | 点餐 |
| 3F | 水果摊 | 即时购买·按份水果 |

走到哪个柜台，按 E 就只打开那个柜台的菜单，标题显示楼层与档口名；只识别同层、最近且在交互距离内的柜台，不会隔墙或隔层串到别的窗口。水吧只卖饮品，水果摊按“份”售卖水果，二者确认后立即把商品放进随身物品并可直接享用（菜单内“立即享用”），不进入备餐与还餐盘流程。

**订单可同时存在多笔**（`CafeteriaOrderingSystem.cs` / `CafeteriaOrder.cs`）：每笔订单保存自己的编号、商家、楼层、商品、金额、取餐点与状态。在 A 档口下单后仍可去 B 档口下单或买饮品，互不覆盖；备餐倒计时结束后自动转为“可取餐”，只有该档口自己的取餐点能领取，且每个柜台按 E 时“取餐”优先于“打开菜单”。手里已有餐品时再次领取会被拒绝并提示先处理手中餐盘，未领取的订单继续保留。落座用餐、进食进度条与餐盘回收沿用原有流程。

**菜单按游戏日期生成**（`Assets/Scripts/CafeteriaMenu.cs`）：同一游戏日反复开关菜单、进出食堂或重启游戏，菜品与价格完全一致；不同商家使用各自的菜品池。价格规则：菜品 >¥0 且 ≤¥10，饮品 >¥0 且 ≤¥20，水果每份 >¥0 且 ≤¥10，均可在该文件顶部的常量中调整。已下单的订单保存下单时的商品与价格快照，换日刷新不会改变已有订单金额。

### 图书馆参考图改造

图书馆采用三层对称石材侧翼与中央通高玻璃大厅，补充木色竖向格栅、宽屋檐、金色馆名、入口雨棚、台阶、侧坡道和绿化铺装。窗后书架、阅览桌、灯带及弧形栏杆仅用于外观展示。前后门均支持 E 交互，后门配有玻璃门、雨棚、台阶和两侧连接步道。两扇门共用 library.unity，退出时回到本次进入的门外。

**室内已建成四层**（40 × 32 米，层高 4.3 米，总高 17.2 米，`Campus/Interiors/Build Library Three Floors` 重建，命令名暂沿用）。一层为门厅：南门两侧自动售卖机、正中央校园规划沙盘（玻璃罩、灯光与说明牌）、东北卫生间、南墙东南角楼梯与电梯相邻（双跑折返楼梯，向内凹陷）、正北第二扇门、西墙预留通往信息楼的连廊洞口（暂未接通）、东侧校史展览馆（仅展墙贴图与 `[E]` 交互，**不含实体展陈模型**）。二、三、四层布局相同：北墙卫生间、东墙 6 米大门通往圆形自习厅，厅内由外向内依次为落地玻璃幕墙、自习桌环、书架与过道环、9 米中央镂空（带栏杆），三层圆厅中庭上下贯通。楼梯贯通四层，电梯为 `[E]` 呼出楼层选择面板（1–4 层，数字键或点击）。

全馆现有 15 处 `[E]` 交互点（`LibraryIndoorInteractions.cs`）。

外观约 48 × 30 米，中央高约 22.4 米，是参考照片与校园位置的设计适配，并非测绘复原。`Campus/Apply Reference Library` 可重建外观；若重建其他整片校园资源，按食堂、图书馆、体育馆顺序应用改造。最新整合运行地图在 `Assets/TeachingRevision/CampusMap.json`。`Preview-Library.cmd` 可打开图书馆广场预览。

### 体育馆参考外观

体育馆采用两端上扬的连续曲面屋顶、木色檐底、三层玻璃大厅与浅色石材侧翼，并加入运动剪影、木格栅、玻璃入口、宽台阶和门前花池、自行车、座椅、旗帜路灯。占地约 56 × 33 米，檐口约 22.3–24.5 米；尺寸适配原地图，保留相邻网球场。透过玻璃可见灯光、楼层栏杆和健身器械等外观陈设，独立室内 `gymnasium.unity` 仍为空白占位场景。

双击 `Preview-Gymnasium.cmd` 可到体育馆门前预览，走到中央台阶前按 E 进入。`Campus/Apply Reference Gymnasium` 可重新生成。体育馆最后裁剪合并外围场景；食堂、图书馆的单独重建命令会先恢复体育馆裁剪源，再依次重建，避免旧模型重新出现。整体重建后按食堂 → 图书馆 → 体育馆顺序应用。`-gymQA` 输出五个实际渲染视角至 `Builds/Windows/QA/Gymnasium`。

### ABC 教学楼参考外观与间距

ABC 保留凹字形关系、原编号、中央喷泉和广场。A 在后侧、B 在西侧、C 在东侧；A/C 的现有位置不变。B 改为直线楼体，地图范围 x=24.5–39.5、z=-105.5–-42.5 米，两端收在 A/C 的范围内，楼角保留步行间隔。入口、室外碰撞、相机碰撞与路线随 B 楼轮廓同步更新；原弧形楼留下的低矮地基底板已清理，露出原有草地。

三栋楼统一为五层浅色石材外墙、深色窗框、透明玻璃和木色竖向格栅；各自配有玻璃入口大厅、檐顶、雨棚、台阶与实体楼号字牌。窗后课桌、学生和灯光仅作为外观景深；入口交互点和碰撞跟随楼体移动。ABC 三栋楼均已制作可进入的五层室内，分别为阶梯教学、综合教学与自习、计算机与多媒体教学。

双击 `Preview-Teaching.cmd` 可打开教学区预览，游戏中也可按 T 到教学广场。`Campus/Apply Reference Teaching Buildings` 根据保存的 `Assets/TeachingRevision/OriginalFootprints.json` 重建，重复执行不会继续平移楼体。完整改造顺序为食堂 → 图书馆 → 体育馆 → 教学区；前三项单独重建命令会保留并重建现有教学区改造。`-teachingQA` 输出教学区整体、三楼正面、俯视及完整正交轮廓图至 `Builds/Windows/QA/Teaching`。

### 建筑入口与室内场景接口

图书馆、体育馆、食堂、ABC 教学楼和六栋宿舍共 13 个入口（图书馆含前后门）、12 个室内场景已接入 E 键交互。靠近正门显示“进入建筑”，进入后靠近出口按 E 返回原建筑门外。第六食堂（三层）与图书馆（四层）已有完整的实际室内建筑、装潢陈设与交互系统（详见 `Updates/` 下的独立更新包）；ABC 楼均为可探索的五层教学空间，其余场景仍为空白测试房间；角色移动、跳跃、相机、暂停、碰撞和进出流程可以直接测试。

各楼的独立场景在 `Assets/Interiors/Scenes`，对应配置在 `Assets/Interiors/Entrances`。同伴只需编辑各自场景并保留 `InteriorSceneRoot` 的出生点和出口引用，无需修改主校园场景。具体配置、生命周期事件、场景构建和协作说明见 [INTERIOR_HANDOFF.txt](INTERIOR_HANDOFF.txt)。双击 `Preview-Interiors.cmd` 可到食堂门外测试。

### C 楼可进入室内

`Assets/Interiors/Scenes/teaching-c.unity` 按参考图的浅灰石材、木饰面、玻璃栏杆和明亮教室设计为五层，层高 4.2 米。每层有两间多媒体教室、两间机房和男女卫生间，共 10 间教室、10 间机房；每间有 15 个课桌/计算机座位。中庭通高，楼层和房号标识为墙面实体文字。不设置自习室或休息区。空间尺寸是游戏设计，并非实测室内图。

后侧双跑楼梯每半层 12 级，单级高 0.175 米、进深 0.42 米，四组楼梯共 96 个带独立碰撞的台阶，连接 1F–5F。角色沿台阶实际上下楼，不使用传送、隐形坡道或切换楼层按钮。中庭与楼梯设置护栏碰撞，房间通过敞开的门洞进入。电脑、投影、卫生间设施目前为场景陈设。

室内墙体、门框、吊顶和地面按统一边界拼接；卫生间使用贴墙洗手柜、凹面洗手盆、完整隔间收边。桌椅、柜体、陶瓷设施增加细小圆角，扶手和桌腿采用圆管。C 楼使用独立室内渲染器和 Volume，提供抗锯齿、接触阴影及柔和色调；退出后恢复校园相机和阴影设置。渲染器与配置保存在 `Assets/TeachingCInterior`，通过 `TeachingCInteriorPresentation` 接入场景生命周期。

双击 `Preview-Teaching-C-Interior.cmd` 可直接进入 C 楼预览，点击继续探索后使用 WASD 行走；走廊尽头上楼，回到一楼正门附近按 E 返回校园。普通校园入口也已使用同一场景。

`Campus/Interiors/Generate Teaching C` 从 `Assets/Editor/TeachingCInteriorBuilder.cs` 重新生成 C 楼，资源保存于 `Assets/TeachingCInterior`。该命令会重建 C 楼室内，因此手工编辑场景后不要直接重跑生成器；它不改其他建筑室内或户外布局。`-teachingCQA` 自动通过 E 进出，使用与键盘相同的 `CampusGame.MoveIndoor` 行走逻辑完成一楼到五楼及返回，验证房门通路和中庭护栏，检查十处卫生间的墙体、门楣、地面和吊顶闭合，并输出十张渲染图及结果到 `Builds/Windows/QA/TeachingCInterior`。

### A、B 楼室内

A 楼每层两间阶梯教室，共 10 间。每间 5 排、30 座，逐排升高 0.18 米，配深蓝座椅、木质吸音墙、讲台和投影；座位两侧为连续可行走的台阶通道。教学空间全部为阶梯教室，不设机房或自习室。

B 楼每层各一间普通教室、多媒体教室、语音教室和研讨教室，共 20 间。语音桌配屏幕、隔板、耳机及麦克风；研讨室采用分组桌。每层中部设 8 个自习座位、书架和台灯，留出中央贯通通道。采用浅木色与灰绿色，中央楼板连续，与 C 楼通高中庭不同。

两栋楼均为五层、层高 4.2 米，每层有男女卫生间；各有 96 级带碰撞的实体楼梯。室内为功能设计，非学校实测图。电脑、耳机、投影等为陈设。进入与返回仍使用建筑门口的 E 键。

`Preview-Teaching-A-Interior.cmd`、`Preview-Teaching-B-Interior.cmd` 可分别预览。菜单 `Campus/Interiors/Generate Teaching A and B` 重建两个独立场景，资源在 `Assets/TeachingAInterior` 和 `Assets/TeachingBInterior`。共享结构与细节生成器在 `TeachingCInteriorBuilder.cs`，差异化布局在 `TeachingABInteriorBuilder.cs`；重建会覆盖对应场景的手工修改，C 场景不随 A/B 重建。三楼共用 C 的室内渲染器，色调配置各自独立。

运行 `CampusEcho.exe -teachingABQA -force-d3d11` 检查两栋楼全部楼层、阶梯座位通道、房门、自习区和进出流程，结果与实际渲染图保存到 `Builds/Windows/QA/TeachingABInterior`。

## 目录

二楼新增 A↔B、A↔C 玻璃连廊。走到连廊尽头按 E，在目标楼二楼进入，再通过实体楼梯前往其他楼层。双击 `Preview-Skybridges.cmd` 可从 A 楼二楼预览；场景连接、出生点和回调接口详见 [SKYBRIDGE_HANDOFF.md](SKYBRIDGE_HANDOFF.md)。

- `Assets/Scripts`：角色移动、相机、任务、界面与碰撞代码。
- `Assets/Editor`：场景生成和 Windows 构建工具。
- `Assets/Scenes`：校园场景，默认使用 CampusLiving。
- `Assets/LivingRevision`：当前校园模型、材质、纹理和地图数据。
- `Assets/DormitoryRevision`、`Assets/ResidenceCourtyard`：新版宿舍、庭院道路、材质及运行地图。
- `Assets/CafeteriaRevision`：食堂及前场环境。
- `Assets/LibraryRevision`：图书馆、前场环境和材质。
- `Assets/GymnasiumRevision`：体育馆、前场环境和材质。
- `Assets/TeachingRevision`：ABC 教学楼、原始轮廓快照、材质及当前整合运行地图。
- `Assets/TeachingCInterior`：C 楼五层室内材质与网格；室内场景仍在 `Assets/Interiors/Scenes`。
- `Assets/Characters`：校园人物与动画。
- `Assets/Shaders`：材质渲染代码。
- `Packages`、`ProjectSettings`：项目依赖和设置。

已生成资源可直接编辑和构建，不需要浏览器版源代码。`Campus/Build Windows Demo` 会构建当前配置的 CampusLiving 场景到 `Builds/Windows`。旧场景重新生成命令依赖原仓库的 `tools/unity` 和 `TempMigration` 导出数据，本独立仓库未包含这些中间数据，日常开发不需要运行重新生成命令。

## 验证与自测

生活费、档口与订单逻辑有两级验证，按改动类型选用，避免每次都重新打包：

1. **逻辑快测（约 10 秒）** — 不需要图形、不需要构建玩家包，改规则后先跑它：

       /Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity \
         -batchmode -quit -projectPath "$(pwd)" \
         -executeMethod AllowanceLogicTest.Run -logFile /tmp/logic.log

   覆盖：余额初始化/扣费/差额/持久化、13 个档口各自的菜单池与价格上限、
   同日菜单稳定性、购物车与连点只扣一次、多订单独立倒计时、即时购买入物品、
   按取餐点领取、满手拒绝、还餐盘后重新可领、模拟重启恢复。
   结果写入 `QA/AllowanceLogic/results.txt`。

2. **图形回归（约 10 分钟）** — 仅当界面绘制、点击路径或场景绑定改动时运行：

       Unity -batchmode -quit -projectPath "$(pwd)" -executeMethod AllowanceQABuild.Build
       <构建产物>/AllowanceQA.app/Contents/MacOS/AllowanceQA -allowanceQA \
         -qaOutput "$(pwd)/output/生活费与档口验证" \
         -screen-width 1280 -screen-height 800 -screen-fullscreen 0

   它以真实 IMGUI 鼠标事件点击界面按钮，并截图记录校园、图书馆、食堂各档口、
   多订单、取餐、进食与余额不足等状态。

> 注意：Unity 每次批处理启动约 40–60 秒，打包约 3–5 分钟。改逻辑时用第 1 种即可，
> 不必反复打包。

## GitHub Desktop 协作

仓库已配置 Unity 忽略规则和 Git LFS。提交 Assets 时必须一起提交对应 `.meta` 文件。代码、场景、材质和设置都应进入版本控制；Library、Temp、Logs、Builds 等缓存和产物不上传。

项目包含较大的生成网格，首次上传、克隆需要传输较多资源。Git LFS 管理模型、纹理资源以及配置中列出的较大网格。若模型资源显示为短文本指针，应先完成 Git LFS 下载，再打开 Unity。

多人协作建议每项功能使用单独分支，尽量分开修改场景或预制体，完成后合并。新仓库与旧网页版的 main 完全独立，但新仓库内部多人修改同一文件时仍可能产生冲突。
