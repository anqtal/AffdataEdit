# 设置与云端谱面存储

Windows 正式版在 `AffdataEdit.exe` 所在目录保存 `settings.json` 和 `Charts/Cloud/`。
macOS 正式版在 `AffdataEdit.app` 的同级目录保存这些文件，不写入应用包；无后缀的
`AffdataEdit-Updater` 保持现状。Unity Editor 使用项目根目录，与 `Assets/` 同级。
设置弹窗中的“存储”页可以查看位置并打开所在文件夹。

`settings.json` 保存通用、音量、网格、皮肤、快捷键、编辑器外观、Windows 音频输出、
OBS 和实时同步偏好。写入临时文件并校验后原子替换，上一份保留为 `settings.json.bak`。

登录状态单独保存在当前系统用户的数据目录，不放入可分享的设置文件：

- Windows：`%LOCALAPPDATA%/AffdataEdit/Account/account.json`。
- macOS：`~/Library/Application Support/AffdataEdit/Account/account.json`。
- Unity Editor 在上述账号目录下再使用 `Editor/` 子目录，与正式版隔离。

账号文件保存登录令牌、到期时间和用户名，不保存登录 PIN。它是普通 JSON；Windows
继承用户数据目录的访问权限，macOS 账号目录限制为当前用户可访问。分享程序文件夹
不会包含此文件，手动备份账号目录时应按登录凭据处理。

首次运行会迁移旧 PlayerPrefs、旧 `Arcade` 目录里的设置 JSON，以及 `Arcade/Cloud/`。
兼容旧公司名 `AffdataEdit` 使用的 PlayerPrefs 位置，也会迁移开发版本曾使用的 `Charts/Remote/`。
开发版本暂存于 `UserSettings/` 的设置和云端谱面会迁到项目根目录，其他 Unity 用户文件保持原位。
所有新数据写入并验证成功后，清空当前应用的 PlayerPrefs，删除已迁移的旧设置与云端
文件。迁移失败保留未清理的源数据，允许重试；不会递归删除整个旧 `Arcade` 目录。
迁移完成标记避免每次启动重复清空 PlayerPrefs。Unity 自身之后可能重新写入窗口等键。

云端目录中的谱面继续使用远端 ID 命名。同名文件内容不一致时，保留新目录现有文件，
把旧版本放入 `Charts/Cloud/.migration-backup/`。再次打开已下载谱面时，远端版本未变
则复用本地副本；远端版本改变或旧副本没有版本记录时，可以选择打开本地或下载远端。
下载先写入临时目录，完成后再替换；已有完整目录保留到 `.backups/`，失败时恢复。

本地谱面仍在用户选择的原文件夹打开和保存，本版本不导入或搬动本地谱面。
设置、账号数据和 Charts 不属于更新清单，发布脚本会排除这些运行时数据。
