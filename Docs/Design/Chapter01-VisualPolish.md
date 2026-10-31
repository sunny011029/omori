# 入口动画与 sheila 白云反馈（2026-10-31）

按新的表现要求，sheila 分享甜品并完成收好草稿的对话后（DraftKept），灰云改成白云，雨滴和再生联系停止。该表现由已有存档节点恢复；Elaine 和 nate 保留原来的天气。云仍使用 Q 云视显示。

更新章末对话、人物记录及结算中的描述，避免继续显示“灰云仍下雨”。该调整取代旧版保留灰云的设计。

ChapterOnePortalEffect 在两个已有入口的视觉节点下生成循环呼吸光晕、向上浮动的绿色像素光点。甜品店地面入口还增加脉动点状光环。复用现有像素块与材质，只创建运行时视觉子物体。

Unity 原生完整剧情检查通过；专项画面检查验证 sheila 白云、其他 NPC 灰云及入口动画。前后截图、测试报告与文件校验见 Recovery/ChapterOne/VisualPolish。场景文件和正式存档校验保持一致。

## 绿色旋涡

参考用户提供的门内蓝色螺旋，将其改为绿色像素旋涡，不添加石门。ChapterOnePortalEffect 复用原有 Sprite 和材质，用 128 个小色块组成螺旋；每秒 10 次更新旋转位置并量化到像素网格。形状按已有入口宽高投影，适应竖向柜内入口与地面扁椭圆入口。动画由 C#、Time.deltaTime 和 Mathf 驱动，不依赖 Animator、AnimationClip、Particle System 或外部图片生成工具。

Unity 专项检查通过，对比不同时刻的旋涡点坐标，确认持续旋转，并检查两处入口截图。报告与备份见 Recovery/ChapterOne/PortalSpiral；场景文件和正式存档校验不变。
