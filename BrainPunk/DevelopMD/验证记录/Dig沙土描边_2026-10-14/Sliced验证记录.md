# Sand Sliced支持验证

- 环境：Unity6000.0.59f2／D3D11、URP17.0.4，同已有描边TEMP项目；正式DigSand、SceneSpriteBrushClear和Terrain Editor源码，事件依赖仅为既有隔离替身。
- 命令：Unity `-batchmode -projectPath <TEMP> -executeMethod SandSlicedVerification.Run -logFile sand-sliced-verified.log`；日志含 `SAND_SLICED_PASS=17` 与 `SAND_DUAL_OUTLINE_PASS=26`，退出正常。切片验证后在同次启动调用已有Simple回归，未新增长驻服务或修改正式资源。
- 17项切片断言：Shader支持；原生九宫格与新Shader3×2颜色；实际矩形参数；Size小于边框总和；非等比Scale；非中心pivot；跨边框单洞；描边／洞中心；外扩；URP；输入UV；笔刷宽高比；GPU盖章；CPU进度；透明边框Alpha比例；InitMap几何与遮罩。比较容差允许最多19个边界像素不同，不能写成全部像素完全相同。
- 有色九宫格64×64／PPU64／Border16，实际Size3×2及0.35×0.4、Scale1.2×0.8、pivot0.2×0.7；相机384×256。跨边框洞按实际3×2线性遮罩生成；外扩5px。Simple26项继续覆盖内外独立、GPU像素、URP和模板／烘焙／材质重建。
- 第一次失败 `sand-sliced.log` 的外框采样点在已打开的洞边，改到无洞边缘；第二次 `sand-sliced-final.log` 的TEMP旧Prefab仍绑定原MaskRTSprite（GUID d06ab5cb082b11244add98f9e96ee4d4），按生产绑定设Dig/Sand后通过。相关失败日志保留，无生产算法追加兜底。
- 产物：检查列表、最终／失败日志、测试源码副本、原生／Sliced／跨边框洞／描边／URP PNG在本目录。新一轮Simple检查单独复制为 `sliced-simple-regression-checks.txt`，不覆盖历史双描边证据。
- 未验证：正式资源手工操作、全工程编译、完整PlayMode关卡胜负／重开、真实Collider接触、Android／iOS Shader变体／IL2CPP／GPU预算、真实旋转Atlas／紧网格、Tiled／Flip／剪切、多Sand批处理、运行时直接Size修改。盖章和CPU为实际生产方法隔离调用，不代替完整玩法验证。
- 使用及回滚见DigGame功能说明的Sliced章节。没有写生产Prefab／图片／地图，没有发起Git暂存或提交。
