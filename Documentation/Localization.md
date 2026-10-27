# i18n 接入规划 / i18n integration plan

**状态：设计草案，尚未实现。** 本文记录三语言目标和建议方案，不代表现有版本已支持切换语言。本次只更新文档，不安装包、不更换字体，也不修改界面或谱面格式。

**Status: proposed design, not implemented.** This document records the three-language target and recommended approach. Language switching is not available in the current application. This change adds documentation only, without installing packages, replacing fonts, or changing the UI or chart format.

## 当前基础 / Current baseline

- 界面主要使用 `UnityEngine.UI.Text`、`InputField` 和 `Dropdown`。主场景直接序列化了 200 个 uGUI Text，未发现 TMP 文本组件；运行时生成的设置、账号、云端和更新弹窗也使用 uGUI。

  The UI mainly uses `UnityEngine.UI.Text`, `InputField`, and `Dropdown`. The main scene directly serializes 200 uGUI Text components and no TMP text components. Runtime settings, account, cloud, and update dialogs also use uGUI.

- 主字体是 `NotoSansSC-Regular.otf`；主场景上述文本中有 198 个引用它，另外两个分别引用 `Combo.ttf` 和 `Exo-Regular.ttf`。Arcade-plus 原有的 `NotoSans-Regular.ttf` 仍在仓库中，但它与后来加入的 Noto Sans SC 是不同资源。

  The main font is `NotoSansSC-Regular.otf`, referenced by 198 of those scene text components. The other two use `Combo.ttf` and `Exo-Regular.ttf`. The inherited Arcade-plus `NotoSans-Regular.ttf` is still present, but it is a different asset from the Noto Sans SC font added later.

- 中文字符串分布在场景、prefab 和 C# 中；[manifest.json](../Packages/manifest.json) 尚未引入本地化包。`ArcadeComposeManager` 设置了 `CurrentCulture = zh-Hans`，这只是文化格式设置，不是翻译系统。

  Chinese strings are distributed across scenes, prefabs, and C# code. [manifest.json](../Packages/manifest.json) does not include a localization package. `ArcadeComposeManager` sets `CurrentCulture` to `zh-Hans`; this controls cultural formatting and does not provide translation.

## 默认语言范围 / Built-in language targets

计划默认随程序内置三种语言，安装后离线可用；不是依赖在线翻译服务。

The plan is to bundle three languages for offline use after installation, without requiring an online translation service.

| 语言 / Language | 标识 / Locale | 语言菜单显示 / Menu label |
| --- | --- | --- |
| 简体中文 / Simplified Chinese | `zh-Hans` | 简体中文 |
| 繁体中文 / Traditional Chinese | `zh-Hant` | 繁體中文 |
| 英语 / English | `en` | English |

繁体中文维护独立译文和术语，简繁转换只能辅助起稿，不能代替审校。语言菜单使用各语言自己的名称，方便用户从不熟悉的语言切换回来。

Traditional Chinese will have independently reviewed translations and terminology. Script conversion can help draft translations but cannot replace review. Language names remain in their native forms so users can recover from an unfamiliar selection.

## 方案比较 / Approach comparison

以下是针对本项目的取舍。优先考虑 Unity 官方方案，第三方工具与自建词典作为备选，不以下载量推断适用性。

These tradeoffs are specific to this project. The official Unity approach is preferred; third-party tools and a custom dictionary are alternatives. Popularity alone does not establish suitability.

| 方案 / Approach | 能力与取舍 / Capabilities and tradeoffs |
| --- | --- |
| [Unity Localization](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html) | 官方 UPM 包，提供字符串/资源表、Smart Strings、伪本地化及翻译导入导出。适合在 Unity 中维护；需要接入资源加载与构建流程。<br>Official UPM package with string/asset tables, Smart Strings, pseudo-localization, and translation import/export. Fits Unity authoring, but requires resource-loading and build integration. |
| [I2 Localization](https://www.inter-illusion.com/tools/i2-localization/how-to-localize) | 成熟第三方方案，支持 uGUI、TMP、字体和其他资源的本地化；引入其独立工具链与许可，需要额外评估维护成本。<br>Established third-party tooling for uGUI, TMP, fonts, and other assets. Adds a separate toolchain and licensing considerations that need evaluation. |
| 自建 JSON / CSV 词典 / Custom JSON or CSV dictionary | 数据直观、依赖少，但语言回退、参数格式、界面刷新、翻译校验和编辑工具均需自行维护。<br>Simple data and few dependencies, but fallback, formatting, UI refresh, validation, and authoring tools become project responsibilities. |

**建议采用 `com.unity.localization`，保留现有 uGUI。** 先迁移一组完整的设置控件，验证动态界面和独立构建，再扩展覆盖范围。TMP 迁移是独立决策，不作为 i18n 的前置工作。具体包版本在实施时按项目 Unity 版本核对并锁定。

**Recommend `com.unity.localization` while retaining uGUI.** Start with a complete group of settings controls, validate dynamic UI and standalone builds, then expand coverage. Migrating to TMP is a separate decision, not a prerequisite. Verify and pin the package version against the project’s Unity version when implementation begins.

官方包通过 Addressables 管理语言和翻译表。建议所有首批语言资源本地打包，并将对应内容构建纳入 Windows/macOS 的构建流程，避免只在 Unity Editor 中能找到翻译。[官方资源管理说明](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Addressables.html)

The package manages locales and tables through Addressables. Bundle the initial language resources locally and include their content build in the Windows/macOS build flow, so translations are available outside the Editor. [Official asset-management documentation](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Addressables.html)

## 语言选择与回退 / Locale selection and fallback

建议启动优先级为：已保存的用户选择 → 系统语言匹配 → 简体中文。使用包提供的 Player Pref、System 和 Specific Locale selectors；保存语言代码而非下拉框下标。系统匹配需验证 `zh-CN` / `zh-SG` 到 `zh-Hans`、`zh-TW` / `zh-HK` / `zh-MO` 到 `zh-Hant`、英语区域到 `en` 的映射，不匹配时才使用默认语言。[官方启动选择器说明](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/LocaleSelector.html)

Proposed startup priority: saved user choice → system-language match → Simplified Chinese. Use the package’s Player Pref, System, and Specific Locale selectors, persisting a locale code rather than a dropdown index. Verify mappings from `zh-CN` / `zh-SG` to `zh-Hans`, `zh-TW` / `zh-HK` / `zh-MO` to `zh-Hant`, and English variants to `en`; use the default only when no match exists. [Official startup-selector documentation](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/LocaleSelector.html)

在设置侧栏增加语言选项，切换后更新已打开和随后创建的界面。缺失译文暂回退到完整的简体中文表；三种语言均缺失时显示可识别的键并在开发检查中报错，不能用空白文本或关闭诊断掩盖遗漏。正式宣称支持某语言前，应补齐该版本范围内的译文。

Add a language selector to the settings drawer and refresh both open and newly created UI. Missing translations temporarily fall back to the complete Simplified Chinese table. If all languages lack an entry, show an identifiable key and fail development validation rather than hiding the issue with empty text or disabled diagnostics. Complete the release’s translation scope before claiming support for a language.

## 字符串与界面接入 / Strings and UI integration

建议在 `Assets/Localization/` 下保存 Locale、字符串表和必要的字体资源引用。初期仅分为 `UI` 与 `Messages` 两个集合，使用稳定的语义键，不以中文原句或层级路径作为键。以下仅为设计示例：

Store locales, string tables, and any required font references under `Assets/Localization/`. Start with just `UI` and `Messages` collections, using stable semantic keys instead of Chinese sentences or hierarchy paths. The following are design examples only:

| 键 / Key | 简体中文 / Simplified Chinese | 繁體中文 / Traditional Chinese | 英语 / English |
| --- | --- | --- | --- |
| `settings.playback_speed` | 播放速度 | 播放速度 | Playback speed |
| `project.open` | 打开谱面 | 開啟譜面 | Open chart |
| `project.saved` | 谱面已保存至 {path} | 譜面已儲存至 {path} | Chart saved to {path} |

- **场景与 prefab**：静态标签通过 `LocalizeStringEvent` 绑定 `Text.text`。输入框的占位文字和下拉选项同样纳入翻译，选项刷新时保留选中值，不能触发业务操作。[官方组件说明](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/ComponentLocalizers.html)

  **Scenes and prefabs:** Bind static labels to `Text.text` through `LocalizeStringEvent`. Include input placeholders and dropdown options; refreshing options must preserve selection without triggering application actions. [Official component documentation](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/ComponentLocalizers.html)

- **动态界面**：在 `AdeUiKit` 及相关创建入口传入键与参数。保留动态文本的本地化绑定，响应语言切换；控件销毁时移除订阅。克隆模板后重新绑定目标键，避免沿用原弹窗的文字。

  **Dynamic UI:** Pass keys and arguments through `AdeUiKit` and related creation points. Keep localized bindings for dynamic text, refresh on locale changes, and remove subscriptions when controls are destroyed. Rebind cloned templates so they do not retain the source dialog’s message.

- **参数化提示**：保存路径、数量、难度和错误详情使用完整消息模板与命名参数，避免拼接译文片段。表初始化完成前不要同步假定文字可用；预加载启动需要的表。[官方字符串表说明](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/StringTables.html)

  **Parameterized messages:** Use complete templates with named arguments for paths, counts, difficulties, and error details instead of concatenating translated fragments. Do not assume strings are immediately available before initialization; preload tables needed at startup. [Official string-table documentation](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/StringTables.html)

- **逻辑与文本分离**：枚举、事件名和业务分支依赖稳定值，不比较翻译后的显示文字。用户输入的曲名、作者、文件路径和 AFF 关键字保持原样。对解析错误和已知服务端错误使用消息键与参数；原始诊断信息保留供排查。

  **Separate behavior from text:** Enums, event names, and application branches use stable values, not translated labels. Preserve user-entered titles, artists, paths, and AFF keywords. Format parser diagnostics and known server errors through message keys and arguments, retaining original diagnostic details for troubleshooting.

## 字体、布局与数字格式 / Fonts, layout, and numeric formatting

保留当前 uGUI 字体流程，先验证 Noto Sans SC 的简体与英文显示；为繁体评估对应的 Noto Sans TC / CJK TC 字形与字重。是否替换英文 UI 字体应单独决定。连击和分数字体与编辑器正文分开配置，不因切换语言全部替换。新增字体必须附带对应许可。

Keep the current uGUI font path and first verify Simplified Chinese and English with Noto Sans SC. Evaluate appropriate Noto Sans TC / CJK TC glyph forms and weights for Traditional Chinese. Any change to the English UI font is a separate decision. Configure combo and score fonts separately from editor text, and retain licenses for any new fonts.

不要把 TMP 的 SDF 字体回退设置套用到 uGUI Text。语言切换时同时更新需要切换的字体，检查英文较长标签、繁体字形、输入框和动态弹窗的换行与尺寸。字体导入和“统一中文字体”的编辑器工具也需检查，避免覆盖按语言配置的字体。

Do not apply TMP SDF fallback settings to uGUI Text. Update locale-dependent fonts together with text, and check longer English labels, Traditional Chinese glyph forms, input fields, and dynamic dialog sizing. Audit font import and the existing Chinese-font editor utility so it does not overwrite locale-specific choices.

界面语言与谱面数字格式必须分开。AFF 和协议数据继续使用 `InvariantCulture`，保留小数点、事件参数和保存格式；切换语言不得修改谱面数据或撤销记录。迁移前检查现有强制 `CurrentCulture = zh-Hans` 的用途，不能只把它替换成所选语言就认为完成 i18n。

UI language must remain separate from chart numeric formatting. AFF and protocol data continue using `InvariantCulture`, preserving decimal separators, event arguments, and serialization. Locale changes must not alter charts or undo history. Audit the existing forced `CurrentCulture = zh-Hans` assignment rather than treating a culture change as localization.

## 逐步实施 / Incremental rollout

1. **基础链路**：安装并锁定兼容包，建立三种 Locale、语言选择与本地保存，接入设置侧栏的一组控件，并验证独立构建加载本地翻译资源。

   **Foundation:** Install and pin a compatible package, create the three locales, add language selection and persistence, migrate a group of settings controls, and verify local table loading in a standalone build.

2. **核心编辑界面**：迁移工具栏、属性编辑器、菜单和对话框；处理动态创建、下拉选项、字体与布局，并完成同一范围的三语言译文。

   **Core editing UI:** Migrate toolbars, property editors, menus, and dialogs, including dynamic creation, dropdowns, fonts, layout, and complete translations for all three languages within that scope.

3. **提示与外围流程**：迁移导入保存、AFF 诊断、账号、云端和更新提示。独立更新器属于另一个仓库，其语言支持单独安排，不能随编辑器一并宣称完成。

   **Messages and surrounding flows:** Migrate import/save feedback, AFF diagnostics, account, cloud, and update messages. The standalone updater is a separate repository and needs its own localization work before support can be claimed there.

每一步只做针对性验证：三语言切换与重启恢复、动态窗口刷新、译文键和参数一致性、缺字与溢出，以及切换语言前后同一谱面的保存结果不变。最终覆盖 Windows/macOS 的本地资源加载，修复本次改动引入的 warning/error 后再标记为已支持。

Use focused checks at each step: switching and restoring all three languages, refreshing dynamic windows, key/argument consistency, missing glyphs and overflow, and unchanged chart serialization across locale switches. Verify local resource loading in Windows/macOS builds and resolve introduced warnings/errors before marking the languages as supported.

本地化资源与 Unity 绑定留在 AffdataEdit 中；不向共享几何与谱面算法的 `Affdata.Core` 引入界面或 Localization 包依赖。

Keep localization assets and Unity UI bindings in AffdataEdit. Do not add UI or Localization package dependencies to the shared geometry and chart algorithms in `Affdata.Core`.
