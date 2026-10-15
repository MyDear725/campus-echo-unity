# 教学区二楼连廊

A 楼二楼后侧横走廊左端通 B，右端通 C；B、C 的对应入口可返回 A。连廊为连续地面与玻璃护栏，尽头玻璃门按 E 切换场景。到达位置在目标楼二楼，继续通过原有实体楼梯前往其他楼层。

## 场景接口

`InteriorSceneRoot.connections` 中每项为 `InteriorConnection`：

- `id`：当前场景内的连接标识。
- `anchor`、`radius`：E 交互中心与水平半径；高度差必须小于 1 米，避免上下楼层误触。
- `destination`：已有的 `BuildingEntranceDefinition`，用于目标场景、建筑 ID 以及日后返回目标楼正门。
- `destinationSpawnId`：目标 `InteriorSceneRoot.spawnPoints` 中的出生点 ID。
- `displayName`：交互提示与门口标识。

| 源楼连接 | 目标楼 | 目标出生点 |
| --- | --- | --- |
| B / b-to-a | A | bridge-from-b |
| A / a-to-b | B | bridge-from-a |
| A / a-to-c | C | bridge-from-a |
| C / c-to-a | A | bridge-from-c |

出生点地面为 y=4.2m，锚点 y=4.26m，面朝室内，离返回交互范围至少 0.9m。原 `main` 出生点与正门出口保持独立。换楼不改变室外校园的隐藏状态；目标场景及出生点验证通过才卸载旧室内。失败会保留当前位置，可再次尝试。掉出场景时回到最近使用的出生点。

生命周期：换楼先调用旧根节点 `onPlayerLeaving`，再调用新根节点 `onPlayerEntered`；成功后发出 `BuildingInteriorTravel.Transferred(from,to)`。原 `Entered`、`Exited` 事件只用于校园与室内之间的进出。接入自定义效果请使用根节点回调，避免重复添加相机、玩家和 AudioListener。

## 编辑与验证

`TeachingSkybridgeBuilder.Generate` 重建室外连廊和 A/B/C 三个室内场景，**会覆盖三个场景的手工编辑**；团队开始手工制作后应直接编辑场景或先合并生成器改动。室外连廊在 `TeachingRevision/Skybridges`；室内网格仍分别保存到各楼的资源目录。

`Preview-Skybridges.cmd` 在 A 楼二楼开始预览。常规游玩从建筑正门进入，走后侧实体楼梯上二楼。

`CampusEcho.exe -skybridgeQA -force-d3d11`：使用真实控制器检查 A→B→A→C→A→C、上下实体楼梯、到达朝向、错误目标回滚、掉落恢复以及目标楼正门返回；结果及六张渲染图在 `Builds/Windows/QA/Skybridges`。原 `-entranceQA`、`-teachingABQA`、`-teachingCQA` 继续可用。

## 连廊细节与窗外景观

两条连廊均在教学区后角连接 A 楼，庭院中间不再有 B—C 横跨连廊。室内采用独立地砖、窄缝、带倒角的窗框与门框、细圆扶手、木格栅顶棚和双侧嵌入灯带。门口加入闭门器、横向推杆、金属踢板及伸缩缝。

`SkybridgeCampusView` 在走近连廊时复用校园中已有可见网格和材质，仅创建渲染节点，不带碰撞、相机、灯光或行为脚本。每条连廊按地图方向转换到室内坐标；离开连廊时停用，退出场景时销毁。外立面材质通过节点独立的 MaterialPropertyBlock 裁掉连廊门洞范围，校园原材质默认不裁剪。
