using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Xamin;
using MateEngine.PPT;


public class UISetOnOff : MonoBehaviour
{
    public GameObject target;
    public PPTService pptService;  // 改用新的 PPTService
    public SmartWindowsTTS windowsTTS;
    public static int count = 0;
    public DropdownManager option;
    public Button play;

    private bool isPlayingSequence = false; // 防止重复执行
    private int currentPageIndex = 0; // 当前播放的页面索引
    private Coroutine presentationCoroutine; // 当前演示协程的引用

    // 存储从JSON读取的描述文字
    private string[] presentationDescriptions;

    // JSON文件路径
    private string jsonConfigPath = "test.json";

    // 新增：用于存储每页的恢复时间
    private Dictionary<int, float> pageResumeTimes = new Dictionary<int, float>();
    
    // 新增：跟踪当前加载的PPT路径
    private string currentPPTPath = null;
    
    // 新增：标记是否为自动翻页(用于区分自动翻页和手动翻页)
    private bool isAutoPageChange = false;
    
    // 新增：标记是否正在等待PPT打开(用于忽略打开过程中的关闭事件)
    private bool isWaitingForPPTOpen = false;
    
    public Sprite playImage;
    public Sprite displayImage;

    /// <summary>
    /// 初始化，监听下拉框变化
    /// </summary>
    void Start()
    {
        if (option != null && option.dropdown != null)
        {
            // 监听下拉框值变化事件
            option.dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            Debug.Log("✅ 已注册下拉框变化监听器");
        }

        // 监听PPT页面变化事件
        if (pptService != null)
        {
            pptService.OnSlideChanged += OnPPTSlideChanged;
            pptService.OnPresentationClosed += OnPPTPresentationClosed;
            Debug.Log("✅ 已注册PPT页面变化监听器");
            Debug.Log("✅ 已注册PPT关闭监听器");
        }
    }

    /// <summary>
    /// 下拉框值变化时的回调
    /// </summary>
    private void OnDropdownValueChanged(int index)
    {
        Debug.Log($"🔄 检测到下拉框变化，索引: {index}");
        
        // 如果正在播放，先停止
        if (isPlayingSequence)
        {
            Debug.Log("⏹️ 检测到PPT切换，停止当前播放");
            StopPresentation();
        }
        
        // 重置播放状态和历史记录
        ResetPlaybackState();
    }

    /// <summary>
    /// PPT页面变化时的回调(用户手动翻页)
    /// </summary>
    private void OnPPTSlideChanged(int newSlideNumber)
    {
        Debug.Log($"📄 检测到PPT页面变化: {newSlideNumber}");

        // 检查页面索引是否有效(PPT页码从1开始,数组索引从0开始)
        int newPageIndex = newSlideNumber - 1;
        if (presentationDescriptions == null || newPageIndex < 0 || newPageIndex >= presentationDescriptions.Length)
        {
            Debug.LogWarning($"⚠️ 页面索引超出范围: {newSlideNumber}");
            return;
        }

        // 如果页面没有变化,忽略
        if (newPageIndex == currentPageIndex)
        {
            Debug.Log("📄 页面未变化,忽略");
            return;
        }

        // 如果是自动翻页,不做任何处理
        if (isAutoPageChange)
        {
            Debug.Log($"🤖 自动翻页: 第{currentPageIndex + 1}页 → 第{newSlideNumber}页 (忽略)");
            currentPageIndex = newPageIndex; // 更新索引即可
            return;
        }

        Debug.Log($"🔄 用户手动翻页: 第{currentPageIndex + 1}页 → 第{newSlideNumber}页");

        // 场景1: 播放中翻页 → 暂停播放
        if (isPlayingSequence)
        {
            Debug.Log("⏸️ 播放中检测到翻页,暂停播放");
            
            // 停止当前语音播放
            if (windowsTTS != null && windowsTTS.IsSpeaking())
            {
                Debug.Log("⏹️ 停止当前语音播放");
                windowsTTS.StopSpeaking();
            }

            // 停止当前播放协程
            if (presentationCoroutine != null)
            {
                StopCoroutine(presentationCoroutine);
                presentationCoroutine = null;
            }

            // 清除所有恢复时间记录(翻页后从头播放)
            pageResumeTimes.Clear();

            // 更新当前页面索引
            currentPageIndex = newPageIndex;

            // 设置为暂停状态
            isPlayingSequence = false;
            
            // 更新按钮为播放图标
            if (play != null && playImage != null)
            {
                play.image = playImage;
            }

            Debug.Log($"✅ 已暂停在第{newSlideNumber}页,等待用户点击播放按钮");
        }
        // 场景2: 暂停时翻页 → 智能恢复逻辑
        else
        {
            Debug.Log("⏸️ 暂停状态下检测到翻页");
            
            // 更新当前页面索引
            currentPageIndex = newPageIndex;
            
            Debug.Log($"✅ 已更新当前页为第{newSlideNumber}页,点击播放将从此页开始");
        }
    }

    /// <summary>
    /// PPT演示文稿关闭时的回调
    /// </summary>
    private void OnPPTPresentationClosed()
    {
        Debug.Log("📕 检测到PPT演示文稿关闭");

        // 如果当前正在等待PPT打开,忽略此事件
        // (PPT打开过程中会触发关闭事件,这是正常的)
        if (isWaitingForPPTOpen)
        {
            Debug.Log("ℹ️ 正在等待PPT打开,忽略关闭事件");
            return;
        }

        // 停止语音播放
        if (windowsTTS != null && windowsTTS.IsSpeaking())
        {
            Debug.Log("⏹️ 停止语音播放");
            windowsTTS.StopSpeaking();
        }

        // 停止播放协程
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        // 重置播放状态
        isPlayingSequence = false;
        currentPageIndex = 0;
        count = 0;
        pageResumeTimes.Clear();
        isAutoPageChange = false;

        // 更新按钮为播放图标
        if (play != null && playImage != null)
        {
            play.image = playImage;
        }

        Debug.Log("✅ 已停止播放并重置状态");
    }

    /// <summary>
    /// 处理PPT路径变更
    /// </summary>
    /// <param name="newPath">新的PPT路径</param>
    /// <param name="resetButton">是否重置按钮状态，默认为true</param>
    /// <param name="resetCount">是否重置计数器，默认为true</param>
    private void HandlePPTPathChange(string newPath, bool resetButton = true, bool resetCount = true)
    {
        Debug.Log($"🔄 检测到PPT路径变更");
        Debug.Log($"   旧路径: {currentPPTPath}");
        Debug.Log($"   新路径: {newPath}");
        
        // 关闭当前打开的PPT
        if (pptService != null && pptService.IsConnected())
        {
            Debug.Log("🛑 关闭当前PPT...");
            pptService.ClosePresentation();
        }
        
        // 重置所有播放状态（但可能不重置按钮和计数器）
        ResetPlaybackState(resetButton, resetCount);
        
        // 更新当前PPT路径
        currentPPTPath = newPath;
        
        Debug.Log("✅ PPT路径变更处理完成");
    }

    /// <summary>
    /// 重置播放状态（不关闭PPT）
    /// </summary>
    /// <param name="resetButton">是否重置按钮图标，默认为true</param>
    /// <param name="resetCount">是否重置计数器，默认为true</param>
    private void ResetPlaybackState(bool resetButton = true, bool resetCount = true)
    {
        // 停止演示协程
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        // 重置所有播放相关的状态
        isPlayingSequence = false;
        currentPageIndex = 0;
        if (resetCount)
        {
            count = 0;
        }
        pageResumeTimes.Clear();

        // 停止语音播放
        if (windowsTTS != null)
        {
            windowsTTS.StopSpeaking();
        }

        // 重置播放按钮图标为播放状态（如果需要）
        if (resetButton && play != null && playImage != null)
        {
            play.image = playImage;
        }

        Debug.Log("🔄 播放状态已完全重置（历史记录已清除）");
    }

    public void ToggleTarget()
    {
        Debug.Log("ToggleTarget");
        if (target != null)
            target.SetActive(!target.activeSelf);
    }

    public void SetOnOff(GameObject obj)
    {
        Debug.Log("objname:" + obj.name);
        if (obj != null)
            obj.SetActive(!obj.activeSelf);
    }

    public void ToggleAccessoryByName(string ruleName)
    {
        Debug.Log("ToggleAccessoryByName");
        foreach (var handler in AccessoiresHandler.ActiveHandlers)
        {
            foreach (var rule in handler.rules)
            {
                if (rule.ruleName == ruleName)
                {
                    rule.isEnabled = !rule.isEnabled;
                    break;
                }
            }
        }
    }

    public void ToggleBubbleFeature()
    {
        Debug.Log($"Macaroon按钮被点击了! 当前状态: isPlayingSequence={isPlayingSequence}, count={count}");

        // 如果当前正在播放,则暂停
        if (isPlayingSequence)
        {
            Debug.Log("⏸️ 暂停播放演示");
            play.image = playImage;
            PausePresentation();
            count++; // 增加计数,下次点击将恢复播放
        }
        // 如果当前暂停,则播放
        else
        {
            // 第一次点击(count == 0)：开始播放
            if (count == 0)
            {
                play.image = displayImage;
                Debug.Log("🎬 第一次点击 - 开始播放演示");
                if (LoadAndSetPPTInfoFromJson())
                {
                    if (pptService != null)
                    {
                        Debug.Log("开始打开 PPT...");
                        pptService.OpenPresentation(currentPPTPath);
                        currentPageIndex = 0; // 重置页面索引
                        presentationCoroutine = StartCoroutine(PlayPresentationSequence());
                        count++; // 增加计数
                    }
                    else
                    {
                        Debug.LogWarning("pptService 未绑定！无法播放 PPT！");
                    }
                }
                else
                {
                    Debug.LogError("❌ 加载PPT信息失败，无法播放");
                }
            }
            // 其他情况：继续播放
            else
            {
                Debug.Log("▶️ 继续播放演示");
                play.image = displayImage;
                presentationCoroutine = StartCoroutine(ResumePresentationSequence());
                count++; // 增加计数
            }
        }
    }

    /// <summary>
    /// 暂停演示播放
    /// </summary>
    private void PausePresentation()
    {
        // 停止演示协程
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        // 暂停语音播放并记录当前位置
        if (windowsTTS != null && windowsTTS.IsSpeaking())
        {
            // 保存当前页面的播放位置
            float currentTime = windowsTTS.PauseSpeaking();
            pageResumeTimes[currentPageIndex] = currentTime;
            Debug.Log($"⏸️ 保存第 {currentPageIndex + 1} 页播放位置: {currentTime:F2}s");
        }

        // 暂停PPT播放（使用黑屏而不是空格键，避免翻页）
        /*if (pptController != null)
        {
            pptController.PausePPT();
        }*/

        isPlayingSequence = false;
        Debug.Log("⏸️ 演示已暂停");
    }

    /// <summary>
    /// 继续播放演示序列
    /// </summary>
    private IEnumerator ResumePresentationSequence()
    {
        if (isPlayingSequence)
        {
            Debug.LogWarning("⚠ 演示已经在播放中");
            yield break;
        }

        isPlayingSequence = true;
        Debug.Log("▶️ 继续播放演示序列");

        // 恢复PPT播放
        /*if (pptController != null)
        {
            pptController.ResumePPT();
            yield return new WaitForSeconds(1f); // 等待PPT恢复
        }*/

        // 确定要播放的描述数组
        string[] descriptionsToUse = presentationDescriptions;
        int totalPages = descriptionsToUse.Length;

        if (totalPages == 0)
        {
            Debug.LogError("❌ 没有可用的描述文字，停止播放");
            isPlayingSequence = false;
            yield break;
        }

        // 检查PPT是否还在运行（新系统自动管理连接）
        if (pptService != null && !pptService.IsConnected())
        {
            Debug.LogWarning("⚠ PPT连接已断开，等待重连...");
            yield return new WaitForSeconds(3f); // 等待重连
        }

        // 清除所有恢复时间,总是从头播放
        pageResumeTimes.Clear();
        Debug.Log($"▶️ 从第{currentPageIndex + 1}页开始播放");

        // 从当前页面开始继续播放
        if (windowsTTS != null && windowsTTS.IsAvailable())
        {
            for (int i = currentPageIndex; i < totalPages; i++)
            {
                // 检查是否被暂停
                if (!isPlayingSequence)
                {
                    Debug.Log("⏸️ 演示被暂停，停止播放");
                    yield break;
                }

                currentPageIndex = i;
                Debug.Log($"🔢 播放第 {i + 1} 页");

                string description = descriptionsToUse[i];
                Debug.Log($"📝 描述内容: {description}");

                // 检查是否有保存的恢复时间
                bool hasResumeTime = pageResumeTimes.ContainsKey(i);
                float resumeTime = hasResumeTime ? pageResumeTimes[i] : 0f;

                if (hasResumeTime)
                {
                    Debug.Log($"▶️ 从保存的位置恢复播放: {resumeTime:F2}s");
                }

                // 协程同步播放描述文字（等待播放完成）
                yield return StartCoroutine(SpeakWithResume(description, resumeTime));

                // 如果成功播放完成，移除恢复时间记录
                if (hasResumeTime && isPlayingSequence)
                {
                    pageResumeTimes.Remove(i);
                }

                // 检查播放是否被暂停
                if (!isPlayingSequence)
                {
                    Debug.Log($"⏸️ 第 {i + 1} 页播放被暂停");
                    yield break;
                }

                Debug.Log($"✅ 第 {i + 1} 页描述播放完成");

                // 额外等待0.5秒确保完全结束
                yield return new WaitForSeconds(0.5f);

                // 检查是否被暂停
                if (!isPlayingSequence)
                {
                    Debug.Log("⏸️ 演示被暂停，停止播放");
                    yield break;
                }

                // 切换到下一页（最后一页不切换）
                if (i < totalPages - 1)
                {
                    if (pptService != null)
                    {
                        // 设置自动翻页标志
                        isAutoPageChange = true;
                        pptService.NextSlide();
                        Debug.Log("➡ 切换到下一页");
                        
                        // 等待1秒让翻页完成
                        yield return new WaitForSeconds(1f);
                        
                        // 重置自动翻页标志
                        isAutoPageChange = false;
                    }
                }
                else
                {
                    // 播放完成，重置状态
                    currentPageIndex = 0;
                    count = 0;
                    pageResumeTimes.Clear();
                    Debug.Log("🎉 所有页面播放完成");
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠ TTS 不可用，跳过语音提示");

            // 即使没有TTS，也自动翻页
            for (int i = currentPageIndex; i < totalPages; i++)
            {
                if (!isPlayingSequence)
                {
                    Debug.Log("⏸️ 演示被暂停，停止播放");
                    yield break;
                }

                if (pptService != null)
                {
                    pptService.NextSlide();
                    Debug.Log($"➡ 切换到第 {i + 1} 页");
                }
                yield return new WaitForSeconds(2f); // 每页停留2秒
            }
        }

        isPlayingSequence = false;
        Debug.Log("🎉 演示序列播放完成");
    }

    /// <summary>
    /// 播放演示序列的协程 - 使用 presentationDescriptions 数组
    /// </summary>
    private IEnumerator PlayPresentationSequence()
    {
        isPlayingSequence = true;
        isWaitingForPPTOpen = true; // 设置等待标志
        Debug.Log("🎬 开始播放演示序列");

        // 等待PPT打开（5秒）
        yield return new WaitForSeconds(5f);
        
        // 清除等待标志
        isWaitingForPPTOpen = false;
        Debug.Log("✅ PPT打开等待结束");

        // 确定要播放的描述数组
        string[] descriptionsToUse = presentationDescriptions;
        int totalPages = descriptionsToUse.Length;

        if (totalPages == 0)
        {
            Debug.LogError("❌ 没有可用的描述文字，停止播放");
            isPlayingSequence = false;
            yield break;
        }

        Debug.Log($"📄 将播放 {totalPages} 页描述文字");

        // 检查播放状态(可能在等待期间被重置)
        if (!isPlayingSequence)
        {
            Debug.LogWarning("⚠️ 等待PPT打开期间播放状态被重置,停止播放");
            yield break;
        }

        if (windowsTTS != null && windowsTTS.IsAvailable())
        {
            for (int i = 0; i < totalPages; i++)
            {
                // 检查是否被暂停
                if (!isPlayingSequence)
                {
                    Debug.Log("⏸️ 演示被暂停，停止播放");
                    yield break;
                }

                currentPageIndex = i;
                Debug.Log($"🔢 播放第 {i + 1} 页");

                // 使用 presentationDescriptions 数组中的描述文字
                string description = descriptionsToUse[i];
                Debug.Log($"📝 描述内容: {description}");

                // 协程同步播放描述文字（等待播放完成）
                yield return StartCoroutine(SpeakWithResume(description, 0f));

                // 检查播放是否被暂停
                if (!isPlayingSequence)
                {
                    Debug.Log($"⏸️ 第 {i + 1} 页播放被暂停");
                    yield break;
                }

                Debug.Log($"✅ 第 {i + 1} 页描述播放完成");

                // 额外等待0.5秒确保完全结束
                yield return new WaitForSeconds(0.5f);

                // 检查是否被暂停
                if (!isPlayingSequence)
                {
                    Debug.Log("⏸️ 演示被暂停，停止播放");
                    yield break;
                }

                // 切换到下一页（最后一页不切换）
                if (i < totalPages - 1)
                {
                    if (pptService != null)
                    {
                        // 设置自动翻页标志
                        isAutoPageChange = true;
                        pptService.NextSlide();
                        Debug.Log("➡ 切换到下一页");

                        // 等待翻页完成
                        yield return new WaitForSeconds(1f);
                        
                        // 重置自动翻页标志
                        isAutoPageChange = false;
                    }
                }
                else
                {
                    // 播放完成，重置状态
                    currentPageIndex = 0;
                    count = 0;
                    pageResumeTimes.Clear();
                    Debug.Log("🎉 所有页面播放完成");
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠ TTS 不可用，跳过语音提示");

            // 即使没有TTS，也自动翻页
            for (int i = 0; i < totalPages; i++)
            {
                if (!isPlayingSequence)
                {
                    Debug.Log("⏸️ 演示被暂停，停止播放");
                    yield break;
                }

                if (pptService != null)
                {
                    pptService.NextSlide();
                    Debug.Log($"➡ 切换到第 {i + 1} 页");
                }
                yield return new WaitForSeconds(3f); // 每页停留3秒
            }
        }

        isPlayingSequence = false;
        Debug.Log("🎉 演示序列播放完成");
    }

    /// <summary>
    /// 说话并支持从指定位置恢复，并等待播放完成
    /// </summary>
    private IEnumerator SpeakWithResume(string text, float resumeTime)
    {
        if (windowsTTS == null || !windowsTTS.IsAvailable())
        {
            yield break;
        }

        bool speakCompleted = false;
        bool speakSuccess = false;

        // 使用音频缓存的 Speak 方法
        StartCoroutine(windowsTTS.Speak(text, (success) => {
            speakCompleted = true;
            speakSuccess = success;
        }));

        // 等待说话开始
        float waitStartTime = Time.time;
        while (!speakCompleted && Time.time - waitStartTime < 5f) // 5秒超时
        {
            if (windowsTTS.IsSpeaking())
            {
                break; // 已经开始播放，跳出等待
            }
            yield return null;
        }

        // 如果说话没有开始，直接返回
        if (!windowsTTS.IsSpeaking())
        {
            Debug.LogError($"❌ 说话未能开始: {text}");
            yield break;
        }

        Debug.Log($"▶️ 开始播放语音，等待完成...");

        // 关键修改：等待语音播放完成
        yield return StartCoroutine(WaitForSpeechComplete(text));

        Debug.Log($"✅ 语音播放完成: {text}");
    }

    /// <summary>
    /// 等待语音播放完成（新增方法）
    /// </summary>
    private IEnumerator WaitForSpeechComplete(string text)
    {
        if (windowsTTS == null) yield break;

        float startWaitTime = Time.time;
        float maxWaitTime = windowsTTS.EstimateSpeechDuration(text) + 10f; // 预估时间+10秒缓冲

        while (windowsTTS.IsSpeaking() && (Time.time - startWaitTime) < maxWaitTime)
        {
            // 检查是否被暂停
            if (!isPlayingSequence)
            {
                Debug.Log("⏸️ 播放被暂停，停止等待");
                yield break;
            }
            
            // 检查PPT是否还在运行（实时检测）
            if (pptService != null && !pptService.IsConnected())
            {
                Debug.Log("检测到PPT连接已断开，停止语音播放");
                StopPresentation();
                play.image = playImage;
                yield break;
            }

            // 显示播放进度（可选）
            float progress = windowsTTS.GetPlaybackProgress();
            if (Time.frameCount % 60 == 0) // 每60帧打印一次进度
            {
                Debug.Log($"📊 播放进度: {progress:P1}");
            }

            yield return null;
        }

        // 检查是否超时
        if (windowsTTS.IsSpeaking())
        {
            Debug.LogWarning($"⚠️ 语音播放可能未正常结束，强制继续: {text}");
        }
        else
        {
            Debug.Log($"✅ 确认语音播放完成: {text}");
        }
    }

    /// <summary>
    /// 停止当前的演示序列
    /// </summary>
    public void StopPresentation()
    {
        // 停止演示协程
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        isPlayingSequence = false;
        currentPageIndex = 0;
        count = 0;
        pageResumeTimes.Clear();

        if (windowsTTS != null)
            windowsTTS.StopSpeaking();

        if (pptService != null)
            pptService.ClosePresentation();

        Debug.Log("⏹️ 停止演示播放");
    }

    /// <summary>
    /// 重置播放状态（用于重新开始）
    /// </summary>
    public void ResetPlayback()
    {
        // 停止演示协程
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        isPlayingSequence = false;
        currentPageIndex = 0;
        count = 0;
        pageResumeTimes.Clear();

        if (windowsTTS != null)
        {
            windowsTTS.StopSpeaking();
        }

        Debug.Log("🔄 播放状态已重置");
    }

    public void UnsnapAllAvatars()
    {
        Debug.Log("UnsnapAllAvatars");
        foreach (var h in FindObjectsByType<AvatarWindowHandler>(FindObjectsSortMode.None))
            h.ForceExitWindowSitting();
    }

    public void SetAccessoryState(string ruleName, bool state)
    {
        Debug.Log("SetAccessoryState");
        foreach (var handler in AccessoiresHandler.ActiveHandlers)
        {
            foreach (var rule in handler.rules)
            {
                if (rule.ruleName == ruleName)
                {
                    rule.isEnabled = state;
                    break;
                }
            }
        }
    }

    public void ToggleBigScreenFeature()
    {
        Debug.Log("ToggleBigScreenFeature");
        foreach (var handler in AvatarBigScreenHandler.ActiveHandlers)
            handler.ToggleBigScreenFromUI();
    }

    public void ToggleChibiMode()
    {
        Debug.Log("ToggleChibiMode");
        foreach (var chibi in GameObject.FindObjectsByType<ChibiToggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            chibi.ToggleChibiMode();
    }

    public void CloseApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenWebsite(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
    }

    /// <summary>
    /// 从JSON文件加载PPT信息并设置到PPTController
    /// </summary>
    private bool LoadAndSetPPTInfoFromJson()
    {
        try
        {
            // 查找JSON文件路径
            jsonConfigPath = Path.ChangeExtension(option.GetCurrentOptionText(), ".json");
            string jsonFilePath = FindJsonFilePath();

            if (string.IsNullOrEmpty(jsonFilePath) || !File.Exists(jsonFilePath))
            {
                Debug.LogError($"❌ 未找到JSON配置文件: {jsonConfigPath}");
                return false;
            }

            // 读取JSON文件
            string jsonContent = File.ReadAllText(jsonFilePath);
            PPTInfo pptInfo = JsonUtility.FromJson<PPTInfo>(jsonContent);

            if (pptInfo != null && !string.IsNullOrEmpty(pptInfo.file_path))
            {
                // 检查PPT路径是否发生变化
                if (!string.IsNullOrEmpty(currentPPTPath) && currentPPTPath != pptInfo.file_path)
                {
                    // PPT路径已变更，处理变更逻辑（不重置按钮图标和计数器，因为用户刚点击了播放）
                    HandlePPTPathChange(pptInfo.file_path, false, false);
                }
                else if (string.IsNullOrEmpty(currentPPTPath))
                {
                    // 首次加载PPT
                    currentPPTPath = pptInfo.file_path;
                    Debug.Log($"📌 首次加载PPT: {currentPPTPath}");
                }
                
                // 保存PPT路径（新系统不需要SetPPTInfo，直接使用路径）
                currentPPTPath = pptInfo.file_path;

                // 保存描述文字到字符串数组
                presentationDescriptions = pptInfo.desc ?? new string[0];

                Debug.Log($"✅ 成功加载PPT信息:");
                Debug.Log($"   - 文件名: {pptInfo.filename}");
                Debug.Log($"   - 文件路径: {pptInfo.file_path}");
                Debug.Log($"   - 描述文字数量: {presentationDescriptions.Length}");

                return true;
            }
            else
            {
                Debug.LogError("❌ JSON文件格式错误或缺少必要字段");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 读取JSON文件失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 查找JSON文件路径
    /// </summary>
    private string FindJsonFilePath()
    {
        // 尝试在StreamingAssets中查找
        string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "pptinfo", jsonConfigPath);
        if (File.Exists(streamingAssetsPath))
        {
            return streamingAssetsPath;
        }

        // 尝试在项目根目录查找
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string rootPath = Path.Combine(projectRoot, "pptinfo", jsonConfigPath);
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        // 尝试在Assets文件夹中查找
        string assetsPath = Path.Combine(Application.dataPath, "pptinfo", jsonConfigPath);
        if (File.Exists(assetsPath))
        {
            return assetsPath;
        }

        return null;
    }

    /// <summary>
    /// 获取描述文字数组
    /// </summary>
    public string[] GetPresentationDescriptions()
    {
        return presentationDescriptions ?? new string[0];
    }

    /// <summary>
    /// 获取特定索引的描述文字
    /// </summary>
    public string GetDescriptionAt(int index)
    {
        if (presentationDescriptions == null || index < 0 || index >= presentationDescriptions.Length)
        {
            return "描述文字不存在";
        }
        return presentationDescriptions[index];
    }

    /// <summary>
    /// 获取当前播放状态
    /// </summary>
    public string GetPlaybackStatus()
    {
        if (!isPlayingSequence)
        {
            return count == 0 ? "未开始" : "已暂停";
        }
        return $"播放中 - 第 {currentPageIndex + 1} 页";
    }

    /// <summary>
    /// 获取当前播放进度
    /// </summary>
    public float GetCurrentPlaybackProgress()
    {
        if (windowsTTS != null && isPlayingSequence)
        {
            return windowsTTS.GetPlaybackProgress();
        }
        return 0f;
    }

    /// <summary>
    /// 获取当前播放的文本
    /// </summary>
    public string GetCurrentPlayingText()
    {
        if (windowsTTS != null)
        {
            return windowsTTS.GetCurrentText();
        }
        return null;
    }


    /// <summary>
    /// 清理事件订阅
    /// </summary>
    void OnDestroy()
    {
        // 取消下拉框事件订阅
        if (option != null && option.dropdown != null)
        {
            option.dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }

        // 取消PPT事件订阅
        if (pptService != null)
        {
            pptService.OnSlideChanged -= OnPPTSlideChanged;
            pptService.OnPresentationClosed -= OnPPTPresentationClosed;
        }
    }
}