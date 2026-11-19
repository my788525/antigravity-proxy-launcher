@echo off
setlocal enabledelayedexpansion

title Antigravity 免 TUN 启动器

echo ========================================================
echo       Antigravity 免 TUN 智能代理启动脚本
echo ========================================================
echo.

:: 1. 尝试从 Windows 系统代理注册表读取端口
set PROXY_PORT=
for /f "tokens=2*" %%a in ('reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings" /v ProxyServer 2^>nul') do (
    set RAW_PROXY=%%b
)

if defined RAW_PROXY (
    for /f "tokens=2 delims=:" %%p in ("%RAW_PROXY%") do (
        for /f "tokens=1 delims=;" %%port in ("%%p") do (
            set CANDIDATE_PORT=%%port
        )
    )
)

:: 2. 检查候选端口或默认端口是否在监听
if defined CANDIDATE_PORT (
    netstat -ano | findstr /r ":!CANDIDATE_PORT! .*LISTENING" >nul 2>nul
    if !errorlevel! equ 0 set PROXY_PORT=!CANDIDATE_PORT!
)

:: 如果注册表端口未在监听，逐一扫描常见本地代理端口 (3067, 3066, 7890, 7897, 10809, 10808)
if not defined PROXY_PORT (
    for %%p in (3067 3066 7890 7897 10809 10808 2080 20811) do (
        if not defined PROXY_PORT (
            netstat -ano | findstr /r ":%%p .*LISTENING" >nul 2>nul
            if !errorlevel! equ 0 (
                set PROXY_PORT=%%p
            )
        )
    )
)

if not defined PROXY_PORT (
    echo [警告] 未检测到正在运行的代理客户端端口！
    echo 请确认 Karing / Clash / v2rayN 等代理软件已启动。
    set /p PROXY_PORT="请输入你的本地代理端口 (例如 3066 或 7890): "
)

echo [OK] 检测到代理端口: %PROXY_PORT%
set PROXY_URL=http://127.0.0.1:%PROXY_PORT%
set NO_PROXY_LIST=localhost,127.0.0.1,::1,.local,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16

:: 3. 注入关键环境变量 (Go 语言核心 language_server 强依赖)
set HTTP_PROXY=%PROXY_URL%
set HTTPS_PROXY=%PROXY_URL%
set ALL_PROXY=%PROXY_URL%
set http_proxy=%PROXY_URL%
set https_proxy=%PROXY_URL%
set all_proxy=%PROXY_URL%
set NO_PROXY=%NO_PROXY_LIST%
set no_proxy=%NO_PROXY_LIST%
set GIT_HTTP_PROXY=%PROXY_URL%
set GIT_HTTPS_PROXY=%PROXY_URL%

:: 4. 检测 Antigravity 安装路径
set APP_EXE=
if exist "%LOCALAPPDATA%\Programs\antigravity\Antigravity.exe" (
    set "APP_EXE=%LOCALAPPDATA%\Programs\antigravity\Antigravity.exe"
) else if exist "%LOCALAPPDATA%\Programs\Antigravity IDE\Antigravity IDE.exe" (
    set "APP_EXE=%LOCALAPPDATA%\Programs\Antigravity IDE\Antigravity IDE.exe"
)

if not defined APP_EXE (
    echo [错误] 未找到 Antigravity 安装路径！
    pause
    exit /b 1
)

echo [OK] 找到程序路径: %APP_EXE%
echo [OK] 正在启动 Antigravity (已注入免 TUN 代理参数)...

start "" "%APP_EXE%" --proxy-server="%PROXY_URL%" --proxy-bypass-list="<-loopback>;127.0.0.1;localhost"

timeout /t 2 >nul
exit
