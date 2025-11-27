using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;


public class UISetOnOff : MonoBehaviour
{
    public GameObject target;
    public PPTController pptController;
    public SmartWindowsTTS windowsTTS;
    public static int count = 0;
    public DropdownManager option;

    private bool isPlayingSequence = false; // 防止重复执行

    // 存储从JSON读取的描述文字
    private string[] presentationDescriptions;

    // JSON文件路径
    private string jsonConfigPath = "test.json";

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
        if (isPlayingSequence)
        {
            Debug.LogWarning("⚠ 演示正在进行中，请等待完成");
            return;
        }

        count++;
        Debug.Log("Macaroon按钮被点击了!");

        /*foreach (var handler in AvatarBubbleHandler.ActiveHandlers)
            handler.ToggleBubbleFromUI();*/

        if (count % 2 == 1)
        {
            // 先加载JSON配置
            if (LoadAndSetPPTInfoFromJson())
            {
                // count是1开始播放PPT
                if (pptController != null)
                {
                    Debug.Log("开始打开 PPT...");
                    pptController.OpenPPT();

                    // 使用协程而不是Thread.Sleep
                    StartCoroutine(PlayPresentationSequence());
                }
                else
                {
                    Debug.LogWarning("pptController 未绑定！无法播放 PPT！");
                }
            }
            else
            {
                Debug.LogError("❌ 加载PPT信息失败，无法播放");
            }
        }
        else
        {
            // TODO count不是奇数暂停播放
            // pptController.PausePPT();
            windowsTTS.StopSpeaking();
        }
    }

    /// <summary>
    /// 播放演示序列的协程 - 使用 presentationDescriptions 数组
    /// </summary>
    private IEnumerator PlayPresentationSequence()
    {
        isPlayingSequence = true;
        Debug.Log("🎬 开始播放演示序列");

        // 等待PPT打开（5秒）
        yield return new WaitForSeconds(5f);

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

        if (windowsTTS != null && windowsTTS.IsAvailable())
        {
            for (int i = 0; i < totalPages; i++)
            {
                Debug.Log($"🔢 播放第 {i + 1} 页");

                // 使用 presentationDescriptions 数组中的描述文字
                string description = descriptionsToUse[i];
                Debug.Log($"📝 描述内容: {description}");

                // 协程同步播放描述文字（等待播放完成）
                yield return StartCoroutine(windowsTTS.SpeakCoroutine(description));
                bool speakSuccess = true; // 如果协程没有失败，认为成功

                if (speakSuccess)
                {
                    Debug.Log($"✅ 第 {i + 1} 页描述播放完成");

                    // 等待1秒
                    yield return new WaitForSeconds(1f);

                    // 切换到下一页（最后一页不切换）
                    if (i < totalPages - 1)
                    {
                        pptController.NextSlide();
                        Debug.Log("➡ 切换到下一页");

                        // 等待1秒让翻页完成
                        yield return new WaitForSeconds(1f);
                    }
                }
                else
                {
                    Debug.LogError($"❌ 第 {i + 1} 页描述播放失败，停止序列");
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠ TTS 不可用，跳过语音提示");

            // 即使没有TTS，也自动翻页
            for (int i = 0; i < totalPages; i++)
            {
                pptController.NextSlide();
                Debug.Log($"➡ 切换到第 {i + 1} 页");
                yield return new WaitForSeconds(2f); // 每页停留2秒
            }
        }

        isPlayingSequence = false;
        Debug.Log("🎉 演示序列播放完成");
    }

    /// <summary>
    /// 使用异步TTS的版本（推荐）- 使用 presentationDescriptions 数组
    /// </summary>
    private IEnumerator PlayPresentationSequenceAsync()
    {
        isPlayingSequence = true;
        Debug.Log("🎬 开始异步播放演示序列");

        // 等待PPT打开
        yield return new WaitForSeconds(5f);

        // 确定要播放的描述数组
        string[] descriptionsToUse = presentationDescriptions;
        int totalPages = descriptionsToUse.Length;

        if (totalPages == 0)
        {
            Debug.LogError("❌ 没有可用的描述文字，停止播放");
            isPlayingSequence = false;
            yield break;
        }

        if (windowsTTS != null && windowsTTS.IsAvailable())
        {
            for (int i = 0; i < totalPages; i++)
            {
                Debug.Log($"🔢 播放第 {i + 1} 页");

                // 使用 presentationDescriptions 数组中的描述文字
                string description = descriptionsToUse[i];

                // 使用异步TTS播放（不会阻塞主线程）
                yield return StartCoroutine(SpeakAndWait(description));

                // 等待1秒
                yield return new WaitForSeconds(1f);

                // 切换到下一页（最后一页不切换）
                if (i < totalPages - 1)
                {
                    pptController.NextSlide();
                    Debug.Log("➡ 切换到下一页");

                    // 等待1秒让翻页完成
                    yield return new WaitForSeconds(1f);
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠ TTS 不可用，跳过语音提示");

            // 简单的自动翻页
            for (int i = 0; i < totalPages; i++)
            {
                pptController.NextSlide();
                Debug.Log($"➡ 切换到第 {i + 1} 页");
                yield return new WaitForSeconds(2f);
            }
        }

        isPlayingSequence = false;
        Debug.Log("🎉 异步演示序列播放完成");
    }

    /// <summary>
    /// 异步说话并等待完成的协程
    /// </summary>
    private IEnumerator SpeakAndWait(string text)
    {
        bool speakCompleted = false;
        bool speakSuccess = false;

        // 开始异步播放
        windowsTTS.SpeakAsync(text, (success) => {
            speakCompleted = true;
            speakSuccess = success;
        });

        Debug.Log($"🗣️ 开始朗读: {text}");

        // 等待语音播放完成
        while (!speakCompleted)
        {
            yield return null; // 每帧检查一次
        }

        if (speakSuccess)
        {
            Debug.Log($"✅ 朗读完成: {text}");
        }
        else
        {
            Debug.LogError($"❌ 朗读失败: {text}");
        }
    }

    /// <summary>
    /// 停止当前的演示序列
    /// </summary>
    public void StopPresentation()
    {
        StopAllCoroutines();
        isPlayingSequence = false;

        if (windowsTTS != null)
            windowsTTS.StopSpeaking();

        Debug.Log("⏹️ 停止演示播放");
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
            //option = GameObject.Find("Dropdown").GetComponent<DropdownManager>();
            //print("option:"+option.GetCurrentOptionText());
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
                // 设置PPT信息到控制器
                pptController.SetPPTInfo(pptInfo.filename, pptInfo.file_path);

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
}