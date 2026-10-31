# 第一章占位音频

- CozyLoop_Placeholder.wav：原创程序合成占位音乐，42.67 秒，90 BPM，C 大调 / A 小调色彩，钟琴、暖和弦与低音，16 小节。立体声 44.1 kHz / 16-bit PCM；尾音循环回混至开头。
- Interact_Placeholder.wav：0.36 秒双音交互提示，立体声 44.1 kHz / 16-bit PCM。
- 素材位于 Assets/ChapterOne/Resources/Audio。生成源码位于 Tools/Audio/PlaceholderSynth.cs，无外部采样。

ChapterOneScene.Awake 调用 ChapterOneAudio.Ensure。独立的持久音频对象播放两个 2D AudioSource：音乐循环、默认音量 0.25；提示音默认音量 0.5。学校与甜品店使用同一首音乐，场景切换保持对象和播放位置；切出窗口 / 应用暂停时暂停音源，返回时继续。

SchoolInteractionController.TryInteract 在距离、碰撞、对话和冷却检查通过且交互返回成功后播放提示音。键盘 E 与鼠标使用同一路径；有效对话选项也播放提示音。无目标、距离不足、对话中重复触发及无效选项不播放。音源另有 65 ms 防重叠冷却。

后续替换：用正式 WAV 覆盖同名文件并保留 .meta，触发逻辑无需改变；更改音量可调整 ChapterOneAudio 中的默认值。音频不包含人声，也未加入脚步或特定物品专属音效。

Unity 原生 audio 检查通过：音频加载、自动播放、末尾循环、跨场景不重启且无重复管理器、有效交互与选项播放、无效操作静音、失焦暂停及恢复。报告与修改前备份位于 Recovery/ChapterOne/Audio。测试使用独立存档。
