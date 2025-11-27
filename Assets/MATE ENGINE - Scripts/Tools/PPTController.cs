using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
public class PPTController : MonoBehaviour
{
    [Header("PPT 配置")]
    public string pptFileName = "test.pptx";
    public string defaultPptFolder = @"C:\Users\JinXuanhui\Desktop";
    [Header("PPT 搜索路径（按优先级）")]
    public List<string> pptSearchPaths = new List<string>
    {
        "./PPTs",
        "../PPTs",
        "%USERPROFILE%/Desktop",
        "%USERPROFILE%/Documents"
    };

    [Header("操作设置")]
    public float activationDelay = 0.5f;
    public float fullscreenDelay = 2f;
    public bool autoCloseOnQuit = true;

    [Header("调试设置")]
    public bool enableDebugLogs = true;

    // 内部状态
    private Process pptProcess;
    private string currentPptPath;
    private bool isProcessing = false;
    private Coroutine currentCoroutine;

    // 键盘输入（控制 PowerPoint 翻页）
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private const int KEYEVENTF_KEYUP = 0x02;
    private const byte VK_RIGHT = 0x27;   // →
    private const byte VK_LEFT = 0x25;    // ←
    private const byte VK_ESC = 0x1B;     // ESC
    private const byte VK_F5 = 0x74;      // F5
    private const byte VK_SPACE = 0x20;   // 空格

    void Start()
    {
        // 初始化时检查PPT文件
        ValidatePPTFile();
    }

    /// <summary>
    /// 验证并查找PPT文件
    /// </summary>
    private void ValidatePPTFile()
    {
        currentPptPath = FindPPTFile();

        if (string.IsNullOrEmpty(currentPptPath))
        {
            LogError($"❌ 未找到PPT文件: {pptFileName}");
            LogError("请检查以下路径:");
            foreach (string path in pptSearchPaths)
            {
                LogError($"  - {ExpandEnvironmentVariables(path)}");
            }
        }
        else
        {
            Log($"✅ 找到PPT文件: {currentPptPath}");
        }
    }

    /// <summary>
    /// 查找PPT文件的完整路径
    /// </summary>
    private string FindPPTFile()
    {
        // 首先检查指定的默认文件夹
        if (!string.IsNullOrEmpty(defaultPptFolder))
        {
            string fullPath = Path.Combine(ExpandEnvironmentVariables(defaultPptFolder), pptFileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // 按优先级搜索配置的路径
        foreach (string searchPath in pptSearchPaths)
        {
            string expandedPath = ExpandEnvironmentVariables(searchPath);
            string fullPath = Path.Combine(expandedPath, pptFileName);

            if (File.Exists(fullPath))
            {
                Log($"📁 在路径中找到PPT: {fullPath}");
                return fullPath;
            }
        }

        return null;
    }

    /// <summary>
    /// 展开环境变量
    /// </summary>
    private string ExpandEnvironmentVariables(string path)
    {
        try
        {
            return Environment.ExpandEnvironmentVariables(path);
        }
        catch
        {
            return path;
        }
    }

    /// <summary>
    /// 检查PPT是否已打开
    /// </summary>
    public bool IsPPTOpen()
    {
        return pptProcess != null && !pptProcess.HasExited;
    }

    /// <summary>
    /// 获取当前PPT状态信息
    /// </summary>
    public string GetPPTStatus()
    {
        if (string.IsNullOrEmpty(currentPptPath))
            return "未找到PPT文件";

        if (!IsPPTOpen())
            return "PPT未打开";

        if (isProcessing)
            return "正在处理中...";

        return "PPT已打开";
    }

    /// <summary>
    /// 日志输出方法
    /// </summary>
    private void Log(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PPTController] {message}");
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[PPTController] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[PPTController] {message}");
    }

  /// <summary>
    /// 异步打开 PPT 并直接全屏播放
    /// </summary>
    public void OpenPPT()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = StartCoroutine(OpenPPTCoroutine());
    }

    /// <summary>
    /// PPT打开协程
    /// </summary>
    private IEnumerator OpenPPTCoroutine()
    {
        if (isProcessing)
        {
            LogWarning("⚠ 正在处理PPT操作，请稍候");
            yield break;
        }

        if (string.IsNullOrEmpty(currentPptPath))
        {
            LogError("❌ 未找到有效的PPT文件");
            yield break;
        }

        if (IsPPTOpen())
        {
            LogWarning("⚠ PPT已经打开");
            yield break;
        }

        isProcessing = true;
        Log("🚀 开始打开PPT...");

        bool shouldTryFallback = false;

        try
        {
            // 方法1：使用命令行参数直接全屏播放
            shouldTryFallback = !OpenPPTDirectSlideShow();
        }
        catch (Exception e)
        {
            LogError($"❌ 直接打开方式失败：{e.Message}");
            shouldTryFallback = true;
        }

        if (shouldTryFallback)
        {
            // 方法2：备用方案 - 先打开再全屏
            Log("🔄 尝试备用打开方式...");
            yield return StartCoroutine(OpenPPTFallbackCoroutine());
        }

        isProcessing = false;
    }

    /// <summary>
    /// 方法1：直接以放映模式启动
    /// </summary>
    private bool OpenPPTDirectSlideShow()
    {
        try
        {
            if (string.IsNullOrEmpty(currentPptPath))
            {
                LogError("PPT路径为空");
                return false;
            }

            // 尝试使用 PowerPoint 的命令行参数
            string powerPointExe = FindPowerPointPath();

            if (!string.IsNullOrEmpty(powerPointExe))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = powerPointExe,
                    Arguments = $"/s \"{currentPptPath}\"", // /s 参数表示直接进入幻灯片放映
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Maximized
                };

                try
                {
                    pptProcess = Process.Start(startInfo);
                    Log("✅ 以全屏放映模式打开 PPT");
                    return true;
                }
                catch (Exception ex)
                {
                    LogWarning($"启动PowerPoint失败: {ex.Message}");
                    return false;
                }
            }
            else
            {
                LogWarning("⚠ 未找到 PowerPoint，尝试使用系统默认方式");
                return false;
            }
        }
        catch (Exception e)
        {
            LogWarning($"⚠ 直接放映模式失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 方法2：备用打开方式协程
    /// </summary>
    private IEnumerator OpenPPTFallbackCoroutine()
    {
        if (string.IsNullOrEmpty(currentPptPath))
        {
            LogError("PPT路径为空");
            yield break;
        }

        // 使用系统默认方式打开
        try
        {
            pptProcess = Process.Start(currentPptPath);
            Log("✅ PPT 已打开，等待后全屏...");
        }
        catch (Exception ex)
        {
            LogError($"❌ 启动PPT失败: {ex.Message}");
            yield break;
        }

        // 等待 PowerPoint 启动后按 F5 全屏
        yield return new WaitForSeconds(fullscreenDelay);

        if (pptProcess != null && !pptProcess.HasExited)
        {
            yield return StartCoroutine(ForceFullscreenCoroutine());
        }
        else
        {
            LogWarning("⚠ PPT进程意外退出");
        }
    }

    /// <summary>
    /// 强制全屏播放
    /// </summary>
    public void ForceFullscreen()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
        currentCoroutine = StartCoroutine(ForceFullscreenCoroutine());
    }

    /// <summary>
    /// 强制全屏播放协程
    /// </summary>
    private IEnumerator ForceFullscreenCoroutine()
    {
        if (!IsPPTOpen())
        {
            LogWarning("⚠ PPT未打开");
            yield break;
        }

        if (IsPowerPointForeground())
        {
            PressKey(VK_F5);
            Log("🎬 强制全屏播放");
        }
        else
        {
            LogWarning("⚠ PowerPoint 不是前台窗口，尝试激活");
            ActivatePowerPointWindow();
            yield return new WaitForSeconds(activationDelay);

            try
            {
                PressF5();
            }
            catch (Exception e)
            {
                LogError($"激活PowerPoint失败: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 查找 PowerPoint 安装路径
    /// </summary>
    private string FindPowerPointPath()
    {
        // 扩展PowerPoint搜索路径，包括更多版本
        string[] possiblePaths = {
            // Office 365/2019/2021 (64-bit)
            @"C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE",
            @"C:\Program Files\Microsoft Office\Office16\POWERPNT.EXE",
            // Office 365/2019/2021 (32-bit)
            @"C:\Program Files (x86)\Microsoft Office\root\Office16\POWERPNT.EXE",
            @"C:\Program Files (x86)\Microsoft Office\Office16\POWERPNT.EXE",
            // Office 2013
            @"C:\Program Files\Microsoft Office\Office15\POWERPNT.EXE",
            @"C:\Program Files (x86)\Microsoft Office\Office15\POWERPNT.EXE",
            // Office 2010
            @"C:\Program Files\Microsoft Office\Office14\POWERPNT.EXE",
            @"C:\Program Files (x86)\Microsoft Office\Office14\POWERPNT.EXE",
            // Microsoft 365 Apps for business
            @"C:\Program Files\Microsoft Office\root\VFS\ProgramFilesX86\Microsoft Office\Office16\POWERPNT.EXE"
        };

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Log($"📱 找到PowerPoint: {path}");
                return path;
            }
        }

        // 尝试通过注册表查找（简化版本）
        try
        {
            string registryPath = FindPowerPointFromRegistry();
            if (!string.IsNullOrEmpty(registryPath))
            {
                Log($"📱 通过注册表找到PowerPoint: {registryPath}");
                return registryPath;
            }
        }
        catch (Exception e)
        {
            LogWarning($"从注册表查找PowerPoint失败: {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// 从注册表查找PowerPoint路径
    /// </summary>
    private string FindPowerPointFromRegistry()
    {
        try
        {
            // 这里简化了注册表查询，实际项目中可以使用Microsoft.Win32.Registry
            // 暂时返回null，在完整的实现中可以添加注册表查询逻辑
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查 PowerPoint 是否是前台窗口
    /// </summary>
    private bool IsPowerPointForeground()
    {
        try
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return false;

            GetWindowThreadProcessId(foregroundWindow, out uint processId);

            if (pptProcess != null && !pptProcess.HasExited)
            {
                return pptProcess.Id == processId;
            }

            // 如果没有 pptProcess 引用，检查进程名
            Process foregroundProcess = Process.GetProcessById((int)processId);
            return foregroundProcess.ProcessName.ToLower().Contains("powerpnt");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 激活 PowerPoint 窗口
    /// </summary>
    private void ActivatePowerPointWindow()
    {
        try
        {
            if (pptProcess != null && !pptProcess.HasExited)
            {
                // 尝试使用 MainWindowHandle
                if (pptProcess.MainWindowHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(pptProcess.MainWindowHandle);
                    return;
                }
            }

            // 备用方法：查找所有 PowerPoint 进程
            Process[] powerpointProcesses = Process.GetProcessesByName("POWERPNT");
            if (powerpointProcesses.Length > 0)
            {
                foreach (Process proc in powerpointProcesses)
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        SetForegroundWindow(proc.MainWindowHandle);
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("⚠ 激活 PowerPoint 窗口失败: " + e.Message);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 下一页
    /// </summary>
    public void NextSlide()
    {
        StartCoroutine(SlideControlCoroutine(() => {
            PressKey(VK_RIGHT);
            Log("➡ 下一页");
        }));
    }

    /// <summary>
    /// 上一页
    /// </summary>
    public void PreviousSlide()
    {
        StartCoroutine(SlideControlCoroutine(() => {
            PressKey(VK_LEFT);
            Log("⬅ 上一页");
        }));
    }

    /// <summary>
    /// 退出播放
    /// </summary>
    public void ExitSlideShow()
    {
        StartCoroutine(SlideControlCoroutine(() => {
            PressKey(VK_ESC);
            Log("⛔ 退出播放模式");
        }));
    }

    /// <summary>
    /// 暂停/继续播放
    /// </summary>
    public void PausePPT()
    {
        StartCoroutine(SlideControlCoroutine(() => {
            PressKey(VK_SPACE);
            Log("⏸️ 暂停/继续播放");
        }));
    }

    /// <summary>
    /// 重新开始播放
    /// </summary>
    public void RestartPPT()
    {
        StartCoroutine(RestartPPTCoroutine());
    }

    /// <summary>
    /// 重新开始播放协程
    /// </summary>
    private IEnumerator RestartPPTCoroutine()
    {
        ExitSlideShow();
        yield return new WaitForSeconds(1f);
        ForceFullscreen();
        Log("🔄 重新开始播放");
    }

    /// <summary>
    /// 幻灯片控制协程
    /// </summary>
    private IEnumerator SlideControlCoroutine(System.Action action)
    {
        if (!IsPPTOpen())
        {
            LogWarning("⚠ PPT未打开");
            yield break;
        }

        if (!IsPowerPointForeground())
        {
            Log("🔄 激活 PowerPoint 窗口");
            ActivatePowerPointWindow();
            yield return new WaitForSeconds(activationDelay);
        }

        if (IsPowerPointForeground())
        {
            action?.Invoke();
        }
        else
        {
            LogWarning("⚠ 无法激活PowerPoint窗口");
        }
    }

    public void PressF5()
    {
        PressKey(VK_F5);
        Debug.Log("全屏播放");
    }

    /// <summary>
    /// 模拟键盘按键
    /// </summary>
    private void PressKey(byte key)
    {
        keybd_event(key, 0, 0, 0);              // 按下
        keybd_event(key, 0, KEYEVENTF_KEYUP, 0); // 弹起
    }

    /// <summary>
    /// 异步关闭 PPT
    /// </summary>
    public void ClosePPT()
    {
        StartCoroutine(ClosePPTCoroutine());
    }

    /// <summary>
    /// 关闭PPT协程
    /// </summary>
    private IEnumerator ClosePPTCoroutine()
    {
        if (!IsPPTOpen())
        {
            Log("⚠ PPT已经关闭");
            yield break;
        }

        Log("🛑 开始关闭PPT...");

        // 先退出播放模式
        yield return StartCoroutine(SlideControlCoroutine(() => {
            PressKey(VK_ESC);
            Log("⛔ 退出播放模式");
        }));

        yield return new WaitForSeconds(1f);

        // 尝试正常关闭
        bool forceKillNeeded = false;
        if (pptProcess != null && !pptProcess.HasExited)
        {
            try
            {
                pptProcess.CloseMainWindow();
                Log("📤 发送关闭窗口命令");
            }
            catch (Exception e)
            {
                LogError($"⚠ 关闭进程时出错: {e.Message}");
                forceKillNeeded = true;
            }

            // 等待进程正常退出
            float timeout = 3f;
            float elapsed = 0f;
            while (elapsed < timeout && pptProcess != null && !pptProcess.HasExited)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (pptProcess != null && !pptProcess.HasExited)
            {
                forceKillNeeded = true;
            }
        }

        // 如果需要强制结束
        if (forceKillNeeded)
        {
            try
            {
                LogWarning("⚠ 正常关闭失败，强制结束进程");
                if (pptProcess != null)
                {
                    pptProcess.Kill();
                }
            }
            catch (Exception killEx)
            {
                LogError($"❌ 强制结束进程失败: {killEx.Message}");
            }
        }

        pptProcess = null;
        Log("✅ PPT已关闭");
    }

    /// <summary>
    /// 强制关闭所有PowerPoint进程
    /// </summary>
    public void ForceCloseAllPowerPoint()
    {
        try
        {
            Process[] powerpointProcesses = Process.GetProcessesByName("POWERPNT");
            int closedCount = 0;

            foreach (Process proc in powerpointProcesses)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                        closedCount++;
                        Log($"🔫 强制结束PowerPoint进程: {proc.Id}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"⚠ 无法结束进程 {proc.Id}: {ex.Message}");
                }
            }

            Log($"✅ 共强制关闭 {closedCount} 个PowerPoint进程");
            pptProcess = null;
        }
        catch (Exception e)
        {
            LogError($"❌ 强制关闭失败: {e.Message}");
        }
    }

    /// <summary>
    /// 应用退出时清理资源
    /// </summary>
    void OnApplicationQuit()
    {
        if (autoCloseOnQuit)
        {
            ClosePPT();
        }

        // 停止所有运行的协程
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
        }
    }

    /// <summary>
    /// OnDisable时清理资源
    /// </summary>
    void OnDisable()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }
    }

    /// <summary>
    /// 设置PPT文件信息
    /// </summary>
    /// <param name="filename">文件名</param>
    /// <param name="filePath">完整文件路径</param>
    public void SetPPTInfo(string filename, string filePath)
    {
        this.pptFileName = filename;
        this.currentPptPath = filePath;
        Log($"✅ 设置PPT信息 - 文件名: {filename}, 路径: {filePath}");
    }

    /// <summary>
    /// 获取当前PPT文件路径
    /// </summary>
    public string GetCurrentPPTPath()
    {
        return currentPptPath;
    }

}