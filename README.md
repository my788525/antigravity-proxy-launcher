# Antigravity Proxy Launcher

专为 Google Antigravity 设计的免 TUN 智能代理启动工具。自动将本地 HTTP/SOCKS 代理环境变量注入到核心语言服务 (`language_server.exe`)，无需开启 TUN 虚拟网卡即可畅享 AI 辅助开发。

---

## 解决的核心痛点

在默认环境下，Antigravity 常常需要开启网络客户端的 TUN 模式才能连通服务端，原因在于其架构：

1. **界面层 (Electron)**：通过桌面快捷方式传入 `--proxy-server` 参数时，仅前端 Chromium 渲染层能识别该代理。
2. **核心层 (`language_server.exe`)**：由 Go 语言编写的独立后端进程，负责模型通信与工具调用。Go 标准库在 Windows 下**不读取 WinINET 系统代理注册表**，仅识别 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 等环境变量。
3. **TUN 的副作用**：TUN 驱动级虚拟网卡会带来全局流量劫持、管理员权限要求、与 WSL2/Docker/VPN 路由冲突等诸多问题。

本启动器在拉起 Antigravity 主程序前，在进程树级注入标准代理环境变量与环回排除配置，使核心进程能够无缝复用本地代理客户端（如 Karing、Clash、v2rayN 等），无需任何 TUN 虚拟网卡支持。

---

## 主要功能

- **智能探测代理端口**：自动扫描当前已启动的代理客户端活动端口（如 3066/3067、7890/7897、10808/10809 等）与系统代理设置。
- **全链路环境注入**：自动注入 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`，并精确配置 `NO_PROXY`（排除 127.0.0.1 避免本地 IPC 通讯中断）。
- **双端支持**：自动适配 Antigravity 桌面版 (2.0) 与 Antigravity IDE (VS Code 衍生版)。
- **双模式运行**：
  - **图形界面模式**：提供代理连通性测试、端口切换、旧进程安全重启等功能。
  - **静默极速模式 (`--silent`)**：0 毫秒启动延迟，无控制台黑框弹出，适合桌面快捷方式直接调用。
- **一键快捷方式生成**：支持在桌面自动创建静默启动的桌面图标。
- **系统级永久配置（可选）**：支持一键将环境变量写入当前 Windows 用户环境，配置后原版官方图标亦可直接使用。

---

## 快速上手（开箱即用）

1. 前往本仓库的 [Releases 页面](../../releases) 下载最新版的预编译压缩包 `AntigravityLauncher-v1.0.0-windows-x64.zip`。
2. 解压到任意目录（例如 `C:\Users\<你的用户名>\.gemini\antigravity\tools\launcher` 或任意便携文件夹）。
3. 确保你的代理客户端（Karing / Clash / v2rayN 等）已开启普通的“系统代理”或本地端口监听（**无需开启 TUN**）。
4. 双击运行 `AntigravityLauncher.exe`，点击【创建免TUN桌面快捷方式】即可在桌面生成日常启动图标。

---

## 常见疑问与安全性说明 (FAQ)

### Q: 会不会被官方判定为“修改程序”或导致封号？
**A: 绝对不会，完全没有封号风险。**

1. **零文件与内存篡改**：
   - 启动器不会修改 `Antigravity.exe`、`language_server.exe` 或任何包文件中的任何一个字节。
   - 所有官方程序的数字签名、文件哈希均保持 100% 原厂状态，无任何 DLL 注入或内存 Hook。
2. **纯粹使用官方标准接口**：
   - `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 是操作系统及 Go / Node.js 语言网络层的官方标准规范。
   - Google 开发团队在编写 `language_server` 源码时本身就显式内置了这些环境变量的读取逻辑（专为跨国企业/高校受限内网环境设计）。
   - `--proxy-server` 是 Chromium/Electron 官方标准命令行参数。
3. **服务端流量视角完全一致**：
   - 在服务端（`generativelanguage.googleapis.com`）看来，开启 TUN 与使用代理注入启动器的请求出口 IP 都是你的代理节点，且全程采用端到端 TLS 证书握手加密，请求报文与 OAuth 认证 Token 完全一致，服务端无法也不可能将其视为异常。
4. **本质说明**：
   - 本工具等同于一个自动化脚本，替你在启动时声明了代理环境变量，与在终端手动输入 `set HTTP_PROXY=...` 启动程序完全一致，属于完全合规的系统级网络配置。

---

## 源码编译说明

本项目使用 Windows 系统自带的 .NET Framework 编译器（`csc.exe`），无需安装 Visual Studio：

```cmd
build.bat
```

编译完成后输出文件将保存在 `dist/` 目录下。

---

## 开源协议

本项目基于 MIT 协议开源。
