# AFF 扩展编辑

以 Arcade Alpha 为首要参考，复用现有属性编辑窗口。

- Slide：左右边界支持 `-0.5～1.5`；预览竖线复用设置中的 X 网格位置，拖动四角自动吸附到网格（与编辑器相同的 0.3 捕获距离），支持自定义不等距网格。
- 平滑度：选择 Arc 后编辑“平滑度”，默认 1、最小 1；写入 arc 的第 11 个参数，支持撤销和重做。
- Designant：选择 Arc 后勾选“Designant”；写入 `designant` 类型，使用 Alpha 的粉红色，不计入判定和打击音。取消时，有 ArcTap 的 Arc 恢复为 Trace，其他恢复为普通 Arc。
- FloatLane：在 Tap/Hold 的“轨道”输入框填写小数；`0.0` 是左边界，`0.5` 是中央，`1.0` 是右边界，整数仍表示固定轨道 0～5。支持保存、复制、镜像、撤销重做和连续重选位置；打击效果跟随实际位置。创建普通 Tap/Hold 后即可在此修改为 FloatLane。

示例：
```aff
(1000,0.5);
hold(1000,2000,0.25);
arc(1000,2000,0.00,1.00,s,0.00,1.00,0,none,designant,3.0);
```

## Trace 颜色（Arcade Alpha）

时间组属性支持六位十六进制 RGB（大小写均可），例如 `timinggroup(tracecolFF8040_usetracecol)`。`usetracecolor` 是 `usetracecol` 的别名，必须放在有效的 `tracecolRRGGBB` 后面。只影响普通黑线，不改变普通 Arc 或 Designant。

与 Alpha 一致，只有 `tracecol`、无有效颜色或尚未启用自定义颜色时使用金色 Trace 纹理；启用后使用指定 RGB。自定义 Trace 的透明度为 `0.65 × 125/255`，金色为 `0.65 × 188/255`。保存时保留原始属性文本及顺序。

## Arcahv / Redline SceneControl

```aff
scenecontrol(1000,arcahvdistort,0.5,255);
scenecontrol(1000,arcahvdebris,0.5,255);
scenecontrol(2000,redline,1.0,0);
scenecontrol(4000,arcahvdistort,0.5,0);
scenecontrol(4000,arcahvdebris,0.5,0);
```

- `arcahvdistort` / `arcahvdebris`：持续时间单位为秒，最后一个参数为目标透明度（0～255）；0 秒按 Alpha 使用 1 秒过渡。淡入后保持，需用后续透明度为 0 的事件淡出。
- `redline`：持续时间单位为秒，最后一个参数与 Alpha 一样保留但不参与显示；每个事件加载时固定一个随机高度，范围为背景坐标 300～760。复用 Alpha 的贴图、淡入与闪烁动画。
- 效果按谱面时间求值，支持暂停、回退和拖动时间轴；重新载入谱面时清理旧实例。当前通过 AFF 文本配置。

## Arc 转换为 Slide

选中一条或多条 Arc，在右键菜单选择“转换为 Slide”。默认生成天空 Slide，保留时间、时间组和横向轨迹；原 Arc 的高度、颜色及平滑度不映射到 Slide。默认宽度 1/3，靠近横向边界时缩窄，之后可在原有属性窗口调整宽度、曲线或地面属性。

S/Si/So 及其组合沿用横向曲线；B 曲线直接转换成使用 b 缓动的单段 Slide。带 ArcTap 的原 Arc 保留为黑线挂载轨迹。时长不足 2ms 或无法容纳正宽度 Slide 的 Arc 会跳过并提示。整批转换支持一次撤销/重做，恢复原 Arc 及选中状态。

Slide 曲线：`0` 直线、`1` sin、`2` cos、`3` b（与 Arc 的 B 相同的三次平滑曲线）。AFF 也接受 `b`，保存时写为 `3`；左右边界可独立选择。
