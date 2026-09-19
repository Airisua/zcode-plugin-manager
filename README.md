# ZCode Plugin Manager

一个 .NET 8 WPF 插件管理器，默认扫描上一级 `插件` 目录，内置 zcode-keysmith 和 ZCode Token 用量状态栏的安装支持。

## 运行

```powershell
dotnet run --project .\PluginManager.csproj
```

验证：

```powershell
dotnet build .\PluginManager.slnx --nologo
dotnet run --project .\tests\PluginManager.Tests.csproj
```

构建可执行文件：

```powershell
.\scripts\publish.ps1
```

发布产物在 `publish\ZCodePluginManager.exe`，依赖本机的 .NET 8 Desktop Runtime。

## 说明

- 主页面展示当前识别到的插件和安装状态。
- 主页面“设置”里可以指定插件根目录，修改并保存后会重新扫描。
- 详情页显示 README 提炼出的功能说明、安装 / 卸载 / 更新按钮和执行输出。
- 可在详情页设置 ZCode 安装目录和 Python 可执行文件。
- 主页面可以直接粘贴 GitHub 链接添加插件；卡片或详情页可移除本地插件目录。
- 支持通用 `install.py` 和 `install.ps1` 插件；没有通用安装器的仓库仍会添加页面，但安装按钮会禁用。
- 设置保存在 `%APPDATA%\ZCodePluginManager\settings.json`。
- 主页面“检查更新”支持热更新：应用从 GitHub Release 下载 `ZCodePluginManager.zip`，校验 SHA-256 后由外部 updater 等待当前进程退出、替换文件并自动重启。
- 推送 `v*` 标签时，GitHub Actions 会自动构建、测试、打包并创建 Release。
