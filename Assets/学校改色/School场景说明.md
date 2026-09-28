# School 场景

打开 `Assets/Scenes/school.unity`。布局参照 `Assets/Reference.png`：左侧大教室、右侧小教室、下方连通走廊、两扇教室门和中央入口。

`School_ReferenceLayout` 下的对象可以单独编辑：

- `01_Architecture`：四组地面 Tilemap、墙体、入口地垫。
- `02_LeftClassroom`：讲台、课桌椅、黑板、窗帘与书柜。
- `03_RightClassroom`：课桌椅、教学桌、地球仪、窗帘与展示板。
- `04_CorridorFurniture`：教室门、储物柜、书柜和装饰。
- `Main Camera`：正交全景摄像机，自动适配 Game 窗口比例。

地面直接引用 `ground_School_Green` 调色板的 Tile。家具使用 `school-purple` 与 `school-柜子` 的同源改色图集；新增的 `School_` 整体 Sprite 切片可在 Sprite Editor 中查看，原有切片和图像像素保留。门、窗帘和墙面线条为独立的 Sprite 对象，颜色可在 Inspector 中调整。

原场景对象保留在停用的 `PreviousScene_Disabled` 下；搭建前的场景备份与全景预览位于项目根目录的 `Recovery/School`。

`Tools > School > Build Reference Layout` 会重新生成 `School_ReferenceLayout` 并保存场景，**覆盖该分组内的手动摆放修改**；每次生成前都会保存场景备份。正常编辑无需再次运行此工具。

当前为静态场景搭建。课桌和墙体已有基础碰撞，门为场景装饰；NPC、对话与开门交互可在后续添加。
