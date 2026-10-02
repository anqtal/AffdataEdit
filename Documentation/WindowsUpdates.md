# Windows 构建与增量更新

主项目仓库：<https://github.com/anqtal/AffdataEdit>

独立更新器：<https://github.com/anqtal/AffdataEdit-Updater>

仅构建 Windows x64。每次 push 到 master 自动构建；本地未 push 的 commit 不会触发。
构建成功后获取独立更新器的最新 Release，校验 SHA-256，放进程序目录，上传 R2，
最后发布完整 ZIP 的 GitHub Release。两个仓库均在新 Release 成功发布后删除旧 Release
和对应标签。构建失败保留上一版。

## GitHub 配置

主仓库 Actions Variables：

- `R2_ACCOUNT_ID`：Cloudflare Account ID。
- `R2_BUCKET`：`affdataedit-updates`。
- `R2_PUBLIC_URL`：bucket 公开 HTTPS 根地址，不带 `/windows`。

主仓库 Actions Secrets（不要提交凭据到仓库）：

- `R2_ACCESS_KEY_ID`、`R2_SECRET_ACCESS_KEY`：仅此 bucket 的对象读写权限。
- `UNITY_EMAIL`、`UNITY_PASSWORD`。
- `UNITY_LICENSE` 或 `UNITY_SERIAL`：与 GameCI 激活流程兼容的许可证。

Unity 版本自动读取 `ProjectSettings/ProjectVersion.txt`。当前 Mono Windows 目标使用
Linux runner 交叉构建，需要 GameCI 对应版本的编辑器镜像。
`WindowsCiBuild.Build` 只构建主场景，并拒绝带构建 warning/error 的产物。
Windows 文件选择器先在 Windows runner 用 MSVC 编译 `Native/Windows/FileBrowser.cpp`，
再通过 CI artifact 放入 `Assets/SFB/Plugins/Windows`。它直接调用系统 IFileDialog，
不依赖 Unity 不支持的 Windows Forms。CI 允许这一个生成的未跟踪 DLL 参与构建。

## R2 发布约定

- `windows/objects/<SHA256>`：不可变文件对象，重复内容跳过上传。
- `windows/releases/<commit>.json`：各版本完整文件清单。
- `windows/latest.json`：客户端下载的最新清单，最后上传，`Cache-Control: no-store`。

使用自定义域名时，不要让 CDN Cache Rule 覆盖 latest.json 的 no-store。
目前可使用 bucket 的 r2.dev 公开地址，它有限流，正式分发建议绑定自定义域名。
旧 R2 对象不随旧 GitHub Release 删除，避免正在下载旧清单的客户端失败。

## 客户端

第一次从 GitHub Release 下载并解压完整 ZIP。双击 `AffdataEdit-Updater.exe` 检查更新，
按文件大小和 SHA-256 跳过一致文件，仅下载新增或修改文件。下载校验完成后，
先保存并关闭 AffdataEdit，再按 Enter 应用更新并启动程序。

本地额外文件不删除；远程清单存在的同名本地文件会被覆盖。更新器也作为普通文件更新，
因此从临时目录运行。正常替换失败时回滚；强制终止/断电恢复暂不自动处理。
这是整个文件级的增量更新，大型 Unity 资源文件变化时仍需下载完整文件。

此版入口是独立更新器，尚未在 Unity 设置面板添加“检查更新”按钮。
