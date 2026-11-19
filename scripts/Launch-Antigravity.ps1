# Antigravity 免 TUN 启动脚本 (PowerShell 版)
param (
    [switch]$Ide,
    [int]$Port = 0
)

# 1. 自动探测代理端口
function Get-ActiveProxyPort {
    # 优先读取系统代理注册表
    try {
        $reg = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings" -ErrorAction SilentlyContinue
        if ($reg.ProxyEnable -eq 1 -and $reg.ProxyServer) {
            if ($reg.ProxyServer -match ":(\d+)") {
                $p = [int]$matches[1]
                if (Test-PortListening $p) { return $p }
            }
        }
    } catch {}

    # 常见代理端口扫描列表 (Karing, Clash, v2rayN 等)
    $candidates = @(3067, 3066, 7890, 7897, 10809, 10808, 2080, 20811)
    foreach ($p in $candidates) {
        if (Test-PortListening $p) { return $p }
    }
    return 0
}

function Test-PortListening([int]$p) {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $iar = $tcp.BeginConnect("127.0.0.1", $p, $null, $null)
        $wait = $iar.AsyncWaitHandle.WaitOne(150, $false)
        if ($wait -and $tcp.Connected) {
            $tcp.EndConnect($iar)
            $tcp.Close()
            return $true
        }
        $tcp.Close()
    } catch {}
    return $false
}

$proxyPort = if ($Port -gt 0) { $Port } else { Get-ActiveProxyPort }

if ($proxyPort -eq 0) {
    Write-Warning "未检测到正在运行的代理客户端，请确保 Karing/Clash 已启动！"
    $proxyPort = Read-Host "请输入本地代理端口 (例如 3066 或 7890)"
}

$proxyUrl = "http://127.0.0.1:$proxyPort"
$noProxy = "localhost,127.0.0.1,::1,.local,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16"

# 2. 注入核心环境变量 (供 language_server 等后台进程使用)
$env:HTTP_PROXY = $proxyUrl
$env:HTTPS_PROXY = $proxyUrl
$env:ALL_PROXY = $proxyUrl
$env:http_proxy = $proxyUrl
$env:https_proxy = $proxyUrl
$env:all_proxy = $proxyUrl
$env:NO_PROXY = $noProxy
$env:no_proxy = $noProxy
$env:GIT_HTTP_PROXY = $proxyUrl
$env:GIT_HTTPS_PROXY = $proxyUrl

# 3. 寻找可执行文件路径
$desktopExe = "$env:LOCALAPPDATA\Programs\antigravity\Antigravity.exe"
$ideExe = "$env:LOCALAPPDATA\Programs\Antigravity IDE\Antigravity IDE.exe"

$targetExe = if ($Ide) { $ideExe } else { $desktopExe }
if (-not (Test-Path $targetExe)) {
    if (Test-Path $desktopExe) { $targetExe = $desktopExe }
    elseif (Test-Path $ideExe) { $targetExe = $ideExe }
    else {
        Write-Error "未找到 Antigravity 安装路径！"
        exit 1
    }
}

Write-Host "[OK] 使用代理: $proxyUrl" -ForegroundColor Green
Write-Host "[OK] 启动程序: $targetExe" -ForegroundColor Cyan

$arguments = "--proxy-server=""$proxyUrl"" --proxy-bypass-list=""<-loopback>;127.0.0.1;localhost"""
Start-Process -FilePath $targetExe -ArgumentList $arguments -WorkingDirectory (Split-Path $targetExe)
