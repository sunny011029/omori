# School 场景

打开 `Assets/Scenes/school.unity`。布局参照 `Assets/Reference.png`：左侧大教室、右侧小教室、下方连通走廊、两扇教室门和中央入口。

`School_ReferenceLayout` 下的对象可以单独编辑：

- `01_Architecture`：四组地面 Tilemap、墙体、入口地垫。
- `02_LeftClassroom`：讲台、课桌椅、黑板、窗帘与书柜。
- `03_RightClassroom`：课桌椅、教学桌、地球仪、窗帘与展示板。
- `04_CorridorFurniture`：教室门、储物柜、书柜和装饰。
- `05_GameplayGeometry`：走廊隔墙、门口通道与房间边界碰撞。
- `06_Player/BlondWhite`：从 MochaStore 复制的金发白衣角色。
- `Main Camera`：平滑跟随角色，并根据教室、走廊、入口的实际范围限制可见区域。

地面直接引用 `ground_School_Green` 调色板的 Tile。家具使用 `school-purple` 与 `school-柜子` 的同源改色图集；新增的 `School_` 整体 Sprite 切片可在 Sprite Editor 中查看，原有切片和图像像素保留。门、窗帘和墙面线条为独立的 Sprite 对象，颜色可在 Inspector 中调整。

原场景对象保留在停用的 `PreviousScene_Disabled` 下；搭建前的场景备份与全景预览位于项目根目录的 `Recovery/School`。

`Tools > School > Build Reference Layout` 会重新生成 `School_ReferenceLayout` 并保存场景，**覆盖该分组内的手动摆放修改**；每次生成前都会保存场景备份。正常编辑无需再次运行此工具。

进入 Play 模式并点击 Game 窗口后，使用 WASD 或方向键移动。角色沿用 MochaStore 的控制器、Animator、Walk/Idle 动画、3.5 移动速度和脚部碰撞设置；父节点缩放为 0.5，以适配学校桌椅尺寸。斜向移动不会加速，上下移动保留上次左右朝向。

家具与角色统一使用 `TopDownYSort` 按脚部位置排序。墙体、课桌、柜子和入口底边阻挡角色；两扇教室门在靠近时自动打开，远离后关闭。右教室门口的桌椅向左移动 0.4 格以留出通路。

摄像机沿用 `TopDownCameraFollow` 的跟随逻辑，通过学校专用的 `SchoolCameraFootprint` 限制整个画面，避免显示两个教室之间的外部区域。走廊和入口会自动拉近，进入教室后恢复较宽视野；超宽窗口使用侧边留空，确保狭窄入口处角色仍能完整显示。MochaStore 继续使用原有的矩形房间边界。

`Tools > School > Install Character and Camera` 安装或重新配置角色与摄像机，先保存场景备份；首次安装会复制角色，重复运行不会再复制角色。重新生成场景布局后，需要重新运行安装。`Tools > School > Verify Character and Camera` 会进入 Play 模式检查移动、动画、碰撞、遮挡及多种画面比例下的边界，结束后退出 Play；报告位于 `Library/SchoolGameplayChecks.report.txt`。
