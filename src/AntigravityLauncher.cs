using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;

[assembly: AssemblyTitle("Antigravity Proxy Launcher")]
[assembly: AssemblyDescription("Lightweight proxy injection launcher for Google Antigravity, eliminating the requirement for TUN virtual network adapters.")]
[assembly: AssemblyCompany("Open Source Community")]
[assembly: AssemblyProduct("Antigravity Proxy Launcher")]
[assembly: AssemblyCopyright("Copyright © 2026 MIT License")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace AntigravityLauncher
{
    public class Launcher
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        private const int HWND_BROADCAST = 0xffff;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        public static string DetectedProxy = "";
        public static int DetectedPort = 0;
        public static string AntigravityDesktopPath = "";
        public static string AntigravityIdePath = "";
        public static string TargetAppPath = "";
        public static bool IsIdeSelected = false;

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DetectPaths();
            DetectProxy();

            bool silent = false;
            bool forceRestart = false;
            int forcedPort = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                if (arg == "--silent" || arg == "-s" || arg == "/s")
                {
                    silent = true;
                }
                else if (arg == "--restart" || arg == "-r")
                {
                    forceRestart = true;
                }
                else if (arg == "--ide")
                {
                    IsIdeSelected = true;
                }
                else if (arg.StartsWith("--port="))
                {
                    int.TryParse(arg.Substring(7), out forcedPort);
                }
                else if (arg == "--create-shortcut")
                {
                    CreateDesktopShortcut(false);
                    return;
                }
                else if (arg == "--set-env")
                {
                    if (DetectedPort > 0)
                    {
                        SetUserEnvironmentProxy(DetectedPort);
                    }
                    return;
                }
                else if (arg == "--clear-env")
                {
                    ClearUserEnvironmentProxy();
                    return;
                }
            }

            if (forcedPort > 0)
            {
                DetectedPort = forcedPort;
                DetectedProxy = "http://127.0.0.1:" + forcedPort;
            }

            TargetAppPath = IsIdeSelected ? AntigravityIdePath : AntigravityDesktopPath;
            if (string.IsNullOrEmpty(TargetAppPath) && !IsIdeSelected)
            {
                TargetAppPath = AntigravityIdePath;
            }
            if (string.IsNullOrEmpty(TargetAppPath) && IsIdeSelected)
            {
                TargetAppPath = AntigravityDesktopPath;
            }

            if (silent)
            {
                if (DetectedPort > 0 && !string.IsNullOrEmpty(TargetAppPath) && File.Exists(TargetAppPath))
                {
                    if (forceRestart)
                    {
                        KillRunningAntigravity();
                    }
                    LaunchApp(TargetAppPath, DetectedPort);
                    return;
                }
            }

            Application.Run(new MainForm());
        }

        public static void DetectPaths()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string desktopExe = Path.Combine(localAppData, @"Programs\antigravity\Antigravity.exe");
            if (File.Exists(desktopExe))
            {
                AntigravityDesktopPath = desktopExe;
            }

            string ideExe = Path.Combine(localAppData, @"Programs\Antigravity IDE\Antigravity IDE.exe");
            if (File.Exists(ideExe))
            {
                AntigravityIdePath = ideExe;
            }
        }

        public static void DetectProxy()
        {
            // 1. Try Windows Internet Settings Registry
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (key != null)
                    {
                        object enableObj = key.GetValue("ProxyEnable");
                        int enable = (enableObj is int) ? (int)enableObj : 0;
                        object serverObj = key.GetValue("ProxyServer");
                        string server = serverObj != null ? serverObj.ToString() : "";

                        if (enable == 1 && !string.IsNullOrEmpty(server))
                        {
                            int port = ExtractPort(server);
                            if (port > 0 && IsPortListening(port))
                            {
                                DetectedPort = port;
                                DetectedProxy = "http://127.0.0.1:" + port;
                                return;
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Scan candidate ports (Karing: 3066/3067, Clash: 7890/7897, v2rayN: 10809/10808, Neko: 2080/20811)
            int[] candidates = new int[] { 3067, 3066, 7890, 7897, 10809, 10808, 2080, 20811, 5353 };
            for (int i = 0; i < candidates.Length; i++)
            {
                int p = candidates[i];
                if (IsPortListening(p))
                {
                    DetectedPort = p;
                    DetectedProxy = "http://127.0.0.1:" + p;
                    return;
                }
            }
        }

        public static int ExtractPort(string serverStr)
        {
            if (serverStr.Contains("https="))
            {
                int idx = serverStr.IndexOf("https=");
                int semi = serverStr.IndexOf(';', idx);
                string part = semi > idx ? serverStr.Substring(idx + 6, semi - idx - 6) : serverStr.Substring(idx + 6);
                return ExtractPortFromAddress(part);
            }
            else if (serverStr.Contains("http="))
            {
                int idx = serverStr.IndexOf("http=");
                int semi = serverStr.IndexOf(';', idx);
                string part = semi > idx ? serverStr.Substring(idx + 5, semi - idx - 5) : serverStr.Substring(idx + 5);
                return ExtractPortFromAddress(part);
            }
            return ExtractPortFromAddress(serverStr);
        }

        private static int ExtractPortFromAddress(string addr)
        {
            int colon = addr.LastIndexOf(':');
            if (colon >= 0 && colon < addr.Length - 1)
            {
                int port;
                if (int.TryParse(addr.Substring(colon + 1), out port))
                {
                    return port;
                }
            }
            return 0;
        }

        public static bool IsPortListening(int port)
        {
            try
            {
                using (TcpClient tcp = new TcpClient())
                {
                    IAsyncResult ar = tcp.BeginConnect("127.0.0.1", port, null, null);
                    bool success = ar.AsyncWaitHandle.WaitOne(150, false);
                    if (success && tcp.Connected)
                    {
                        tcp.EndConnect(ar);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool TestGoogleReachable(int port, out string error)
        {
            error = "";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://generativelanguage.googleapis.com");
                req.Proxy = new WebProxy("http://127.0.0.1:" + port);
                req.Timeout = 3500;
                req.Method = "GET";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return true;
                }
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    return true;
                }
                error = wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsAntigravityRunning()
        {
            Process[] procs = Process.GetProcessesByName("Antigravity");
            if (procs.Length > 0) return true;
            procs = Process.GetProcessesByName("Antigravity IDE");
            return procs.Length > 0;
        }

        public static void KillRunningAntigravity()
        {
            string[] names = new string[] { "Antigravity", "Antigravity IDE", "language_server" };
            for (int i = 0; i < names.Length; i++)
            {
                Process[] procs = Process.GetProcessesByName(names[i]);
                for (int j = 0; j < procs.Length; j++)
                {
                    try
                    {
                        procs[j].Kill();
                        procs[j].WaitForExit(1000);
                    }
                    catch { }
                }
            }
        }

        public static void LaunchApp(string exePath, int port)
        {
            string proxyUrl = "http://127.0.0.1:" + port;
            string noProxy = "localhost,127.0.0.1,::1,.local,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16";

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exePath;
            psi.Arguments = string.Format("--proxy-server=\"{0}\" --proxy-bypass-list=\"<-loopback>;127.0.0.1;localhost\"", proxyUrl);
            psi.WorkingDirectory = Path.GetDirectoryName(exePath);
            psi.UseShellExecute = false;

            psi.EnvironmentVariables["HTTP_PROXY"] = proxyUrl;
            psi.EnvironmentVariables["HTTPS_PROXY"] = proxyUrl;
            psi.EnvironmentVariables["ALL_PROXY"] = proxyUrl;
            psi.EnvironmentVariables["http_proxy"] = proxyUrl;
            psi.EnvironmentVariables["https_proxy"] = proxyUrl;
            psi.EnvironmentVariables["all_proxy"] = proxyUrl;
            psi.EnvironmentVariables["NO_PROXY"] = noProxy;
            psi.EnvironmentVariables["no_proxy"] = noProxy;

            psi.EnvironmentVariables["GIT_HTTP_PROXY"] = proxyUrl;
            psi.EnvironmentVariables["GIT_HTTPS_PROXY"] = proxyUrl;

            Process.Start(psi);
        }

        public static void CreateDesktopShortcut(bool forIde)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string launcherExe = Path.Combine(exeDir, "AntigravityLauncher.exe");

            string shortcutName = forIde ? "Antigravity IDE (免TUN启动).lnk" : "Antigravity (免TUN启动).lnk";
            string targetApp = forIde ? AntigravityIdePath : AntigravityDesktopPath;
            string shortcutPath = Path.Combine(desktop, shortcutName);

            string args = forIde ? "--ide --silent" : "--silent";

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = launcherExe;
            shortcut.Arguments = args;
            shortcut.WorkingDirectory = exeDir;
            if (!string.IsNullOrEmpty(targetApp) && File.Exists(targetApp))
            {
                shortcut.IconLocation = targetApp + ",0";
            }
            shortcut.Description = "Antigravity 免 TUN 启动器 (自动注入代理)";
            shortcut.Save();
        }

        public static void SetUserEnvironmentProxy(int port)
        {
            string proxyUrl = "http://127.0.0.1:" + port;
            string noProxy = "localhost,127.0.0.1,::1,.local,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16";

            Environment.SetEnvironmentVariable("HTTP_PROXY", proxyUrl, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", proxyUrl, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("ALL_PROXY", proxyUrl, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("NO_PROXY", noProxy, EnvironmentVariableTarget.User);

            BroadcastSettingChange();
        }

        public static void ClearUserEnvironmentProxy()
        {
            Environment.SetEnvironmentVariable("HTTP_PROXY", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("ALL_PROXY", null, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable("NO_PROXY", null, EnvironmentVariableTarget.User);

            BroadcastSettingChange();
        }

        private static void BroadcastSettingChange()
        {
            try
            {
                UIntPtr result;
                SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 1000, out result);
            }
            catch { }
        }
    }

    public class MainForm : Form
    {
        private Label lblHeader;
        private Label lblProxyStatus;
        private Label lblRunningNotice;
        private TextBox txtPort;
        private Button btnDetectProxy;
        private Button btnTestGoogle;
        private RadioButton rbDesktop;
        private RadioButton rbIde;
        private Button btnLaunch;
        private Button btnRestartAndLaunch;
        private Button btnCreateShortcut;
        private Button btnSetEnv;
        private Button btnClearEnv;
        private Label lblTip;

        public MainForm()
        {
            InitializeComponent();
            RefreshStatus();
        }

        private void InitializeComponent()
        {
            this.Text = "Antigravity 免 TUN 启动器";
            this.Size = new Size(570, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);

            lblHeader = new Label();
            lblHeader.Text = "Antigravity 代理注入启动器 (免 TUN 模式)";
            lblHeader.Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
            lblHeader.ForeColor = Color.FromArgb(15, 23, 42);
            lblHeader.Location = new Point(24, 18);
            lblHeader.AutoSize = true;

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "自动注入 HTTP_PROXY 到 Go 核心引擎 (language_server)，彻底告别 TUN 虚拟网卡";
            lblSubtitle.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(100, 116, 139);
            lblSubtitle.Location = new Point(25, 46);
            lblSubtitle.AutoSize = true;

            GroupBox gbProxy = new GroupBox();
            gbProxy.Text = " 1. 本地代理检测 (Karing / Clash / v2rayN 等) ";
            gbProxy.Location = new Point(25, 75);
            gbProxy.Size = new Size(505, 95);
            gbProxy.ForeColor = Color.FromArgb(51, 65, 85);

            Label lblPort = new Label();
            lblPort.Text = "代理端口 (127.0.0.1):";
            lblPort.Location = new Point(18, 28);
            lblPort.AutoSize = true;

            txtPort = new TextBox();
            txtPort.Location = new Point(165, 25);
            txtPort.Size = new Size(75, 24);

            btnDetectProxy = new Button();
            btnDetectProxy.Text = "重新扫描";
            btnDetectProxy.Location = new Point(255, 23);
            btnDetectProxy.Size = new Size(95, 28);
            btnDetectProxy.Click += BtnDetectProxy_Click;

            btnTestGoogle = new Button();
            btnTestGoogle.Text = "测试 Google 连接";
            btnTestGoogle.Location = new Point(360, 23);
            btnTestGoogle.Size = new Size(130, 28);
            btnTestGoogle.Click += BtnTestGoogle_Click;

            lblProxyStatus = new Label();
            lblProxyStatus.Text = "状态：检测中...";
            lblProxyStatus.Location = new Point(18, 62);
            lblProxyStatus.Size = new Size(470, 22);
            lblProxyStatus.ForeColor = Color.FromArgb(15, 118, 110);

            gbProxy.Controls.Add(lblPort);
            gbProxy.Controls.Add(txtPort);
            gbProxy.Controls.Add(btnDetectProxy);
            gbProxy.Controls.Add(btnTestGoogle);
            gbProxy.Controls.Add(lblProxyStatus);

            GroupBox gbTarget = new GroupBox();
            gbTarget.Text = " 2. 选择启动程序 ";
            gbTarget.Location = new Point(25, 180);
            gbTarget.Size = new Size(505, 75);
            gbTarget.ForeColor = Color.FromArgb(51, 65, 85);

            rbDesktop = new RadioButton();
            rbDesktop.Text = "Antigravity 桌面版 (2.0)";
            rbDesktop.Location = new Point(25, 30);
            rbDesktop.AutoSize = true;
            rbDesktop.Checked = !string.IsNullOrEmpty(Launcher.AntigravityDesktopPath);
            rbDesktop.Enabled = !string.IsNullOrEmpty(Launcher.AntigravityDesktopPath);

            rbIde = new RadioButton();
            rbIde.Text = "Antigravity IDE (VS Code 版)";
            rbIde.Location = new Point(255, 30);
            rbIde.AutoSize = true;
            rbIde.Checked = !rbDesktop.Checked && !string.IsNullOrEmpty(Launcher.AntigravityIdePath);
            rbIde.Enabled = !string.IsNullOrEmpty(Launcher.AntigravityIdePath);

            gbTarget.Controls.Add(rbDesktop);
            gbTarget.Controls.Add(rbIde);

            lblRunningNotice = new Label();
            lblRunningNotice.Location = new Point(25, 260);
            lblRunningNotice.Size = new Size(505, 20);
            lblRunningNotice.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            lblRunningNotice.ForeColor = Color.FromArgb(217, 119, 6);

            btnLaunch = new Button();
            btnLaunch.Text = "一键注入并启动";
            btnLaunch.Location = new Point(25, 285);
            btnLaunch.Size = new Size(160, 42);
            btnLaunch.BackColor = Color.FromArgb(37, 99, 235);
            btnLaunch.ForeColor = Color.White;
            btnLaunch.FlatStyle = FlatStyle.Flat;
            btnLaunch.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            btnLaunch.Cursor = Cursors.Hand;
            btnLaunch.Click += BtnLaunch_Click;

            btnRestartAndLaunch = new Button();
            btnRestartAndLaunch.Text = "重启并注入启动";
            btnRestartAndLaunch.Location = new Point(195, 285);
            btnRestartAndLaunch.Size = new Size(140, 42);
            btnRestartAndLaunch.BackColor = Color.FromArgb(225, 29, 72);
            btnRestartAndLaunch.ForeColor = Color.White;
            btnRestartAndLaunch.FlatStyle = FlatStyle.Flat;
            btnRestartAndLaunch.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            btnRestartAndLaunch.Cursor = Cursors.Hand;
            btnRestartAndLaunch.Click += BtnRestartAndLaunch_Click;

            btnCreateShortcut = new Button();
            btnCreateShortcut.Text = "创建免TUN桌面快捷方式";
            btnCreateShortcut.Location = new Point(345, 285);
            btnCreateShortcut.Size = new Size(185, 42);
            btnCreateShortcut.BackColor = Color.FromArgb(241, 245, 249);
            btnCreateShortcut.ForeColor = Color.FromArgb(30, 41, 59);
            btnCreateShortcut.FlatStyle = FlatStyle.Flat;
            btnCreateShortcut.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);
            btnCreateShortcut.Cursor = Cursors.Hand;
            btnCreateShortcut.Click += BtnCreateShortcut_Click;

            GroupBox gbEnv = new GroupBox();
            gbEnv.Text = " 3. 系统级永久方案 (可选) ";
            gbEnv.Location = new Point(25, 340);
            gbEnv.Size = new Size(505, 65);
            gbEnv.ForeColor = Color.FromArgb(100, 116, 139);

            btnSetEnv = new Button();
            btnSetEnv.Text = "写入用户环境变量 (免启动器直接开)";
            btnSetEnv.Location = new Point(15, 23);
            btnSetEnv.Size = new Size(275, 30);
            btnSetEnv.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            btnSetEnv.Click += BtnSetEnv_Click;

            btnClearEnv = new Button();
            btnClearEnv.Text = "清除环境变量";
            btnClearEnv.Location = new Point(310, 23);
            btnClearEnv.Size = new Size(175, 30);
            btnClearEnv.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            btnClearEnv.Click += BtnClearEnv_Click;

            gbEnv.Controls.Add(btnSetEnv);
            gbEnv.Controls.Add(btnClearEnv);

            lblTip = new Label();
            lblTip.Text = "说明：梯子 (Karing / Clash) 仅需保持【普通系统代理】开启即可，完全无需启动 TUN 虚拟网卡！";
            lblTip.Location = new Point(25, 420);
            lblTip.Size = new Size(505, 30);
            lblTip.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            lblTip.ForeColor = Color.FromArgb(100, 116, 139);

            this.Controls.Add(lblHeader);
            this.Controls.Add(lblSubtitle);
            this.Controls.Add(gbProxy);
            this.Controls.Add(gbTarget);
            this.Controls.Add(lblRunningNotice);
            this.Controls.Add(btnLaunch);
            this.Controls.Add(btnRestartAndLaunch);
            this.Controls.Add(btnCreateShortcut);
            this.Controls.Add(gbEnv);
            this.Controls.Add(lblTip);
        }

        private void RefreshStatus()
        {
            Launcher.DetectProxy();
            if (Launcher.DetectedPort > 0)
            {
                txtPort.Text = Launcher.DetectedPort.ToString();
                lblProxyStatus.Text = string.Format("已检测到本地代理端口：{0} (已成功监听)", Launcher.DetectedPort);
                lblProxyStatus.ForeColor = Color.FromArgb(16, 149, 106);
            }
            else
            {
                txtPort.Text = "7890";
                lblProxyStatus.Text = "未在系统默认端口检测到代理，请手动输入端口。";
                lblProxyStatus.ForeColor = Color.FromArgb(220, 38, 38);
            }

            bool running = Launcher.IsAntigravityRunning();
            if (running)
            {
                lblRunningNotice.Text = "提示：检测到 Antigravity 正在运行中。若此前未走代理，请点击【重启并注入启动】。";
                lblRunningNotice.ForeColor = Color.FromArgb(217, 119, 6);
                btnRestartAndLaunch.Enabled = true;
            }
            else
            {
                lblRunningNotice.Text = "状态：Antigravity 当前未运行，可直接注入启动。";
                lblRunningNotice.ForeColor = Color.FromArgb(100, 116, 139);
                btnRestartAndLaunch.Enabled = false;
            }
        }

        private void BtnDetectProxy_Click(object sender, EventArgs e)
        {
            RefreshStatus();
        }

        private void BtnTestGoogle_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port <= 0)
            {
                MessageBox.Show(this, "请输入有效的代理端口！", "端口错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lblProxyStatus.Text = "正在测试 Google API 连接性...";
            lblProxyStatus.ForeColor = Color.FromArgb(37, 99, 235);
            this.Update();

            string err;
            bool ok = Launcher.TestGoogleReachable(port, out err);
            if (ok)
            {
                lblProxyStatus.Text = string.Format("测试成功！Google Generative Language 接口畅通 (端口: {0})", port);
                lblProxyStatus.ForeColor = Color.FromArgb(16, 149, 106);
                MessageBox.Show(this, "测试成功！通过本地代理已成功连接 Google Generative Language 接口，可免 TUN 正常使用！", "连接成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                lblProxyStatus.Text = string.Format("连接失败: {0}", err);
                lblProxyStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, "连接失败！请确认梯子已开启，且该端口为有效 HTTP 代理端口。\n\n错误信息：" + err, "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLaunch_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port <= 0)
            {
                MessageBox.Show(this, "请输入有效的代理端口！", "端口错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetExe = rbIde.Checked ? Launcher.AntigravityIdePath : Launcher.AntigravityDesktopPath;
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
            {
                MessageBox.Show(this, "未找到目标应用程序文件，请确认 Antigravity 已安装！", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Launcher.LaunchApp(targetExe, port);
                this.WindowState = FormWindowState.Minimized;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "启动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestartAndLaunch_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port <= 0)
            {
                MessageBox.Show(this, "请输入有效的代理端口！", "端口错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetExe = rbIde.Checked ? Launcher.AntigravityIdePath : Launcher.AntigravityDesktopPath;
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
            {
                MessageBox.Show(this, "未找到目标应用程序文件！", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show(this, "确定要退出当前已打开的 Antigravity 进程并以代理模式重新启动吗？\n(未保存的草稿可能会丢失)", "确认重启", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    Launcher.KillRunningAntigravity();
                    System.Threading.Thread.Sleep(800);
                    Launcher.LaunchApp(targetExe, port);
                    RefreshStatus();
                    this.WindowState = FormWindowState.Minimized;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "重启启动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnCreateShortcut_Click(object sender, EventArgs e)
        {
            try
            {
                Launcher.CreateDesktopShortcut(rbIde.Checked);
                MessageBox.Show(this, "免 TUN 桌面快捷方式创建成功！\n以后双击桌面上的【Antigravity (免TUN启动)】即可静默一键启动，自动继承代理，无需开 TUN！", "快捷方式创建成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "创建快捷方式失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSetEnv_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port <= 0)
            {
                MessageBox.Show(this, "请输入有效的代理端口！", "端口错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Launcher.SetUserEnvironmentProxy(port);
                MessageBox.Show(this, string.Format("已成功将 HTTP_PROXY / HTTPS_PROXY 设置到当前用户的 Windows 环境变量 (端口: {0})！\n\n之后无需启动器，直接双击原来的 Antigravity 官方快捷方式也可正常联网。\n(注：已运行的程序需重启后生效)", port), "设置成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "写入环境变量失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClearEnv_Click(object sender, EventArgs e)
        {
            try
            {
                Launcher.ClearUserEnvironmentProxy();
                MessageBox.Show(this, "已成功清除用户级代理环境变量！", "清除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "清除环境变量失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
