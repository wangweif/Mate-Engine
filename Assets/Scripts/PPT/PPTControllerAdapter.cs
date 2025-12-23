using UnityEngine;
using System.Collections;
using MateEngine.PPT;

/// <summary>
/// PPTController 适配器 - 将新的 PPTService 包装成旧的 PPTController 接口
/// 保持向后兼容性，无需修改现有代码
/// </summary>
public class PPTControllerAdapter : MonoBehaviour
{
    [Header("PPT 配置")]
    public string pptFileName = "test.pptx";
    public string defaultPptFolder = @"C:\Users\JinXuanhui\Desktop";

    [Header("调试设置")]
    public bool enableDebugLogs = true;

    // 内部状态
    private string currentPptPath;
    private bool isPPTOpen = false;
    private int currentSlideIndex = 1;

    // 引用新的 PPTService
    private PPTService pptService;

    void Awake()
    {
        // 获取或创建 PPTService
        pptService = PPTService.Instance;
        if (pptService == null)
        {
            GameObject serviceObj = new GameObject("PPTService");
            pptService = serviceObj.AddComponent<PPTService>();
            DontDestroyOnLoad(serviceObj);
        }

        // 订阅事件
        pptService.OnSlideChanged += OnSlideChanged;
        pptService.OnPresentationClosed += OnPresentationClosed;
        pptService.OnError += OnError;
        pptService.OnConnected += OnConnected;
    }

    void OnDestroy()
    {
        // 取消订阅
        if (pptService != null)
        {
            pptService.OnSlideChanged -= OnSlideChanged;
            pptService.OnPresentationClosed -= OnPresentationClosed;
            pptService.OnError -= OnError;
            pptService.OnConnected -= OnConnected;
        }
    }

    // ==================== 事件回调 ====================

    private void OnSlideChanged(int slideNumber)
    {
        currentSlideIndex = slideNumber;
        Log($"📄 幻灯片切换到第 {slideNumber} 页");
    }

    private void OnPresentationClosed()
    {
        isPPTOpen = false;
        Log("📕 演示文稿已关闭");
    }

    private void OnError(string errorMessage)
    {
        LogError($"❌ PPT错误: {errorMessage}");
    }

    private void OnConnected()
    {
        Log("✅ 已连接到 PPT Host");
    }

    // ==================== 兼容旧接口的方法 ====================

    /// <summary>
    /// 检查PPT是否已打开
    /// </summary>
    public bool IsPPTOpen()
    {
        return isPPTOpen;
    }

    /// <summary>
    /// 打开 PPT 并直接全屏播放
    /// </summary>
    public void OpenPPT()
    {
        if (string.IsNullOrEmpty(currentPptPath))
        {
            LogError("❌ 未设置PPT路径，请先调用 SetPPTInfo");
            return;
        }

        Log($"🚀 开始打开PPT: {currentPptPath}");
        
        if (pptService != null)
        {
            pptService.OpenPresentation(currentPptPath);
            isPPTOpen = true;
            currentSlideIndex = 1;
        }
        else
        {
            LogError("❌ PPTService 未初始化");
        }
    }

    /// <summary>
    /// 下一页
    /// </summary>
    public void NextSlide()
    {
        if (!isPPTOpen)
        {
            LogWarning("⚠ PPT未打开");
            return;
        }

        if (pptService != null)
        {
            pptService.NextSlide();
            Log("➡ 下一页");
        }
    }

    /// <summary>
    /// 上一页
    /// </summary>
    public void PreviousSlide()
    {
        if (!isPPTOpen)
        {
            LogWarning("⚠ PPT未打开");
            return;
        }

        if (pptService != null)
        {
            pptService.PreviousSlide();
            Log("⬅ 上一页");
        }
    }

    /// <summary>
    /// 退出播放
    /// </summary>
    public void ExitSlideShow()
    {
        if (!isPPTOpen)
        {
            Log("⚠ PPT已经关闭");
            return;
        }

        if (pptService != null)
        {
            pptService.ClosePresentation();
            isPPTOpen = false;
            Log("⛔ 退出播放模式");
        }
    }

    /// <summary>
    /// 暂停/继续播放（注意：新系统不支持暂停，这里只是兼容接口）
    /// </summary>
    public void PausePPT()
    {
        LogWarning("⚠ 新的PPT系统不支持暂停功能");
    }

    /// <summary>
    /// 恢复播放（兼容接口）
    /// </summary>
    public void ResumePPT()
    {
        LogWarning("⚠ 新的PPT系统不支持恢复功能");
    }

    /// <summary>
    /// 关闭 PPT
    /// </summary>
    public void ClosePPT()
    {
        if (pptService != null)
        {
            pptService.ClosePresentation();
            isPPTOpen = false;
            Log("🛑 关闭PPT");
        }
    }

    /// <summary>
    /// 强制全屏（兼容接口，新系统自动全屏）
    /// </summary>
    public void ForceFullscreen()
    {
        Log("ℹ️ 新的PPT系统自动全屏播放");
    }

    /// <summary>
    /// 设置PPT文件信息
    /// </summary>
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

    /// <summary>
    /// 获取当前PPT状态信息
    /// </summary>
    public string GetPPTStatus()
    {
        if (string.IsNullOrEmpty(currentPptPath))
            return "未设置PPT文件";

        if (!isPPTOpen)
            return "PPT未打开";

        return $"PPT已打开 - 第 {currentSlideIndex} 页";
    }

    /// <summary>
    /// 获取当前幻灯片编号
    /// </summary>
    public int GetCurrentSlideIndex()
    {
        return currentSlideIndex;
    }

    /// <summary>
    /// 跳转到指定幻灯片
    /// </summary>
    public void GoToSlide(int slideNumber)
    {
        if (!isPPTOpen)
        {
            LogWarning("⚠ PPT未打开");
            return;
        }

        if (pptService != null)
        {
            pptService.GoToSlide(slideNumber);
            Log($"🔢 跳转到第 {slideNumber} 页");
        }
    }

    // ==================== 日志方法 ====================

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
}
