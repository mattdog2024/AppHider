# AppHider - 应用隐藏与网络控制工具

## 项目简介

AppHider 是一个 Windows 桌面应用程序，提供应用隐藏和网络控制功能，用于保护隐私。

## 主要功能

- **应用隐藏**：隐藏指定的应用程序窗口（从任务栏和 Alt+Tab 中移除）
- **网络控制**：快速断开/恢复网络连接
- **隐私模式**：一键激活隐藏应用+断网
- **重启保护**：电脑重启后网络保持断开状态，只能通过紧急恢复解锁
- **后台模式**：可在后台运行，通过全局热键控制
- **安全模式**：检测到异常时自动进入安全模式

## 快捷键

- **Ctrl+Alt+F9**：切换隐私模式（隐藏应用+断网/恢复）
- **Ctrl+Alt+F10**：显示/隐藏主窗口

## 最新版本

**v2.3.6-DEBUG** (2024-12-23)
- ✅ 修复了应用隐藏功能
- ✅ 添加了详细的日志记录
- ✅ 优化了网络恢复流程
- ✅ 完善了重启后网络锁定功能

## 项目结构

```
AppHider/
├── AppHider/                          # 源代码目录
│   ├── Services/                      # 服务层
│   │   ├── PrivacyModeController.cs   # 隐私模式控制器
│   │   ├── AppHiderService.cs         # 应用隐藏服务
│   │   ├── NetworkController.cs       # 网络控制服务
│   │   └── ...
│   ├── Views/                         # 视图层
│   ├── Models/                        # 数据模型
│   └── Utils/                         # 工具类
├── Release-v2.3.6-DEBUG/              # 最新编译输出
├── AppHider-v2.3.6-DEBUG.zip          # 最新发布包
├── AppHider.sln                       # Visual Studio 解决方案
└── 文档和工具脚本...
```

## 使用说明

### 安装

1. 解压 `AppHider-v2.3.6-DEBUG.zip`
2. 以管理员身份运行 `AppHider.exe`
3. 首次运行需要设置密码

### 基本使用

1. **隐藏应用**：
   - 在主窗口勾选要隐藏的应用
   - 按 `Ctrl+Alt+F9` 激活隐私模式
   - 应用会被隐藏，网络会断开

2. **恢复**：
   - 再次按 `Ctrl+Alt+F9` 恢复
   - 或点击"紧急恢复"按钮

3. **重启后**：
   - 如果重启前处于隐私模式，重启后网络保持断开
   - 必须使用"紧急恢复"按钮才能恢复网络
   - 快捷键在此情况下被禁用（安全保护）

## 实用工具脚本

- `以管理员身份运行AppHider.bat` - 以管理员权限启动程序
- `查看日志.bat` - 查看应用日志
- `查看错误日志.bat` - 查看错误日志
- `强制退出AppHider.bat` - 强制退出程序
- `强制退出并恢复网络.bat` - 强制退出并恢复网络
- `紧急退出指南.txt` - 紧急情况处理指南

## 功能文档

- `AUTO_STARTUP_USAGE.md` - 自动启动功能说明
- `BACKGROUND_MODE_USAGE.md` - 后台模式使用说明
- `DIRECTORY_HIDING_USAGE.md` - 目录隐藏功能说明
- `HOTKEY_RELIABILITY_FIX.md` - 热键可靠性修复说明
- `SAFE_MODE_USAGE.md` - 安全模式使用说明
- `WATCHDOG_USAGE.md` - 看门狗功能说明
- `v2.3.5-重启后网络锁定功能.txt` - 重启网络锁定功能详细说明

## 技术栈

- **.NET 8.0** - 应用框架
- **WPF** - 用户界面
- **Win32 API** - 窗口控制和网络管理

## 日志位置

日志文件保存在：`%APPDATA%\AppHider\AppHider_*.log`

## 注意事项

1. 必须以管理员权限运行才能控制网络
2. 重启后网络锁定是安全功能，只能通过紧急恢复解锁
3. 如遇到问题，查看日志文件或使用紧急退出脚本

## 开发

### 编译

```bash
dotnet build AppHider.sln -c Release
```

### 发布

```bash
dotnet publish AppHider/AppHider.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o Release-v2.3.6-DEBUG
```

## 版本历史

- **v2.3.6** - 修复应用隐藏功能，添加详细日志
- **v2.3.5** - 实现重启后网络锁定功能
- **v2.3.4** - 修复菜单热键显示问题
- **v2.3.3** - 热键可靠性改进
- **v2.3.2** - 安全性增强
- **v2.3.1** - 稳定性改进
- **v2.3.0** - 重大功能更新

## 许可证

此项目为个人项目。

## 联系方式

如有问题或建议，请通过 GitHub Issues 反馈。
