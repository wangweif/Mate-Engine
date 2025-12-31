// AdvancedChatManager.cs - 集成实时语音对话和TTS功能
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Networking;

public class AdvancedChatManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform messageContainer;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button voiceInputButton; // 语音输入按钮(原sendButton)

    [Header("按钮图标")]
    [SerializeField] private Sprite voiceIcon; // 麦克风图标
    [SerializeField] private Sprite keyboardIcon; // 键盘图标

    [Header("Message Prefabs")]
    [SerializeField] private GameObject userMessagePrefab;
    [SerializeField] private GameObject aiMessagePrefab;

    [Header("Bubble Settings")]
    [SerializeField] private float maxBubbleWidth = 400f;     // 气泡最大宽度
    [SerializeField] private float minBubbleWidth = 80f;      // 气泡最小宽度
    [SerializeField] private float bubblePadding = 20f;       // 气泡内边距
    [SerializeField] private float userMessageRightMargin = 0f;  // 用户消息右边距
    [SerializeField] private float aiMessageLeftMargin = 0f;     // AI消息左边距
    [SerializeField] private float messageSpacing = 12f;          // 消息垂直间距

    [Header("Visual Settings")]
    [SerializeField] private Color userBubbleColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color aiBubbleColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color userTextColor = Color.green;
    [SerializeField] private Color aiTextColor = Color.white;

    [Header("Animation")]
    [SerializeField] private float typingSpeed = 0.05f;

    [Header("远程模型配置")]
    [SerializeField] private bool useRemoteModel = true;

    [Header("RAGFlow配置")]
    [SerializeField] private string ragflowHost = "192.168.8.88";
    [SerializeField] private int ragflowPort = 9380;
    [SerializeField] private string ragflowApiKey = "ragflow-cwZWU5YjBjMzUxODExZjBhNThhMDk2OD";
    [SerializeField] private string ragflowAssistantId = "37fd87c8d3d711f097ac578fc36c86e8";
    [SerializeField] private string ragflowLanguage = "Chinese";

    private float containerWidth;
    private Queue<MessageData> messageQueue = new Queue<MessageData>();
    private bool isProcessingQueue = false;
    private bool chatInProgress = false;
    private GameObject currentAIBubble; // 当前AI气泡的GameObject引用
    private string currentSessionId = "";

    [Header("TTS音频")]
    public AudioSource ttsAudioSource; // 用于播放TTS音频

    [Header("实时语音对话设置")]
    public bool enableRealTimeVoiceChat = true;
    public float vadCheckInterval = 0.1f; // VAD检测间隔（秒）
    public float noSpeechThreshold = 1.0f; // 无效语音阈值（秒）
    public float vadEnergyThreshold = 0.01f; // VAD能量阈值
    public float vadActivityRate = 0.6f; // VAD活动率阈值（60%的块检测到语音才认为有活动）
    public float preRollSeconds = 0.4f; // 预录时长
    public float postRollSeconds = 0.5f; // 尾音保留时长

    // 实时语音对话状态
    private bool isRealTimeVoiceChatActive = false;
    private Coroutine realTimeVoiceChatCoroutine;
    private List<AudioClip> audioSegments = new List<AudioClip>();
    private float lastActiveTime = 0f;
    private int audioFileCount = 0;
    private bool isProcessingAudio = false; // 防止并发处理
    private int lastReadPosition = 0; // 上次读取的音频位置
    private Coroutine currentTTSPlayCoroutine = null; // 当前TTS播放协程
    private bool isTTSPlaying = false; // TTS是否正在播放
    private bool chatCancelledByVoice = false; // 是否因新语音打断了模型输出
    private AudioClip currentTTSClip = null; // 当前TTS音频
    private int preRollSamples;
    private Queue<float> preRollBuffer;
    private bool isInSpeech = false;
    private List<float> currentSpeechBuffer = new List<float>();
    private XunFeiSpeechService xunFeiSpeechService;
    private CancellationTokenSource ttsCts;
    private Animator avatarAnimator;
    private static readonly int isTalkingHash = Animator.StringToHash("isTalking");
    private const int sampleRate = 16000;
    private const int maxRecordingLength = 60; // 最大录制60秒
    private string microphoneDevice;
    private bool isRecording = false;
    private const string TtsVoicePrefsKey = "MATE_ENGINE_TTS_VOICE";

    [System.Serializable]
    public class MessageData
    {
        public string content;
        public bool isUserMessage;
    }

    void Start()
    {
        // userMessagePrefab = Resources.Load<GameObject>("Prefabs/UserMessagePreFab");
        // aiMessagePrefab = Resources.Load<GameObject>("Prefabs/AIMessagePreFab");
        // 初始化 - 语音输入按钮点击事件
        voiceInputButton.onClick.AddListener(OnVoiceInputButtonClicked);

        // 输入框回车发送事件
        inputField.onSubmit.AddListener((text) => SendMessage());

        // 计算容器宽度
        CalculateContainerWidth();

        // 初始化Avatar Animator
        FindAvatarAnimator();

        // 初始化讯飞语音服务
        xunFeiSpeechService = new XunFeiSpeechService();

        // 可选：添加一些初始消息
        //StartCoroutine(InitializeChat());
    }

    /// <summary>
    /// 语音输入按钮点击事件
    /// </summary>
    void OnVoiceInputButtonClicked()
    {
        if (isRealTimeVoiceChatActive)
        {
            // 如果正在语音对话，则停止
            StopRealTimeVoiceChat();
            Debug.Log("停止实时语音对话");
        }
        else
        {
            // 启动实时语音对话
            StartRealTimeVoiceChat();
            Debug.Log("启动实时语音对话");
        }
    }

    /// <summary>
    /// 更新语音按钮图标
    /// </summary>
    void UpdateVoiceButtonIcon(bool isVoiceMode)
    {
        if (voiceInputButton == null) return;

        Image buttonImage = voiceInputButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.sprite = isVoiceMode ? keyboardIcon : voiceIcon;
        }
    }

    void OnDestroy()
    {
        StopRealTimeVoiceChat();
        StopCurrentTTS();
        CleanupAllTemporaryFiles();
    }
    void CleanupAllTemporaryFiles()
    {
        string tempDir = Path.Combine(Application.persistentDataPath, "TTSTemp");
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, true);
                Debug.Log($"已清理TTS临时目录: {tempDir}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"清理TTS临时目录失败: {e.Message}");
            }
        }
    }

    void FindAvatarAnimator()
    {
        // 查找VRM模型中的Animator
        var loader = FindFirstObjectByType<VRMLoader>();
        if (loader != null)
        {
            var current = loader.GetCurrentModel();
            if (current != null)
            {
                avatarAnimator = current.GetComponentInChildren<Animator>();
            }
        }

        if (avatarAnimator == null)
        {
            var modelParent = GameObject.Find("Model");
            if (modelParent != null)
            {
                avatarAnimator = modelParent.GetComponentInChildren<Animator>();
            }
        }
    }

    void CalculateContainerWidth()
    {
        if (messageContainer != null)
        {
            containerWidth = messageContainer.rect.width;
            Debug.Log($"容器宽度: {containerWidth}");
        }
    }

    IEnumerator InitializeChat()
    {
        yield return new WaitForSeconds(0.5f);
        AddMessage("你好！我是AI助手，有什么可以帮您的吗？", false);
    }

    public void SendMessage()
    {
        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        inputField.interactable = false;

        // 暂停当前的TTS播放
        StopCurrentTTS();

        // 添加用户消息
        AddMessage(text, true);

        // 清空输入框
        inputField.text = "";

        // 生成AI回复
        StartCoroutine(ChatWithRAGFlow(text));

        // 保持输入框焦点
        //inputField.ActivateInputField();
    }

    // 使用RAGFlow远程模型
    private IEnumerator ChatWithRAGFlow(string userMessage)
    {
        chatInProgress = true;
        chatCancelledByVoice = false;

        // 创建AI气泡
        currentAIBubble = CreateAIBubble("思考中...");

        // 设置Avatar动画状态
        if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, true);

        bool chatCompleted = false;
        string fullResponse = "";

        // 调用RAGFlow API
        yield return StartCoroutine(CallRAGFlowAPI(
            userMessage,
            (partialResponse) => {
                if (!string.IsNullOrEmpty(partialResponse))
                {
                    fullResponse = partialResponse;
                    UpdateBubbleText(currentAIBubble, partialResponse);
                }
            },
            () => {
                chatCompleted = true;
                Debug.Log($"RAGFlow回复完成:{fullResponse}");
            },
            (error) => {
                chatCompleted = true;
                UpdateBubbleText(currentAIBubble, "抱歉，远程模型调用失败：" + error);
                Debug.LogError(error);
            }
        ));

        // 等待聊天完成
        while (!chatCompleted && !chatCancelledByVoice)
        {
            yield return null;
        }

        // 重置Avatar动画状态
        if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);

        chatInProgress = false;
        ScrollToBottom();

        // 如果没有被打断,播放TTS
        if (!chatCancelledByVoice && !string.IsNullOrEmpty(fullResponse))
        {
            Debug.Log("开始播放TTS");
            StartCoroutine(PlayTTSFromAPI(fullResponse, (success) => {
                if (!success)
                {
                    Debug.LogWarning($"⚠ 语音播放失败: {fullResponse}");
                }
            }));
        }
        inputField.interactable = true;
    }

    // 创建AI气泡
    private GameObject CreateAIBubble(string initialText)
    {
        return CreateMessageObject(initialText, false);
    }

    // 创建用户气泡
    private GameObject CreateUserBubble(string text)
    {
        return CreateMessageObject(text, true);
    }

    public void AddMessage(string message, bool isUserMessage)
    {
        MessageData data = new MessageData
        {
            content = message,
            isUserMessage = isUserMessage
        };

        messageQueue.Enqueue(data);

        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessMessageQueue());
        }
    }

    private IEnumerator ProcessMessageQueue()
    {
        isProcessingQueue = true;

        while (messageQueue.Count > 0)
        {
            MessageData data = messageQueue.Dequeue();

            if (data.isUserMessage)
            {
                // 立即显示用户消息
                CreateUserBubble(data.content);
            }
            else
            {
                // AI消息使用打字机效果
                GameObject messageObj = CreateAIBubble("");
                yield return StartCoroutine(TypeMessageWithEffect(data.content, messageObj));
            }

            // 消息间的小延迟
            if (messageQueue.Count > 0)
                yield return new WaitForSeconds(0.1f);
        }

        isProcessingQueue = false;
    }

    private IEnumerator TypeMessageWithEffect(string message, GameObject messageObj)
    {
        TMP_Text textComponent = messageObj.GetComponentInChildren<TMP_Text>();

        if (textComponent == null) yield break;

        // 打字机效果
        string displayText = "";
        foreach (char letter in message.ToCharArray())
        {
            displayText += letter;
            textComponent.text = displayText;

            // 每次更新文本后重新调整气泡大小
            UpdateBubbleSize(messageObj);

            yield return new WaitForSeconds(typingSpeed);

            // 滚动到底部
            ScrollToBottom();
        }
    }

    private GameObject CreateMessageObject(string message, bool isUserMessage)
    {
        GameObject prefab = isUserMessage ? userMessagePrefab : aiMessagePrefab;
        GameObject messageObj = Instantiate(prefab, messageContainer);

        // 设置文本内容
        TMP_Text textComponent = messageObj.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = message;
        }

        // 设置基础样式
        SetupBubbleStyle(messageObj, isUserMessage);

        // 设置左右位置
        SetupBubblePosition(messageObj, isUserMessage);

        // 立即调整气泡大小
        StartCoroutine(UpdateBubbleSizeCoroutine(messageObj));

        return messageObj;
    }

    // 更新气泡文本
    private void UpdateBubbleText(GameObject bubble, string text)
    {
        if (bubble == null) return;

        TMP_Text textComponent = bubble.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = text;
            StartCoroutine(UpdateBubbleSizeCoroutine(bubble));
        }
    }

    private void SetupBubbleStyle(GameObject bubble, bool isUserMessage)
    {
        // 获取组件
        Image bubbleImage = bubble.GetComponent<Image>();
        TMP_Text textComponent = bubble.GetComponentInChildren<TMP_Text>();

        if (bubbleImage == null || textComponent == null) return;

        if (isUserMessage)
        {
            // 用户消息样式
            bubbleImage.color = userBubbleColor;
            textComponent.color = userTextColor;
            textComponent.alignment = TextAlignmentOptions.Right;

            // 设置气泡阴影（可选）
            SetupShadow(bubble, new Vector2(2, -2), new Color(0, 0, 0, 0.2f));
        }
        else
        {
            // AI消息样式
            bubbleImage.color = aiBubbleColor;
            textComponent.color = aiTextColor;
            textComponent.alignment = TextAlignmentOptions.Left;

            // 设置气泡阴影（可选）
            SetupShadow(bubble, new Vector2(-2, -2), new Color(0, 0, 0, 0.1f));
        }
    }

    private void SetupBubblePosition(GameObject bubble, bool isUserMessage)
    {
        RectTransform rect = bubble.GetComponent<RectTransform>();
        if (rect == null) return;

        if (isUserMessage)
        {
            // 用户消息：右侧
            rect.anchorMin = new Vector2(1, 1);  // 右上角锚点
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);      // 右上角轴心

            // 初始位置（会通过UpdateBubbleSize调整）
            rect.anchoredPosition = new Vector2(-userMessageRightMargin, 0);
        }
        else
        {
            // AI消息：左侧
            rect.anchorMin = new Vector2(0, 1);  // 左上角锚点
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);      // 左上角轴心

            // 初始位置
            rect.anchoredPosition = new Vector2(aiMessageLeftMargin, 0);
        }

        // 设置初始大小
        rect.sizeDelta = new Vector2(minBubbleWidth, 40);
    }

    private IEnumerator UpdateBubbleSizeCoroutine(GameObject bubble)
    {
        // 等待一帧确保文本布局完成
        yield return new WaitForEndOfFrame();

        TMP_Text textComponent = bubble.GetComponentInChildren<TMP_Text>();
        RectTransform bubbleRect = bubble.GetComponent<RectTransform>();

        if (textComponent == null || bubbleRect == null) yield break;

        // 强制文本重新计算
        textComponent.ForceMeshUpdate();

        // 获取文本的实际宽度（考虑自动换行）
        float textWidth = textComponent.preferredWidth;
        float textHeight = textComponent.preferredHeight;

        // 计算气泡宽度（考虑最大最小限制）
        float bubbleWidth = Mathf.Clamp(textWidth + bubblePadding * 2, minBubbleWidth, maxBubbleWidth);

        // 计算气泡高度（文本高度 + 上下边距）
        float bubbleHeight = textHeight + 20f; // 上下各10像素边距

        // 更新气泡大小
        bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

        // 更新位置（保持左右偏移）
        bool isUserMessage = bubble.GetComponent<Image>().color == userBubbleColor;
        float xPos = isUserMessage ? -userMessageRightMargin : aiMessageLeftMargin;
        bubbleRect.anchoredPosition = new Vector2(xPos, bubbleRect.anchoredPosition.y);

        // 更新所有消息的垂直位置
        UpdateAllMessagesVerticalPosition();

        // 滚动到底部
        ScrollToBottom();
    }

    private void UpdateBubbleSize(GameObject bubble)
    {
        StartCoroutine(UpdateBubbleSizeCoroutine(bubble));
    }

    private void UpdateAllMessagesVerticalPosition()
    {
        if (messageContainer == null) return;

        float currentY = -10f; // 从顶部开始，留10像素上边距

        // 从上到下遍历所有消息
        for (int i = 0; i < messageContainer.childCount; i++)
        {
            Transform child = messageContainer.GetChild(i);
            RectTransform rect = child.GetComponent<RectTransform>();

            if (rect != null)
            {
                // 设置垂直位置
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, currentY);

                // 更新下一个消息的Y位置
                currentY -= (rect.rect.height + messageSpacing);
            }
        }

        // 更新Content高度
        UpdateContentHeight(-currentY + 10f);
    }

    private void UpdateContentHeight(float newHeight)
    {
        if (messageContainer != null)
        {
            messageContainer.sizeDelta = new Vector2(messageContainer.sizeDelta.x, newHeight);
        }
    }

    private void ScrollToBottom()
    {
        StartCoroutine(ScrollToBottomCoroutine());
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        yield return new WaitForEndOfFrame();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;

            // 强制更新Canvas
            Canvas.ForceUpdateCanvases();
            scrollRect.Rebuild(CanvasUpdate.Layout);
        }
    }

    private void SetupShadow(GameObject target, Vector2 offset, Color color)
    {
        Shadow shadow = target.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = target.AddComponent<Shadow>();
        }

        shadow.effectColor = color;
        shadow.effectDistance = offset;
    }

    [ContextMenu("清空聊天")]
    public void ClearChat()
    {
        foreach (Transform child in messageContainer)
        {
            Destroy(child.gameObject);
        }

        messageQueue.Clear();
        isProcessingQueue = false;
        currentSessionId = ""; // 重置会话ID
    }

    void Update()
    {
        // 按回车发送消息（如果输入框有焦点）
        if (Input.GetKeyDown(KeyCode.Return) && inputField.isFocused)
        {
            SendMessage();
        }
    }

    // ==================== RAGFlow API 调用 ====================
    private IEnumerator CallRAGFlowAPI(string question,
        System.Action<string> onPartialResponse,
        System.Action onComplete,
        System.Action<string> onError)
    {
        // 如果没有session_id，先创建
        if (string.IsNullOrEmpty(currentSessionId))
        {
            yield return StartCoroutine(CreateRAGFlowSession());
            if (string.IsNullOrEmpty(currentSessionId))
            {
                onError?.Invoke("创建会话失败");
                yield break;
            }
        }

        string questionUrl = $"http://{ragflowHost}:{ragflowPort}/api/v1/chats/{ragflowAssistantId}/completions";

        // 转义JSON字符串
        string escapedQuestion = EscapeJsonString(question);
        string jsonData = $"{{\"question\":\"{escapedQuestion}\",\"stream\":true,\"session_id\":\"{currentSessionId}\",\"lang\":\"{ragflowLanguage}\"}}";

        UnityEngine.Networking.UnityWebRequest request = CreateRAGFlowRequest(questionUrl, jsonData);

        var operation = request.SendWebRequest();

        StringBuilder fullResponse = new StringBuilder();
        string lastProcessedText = "";

        while (!operation.isDone)
        {
            yield return null;

            // 处理流式响应
            if (request.downloadHandler != null)
            {
                byte[] rawData = request.downloadHandler.data;
                string responseText = (rawData != null && rawData.Length > 0)
                    ? System.Text.Encoding.UTF8.GetString(rawData)
                    : "";

                // 只处理新增的数据
                if (responseText != lastProcessedText && !string.IsNullOrEmpty(responseText))
                {
                    lastProcessedText = responseText;

                    // 解析流式数据
                    string answer = ParseRAGFlowStreamResponse(responseText);
                    if (!string.IsNullOrEmpty(answer))
                    {
                        onPartialResponse?.Invoke(answer);
                    }
                }
            }
        }

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            onComplete?.Invoke();
        }
        else
        {
            onError?.Invoke($"RAGFlow API请求失败: {request.error}");
        }

        request.Dispose();
    }

    private IEnumerator CreateRAGFlowSession()
    {
        string sessionUrl = $"http://{ragflowHost}:{ragflowPort}/api/v1/chats/{ragflowAssistantId}/sessions";
        string sessionName = $"Unity_Session_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        string jsonData = $"{{\"name\":\"{sessionName}\"}}";

        UnityEngine.Networking.UnityWebRequest request = CreateRAGFlowRequest(sessionUrl, jsonData);
        request.method = "POST";

        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            currentSessionId = ParseSessionId(responseText);
        }

        request.Dispose();
    }

    private UnityEngine.Networking.UnityWebRequest CreateRAGFlowRequest(string url, string jsonData)
    {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + ragflowApiKey);
        request.timeout = 60;
        return request;
    }

    private string ParseSessionId(string jsonResponse)
    {
        try
        {
            int dataIndex = jsonResponse.IndexOf("\"data\"");
            if (dataIndex == -1) return "";

            int idIndex = jsonResponse.IndexOf("\"id\"", dataIndex);
            if (idIndex == -1) return "";

            int idStart = jsonResponse.IndexOf("\"", idIndex + 4) + 1;
            int idEnd = jsonResponse.IndexOf("\"", idStart);
            if (idEnd > idStart)
            {
                return jsonResponse.Substring(idStart, idEnd - idStart);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析会话ID失败: {e.Message}");
        }

        return "";
    }

    private string ParseRAGFlowStreamResponse(string streamText)
    {
        string answer = "";

        try
        {
            string[] lines = streamText.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                // 跳过结束标记
                if (line.Contains("\"data\": true") || line.Contains("\"data\":true"))
                    continue;

                // 处理数据行
                if (line.StartsWith("data:"))
                {
                    string jsonLine = line.Substring(5).Trim();
                    if (!string.IsNullOrEmpty(jsonLine))
                    {
                        // 提取answer
                        int answerIndex = jsonLine.IndexOf("\"answer\"");
                        if (answerIndex != -1)
                        {
                            int answerStart = jsonLine.IndexOf("\"", answerIndex + 8) + 1;
                            int answerEnd = jsonLine.IndexOf("\"", answerStart);
                            if (answerEnd > answerStart)
                            {
                                string extracted = jsonLine.Substring(answerStart, answerEnd - answerStart);
                                answer = UnescapeJsonString(extracted);
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"解析RAGFlow响应失败: {e.Message}");
        }

        return answer;
    }

    // 转义JSON字符串
    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;

        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    // 反转义JSON字符串
    private string UnescapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;

        return str.Replace("\\n", "\n")
                  .Replace("\\r", "\r")
                  .Replace("\\t", "\t")
                  .Replace("\\\"", "\"")
                  .Replace("\\\\", "\\");
    }

    // ==================== TTS语音播放功能 ====================

    /// <summary>
    /// 停止当前TTS播放
    /// </summary>
    void StopCurrentTTS()
    {
        if (ttsCts != null)
        {
            ttsCts.Cancel();
            ttsCts.Dispose();
            ttsCts = null;
        }

        if (currentTTSPlayCoroutine != null)
        {
            StopCoroutine(currentTTSPlayCoroutine);
            currentTTSPlayCoroutine = null;
        }

        AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : null;
        if (audioSourceToUse != null && audioSourceToUse.isPlaying)
        {
            audioSourceToUse.Stop();
            audioSourceToUse.clip = null;
        }

        if (currentTTSClip != null)
        {
            Destroy(currentTTSClip);
            currentTTSClip = null;
        }

        isTTSPlaying = false;
    }

    /// <summary>
    /// 使用讯飞TTS获取音频并播放
    /// </summary>
    IEnumerator PlayTTSFromAPI(string text, System.Action<bool> onComplete)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("TTS文本为空");
            isTTSPlaying = false;
            onComplete?.Invoke(false);
            yield break;
        }

        isTTSPlaying = true;

        // 取消上一请求
        ttsCts?.Cancel();
        ttsCts?.Dispose();
        ttsCts = new CancellationTokenSource();

        if (xunFeiSpeechService == null)
        {
            Debug.LogError("讯飞语音服务未初始化");
            isTTSPlaying = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // 获取当前voice
        string voice = PlayerPrefs.GetString(TtsVoicePrefsKey, "x4_yezi");
        Task<byte[]> ttsTask = xunFeiSpeechService.RequestTtsAsync(text, ttsCts.Token, voice);
        while (!ttsTask.IsCompleted)
        {
            if (ttsCts.IsCancellationRequested || !isTTSPlaying)
            {
                onComplete?.Invoke(false);
                yield break;
            }
            yield return null;
        }

        if (ttsTask.IsCanceled || !ttsTask.IsCompletedSuccessfully)
        {
            Debug.LogError($"讯飞TTS请求失败: {(ttsTask.Exception != null ? ttsTask.Exception.Message : "被取消")}");
            isTTSPlaying = false;
            onComplete?.Invoke(false);
            yield break;
        }

        byte[] audioData = ttsTask.Result;
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogWarning("讯飞TTS返回空音频数据");
            isTTSPlaying = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // 创建临时文件路径
        string tempDir = Path.Combine(Application.persistentDataPath, "TTSTemp");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }
        string tempFilePath = Path.Combine(tempDir, $"tts_{System.Guid.NewGuid()}.mp3");
        File.WriteAllBytes(tempFilePath, audioData);
        Debug.Log($"讯飞TTS音频已保存: {tempFilePath}, 大小: {audioData.Length} 字节");

        AudioClip audioClip = null;
        string fileUrl = "file://" + tempFilePath;
        using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
        {
            yield return audioRequest.SendWebRequest();

            if (audioRequest.result == UnityWebRequest.Result.Success)
            {
                audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
            }
            else
            {
                Debug.LogError($"加载讯飞TTS音频失败: {audioRequest.error}");
                isTTSPlaying = false;
                onComplete?.Invoke(false);
            }
        }

        if (audioClip == null || !isTTSPlaying)
        {
            TryDeleteFile(tempFilePath);
            isTTSPlaying = false;
            onComplete?.Invoke(false);
            yield break;
        }

        AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : null;
        if (audioSourceToUse == null)
        {
            Debug.LogError("没有可用的AudioSource播放TTS");
            Destroy(audioClip);
            TryDeleteFile(tempFilePath);
            isTTSPlaying = false;
            onComplete?.Invoke(false);
            yield break;
        }

        audioSourceToUse.loop = false;
        if (audioSourceToUse.isPlaying)
        {
            audioSourceToUse.Stop();
        }

        currentTTSClip = audioClip;
        audioSourceToUse.clip = audioClip;
        audioSourceToUse.Play();

        while (audioSourceToUse.isPlaying && isTTSPlaying && audioSourceToUse.clip == audioClip)
        {
            yield return null;
        }

        if (!isTTSPlaying || audioSourceToUse.clip != audioClip)
        {
            if (audioSourceToUse.isPlaying)
            {
                audioSourceToUse.Stop();
            }
            Debug.Log("讯飞TTS播放被中断");
        }
        else
        {
            Debug.Log("讯飞TTS播放完成");
        }

        TryDeleteFile(tempFilePath);

        if (currentTTSClip != null)
        {
            Destroy(currentTTSClip);
            currentTTSClip = null;
        }

        isTTSPlaying = false;
        onComplete?.Invoke(true);
    }

    /// <summary>
    /// 尝试删除临时文件
    /// </summary>
    void TryDeleteFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"清理临时文件失败: {e.Message}");
        }
    }

    // ==================== 实时语音对话功能 ====================

    /// <summary>
    /// 启动实时语音对话
    /// </summary>
    public void StartRealTimeVoiceChat()
    {
        if (isRealTimeVoiceChatActive)
        {
            Debug.Log("实时语音对话已在运行中");
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("没有找到麦克风设备");
            return;
        }

        isRealTimeVoiceChatActive = true;
        audioSegments.Clear();
        lastActiveTime = Time.time;
        audioFileCount = 0;
        isProcessingAudio = false;
        isInSpeech = false;
        currentSpeechBuffer.Clear();
        preRollBuffer = null;
        preRollSamples = 0;

        // 切换按钮图标为键盘
        UpdateVoiceButtonIcon(true);

        Debug.Log("开始实时语音对话");
        realTimeVoiceChatCoroutine = StartCoroutine(RealTimeVoiceChatLoop());
    }

    /// <summary>
    /// 停止实时语音对话
    /// </summary>
    public void StopRealTimeVoiceChat()
    {
        if (!isRealTimeVoiceChatActive) return;

        isRealTimeVoiceChatActive = false;

        if (realTimeVoiceChatCoroutine != null)
        {
            StopCoroutine(realTimeVoiceChatCoroutine);
            realTimeVoiceChatCoroutine = null;
        }

        if (isRecording)
        {
            Microphone.End(microphoneDevice);
            isRecording = false;
        }

        // 停止TTS播放
        StopCurrentTTS();
        // 停止模型输出
        CancelCurrentChat();

        audioSegments.Clear();

        // 切换按钮图标为麦克风
        UpdateVoiceButtonIcon(false);

        Debug.Log("实时语音对话已停止");
    }

    /// <summary>
    /// 打断当前模型输出（聊天）
    /// </summary>
    void CancelCurrentChat()
    {
        if (!chatInProgress) return;
        chatCancelledByVoice = true;
        chatInProgress = false;

        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool(isTalkingHash, false);
        }
    }

    /// <summary>
    /// 实时语音对话主循环
    /// </summary>
    IEnumerator RealTimeVoiceChatLoop()
    {
        microphoneDevice = Microphone.devices[0];
        AudioClip recordingClip = Microphone.Start(microphoneDevice, true, maxRecordingLength, sampleRate);
        isRecording = true;
        lastReadPosition = 0;
        lastActiveTime = Time.time;
        preRollSamples = Mathf.Max(1, Mathf.CeilToInt(preRollSeconds * sampleRate * recordingClip.channels));
        preRollBuffer = new Queue<float>(preRollSamples);
        currentSpeechBuffer.Clear();
        isInSpeech = false;

        while (isRealTimeVoiceChatActive)
        {
            yield return new WaitForSeconds(vadCheckInterval);

            if (!isRecording || recordingClip == null) continue;

            // 获取当前录音位置
            int currentPosition = Microphone.GetPosition(microphoneDevice);
            if (currentPosition < 0) continue;

            // 处理循环缓冲区的情况
            int samplesToRead = 0;
            if (currentPosition >= lastReadPosition)
            {
                samplesToRead = currentPosition - lastReadPosition;
            }
            else
            {
                // 缓冲区循环了
                samplesToRead = (recordingClip.samples - lastReadPosition) + currentPosition;
            }

            if (samplesToRead <= 0) continue;

            // 读取新的音频数据
            float[] samples = new float[samplesToRead * recordingClip.channels];
            int readStart = lastReadPosition;

            if (currentPosition >= lastReadPosition)
            {
                // 正常情况，直接读取
                recordingClip.GetData(samples, readStart);
            }
            else
            {
                // 需要分两次读取（跨越缓冲区边界）
                int firstPartSamples = (recordingClip.samples - lastReadPosition) * recordingClip.channels;
                float[] firstPart = new float[firstPartSamples];
                recordingClip.GetData(firstPart, lastReadPosition);

                int secondPartSamples = currentPosition * recordingClip.channels;
                float[] secondPart = new float[secondPartSamples];
                recordingClip.GetData(secondPart, 0);

                Array.Copy(firstPart, 0, samples, 0, firstPartSamples);
                Array.Copy(secondPart, 0, samples, firstPartSamples, secondPartSamples);
            }

            // 更新读取位置
            lastReadPosition = currentPosition;

            // 计算音频时长
            float audioDuration = (float)samplesToRead / sampleRate;

            // 进行VAD检测
            bool hasVoiceActivity = CheckVADActivity(samples, recordingClip.channels);

            if (hasVoiceActivity)
            {
                Debug.Log($"检测到语音活动 (时长: {audioDuration:F2}秒)");
                lastActiveTime = Time.time;

                if (!isInSpeech)
                {
                    // 首次进入语音段时把预录音频拼进去
                    currentSpeechBuffer.AddRange(preRollBuffer);
                    isInSpeech = true;
                }

                currentSpeechBuffer.AddRange(samples);
            }
            else if (isInSpeech)
            {
                // 已在语音段内，即便当前帧静音也保留，避免中间缺失
                currentSpeechBuffer.AddRange(samples);
            }

            // 推进预录缓冲，避免开头被截断
            foreach (var s in samples)
            {
                if (preRollBuffer.Count >= preRollSamples)
                    preRollBuffer.Dequeue();

                preRollBuffer.Enqueue(s);
            }

            // 尾音保留，静音一段时间后再收尾
            if (isInSpeech && Time.time - lastActiveTime > postRollSeconds)
            {
                FinalizeSegment();
            }

            // 检查是否需要保存和处理（静音超过阈值）
            if (Time.time - lastActiveTime > noSpeechThreshold)
            {
                if (audioSegments.Count > 0)
                {
                    // 如果模型或TTS正在输出，立即打断
                    if (isTTSPlaying || chatInProgress)
                    {
                        CancelCurrentChat();
                        StopCurrentTTS();
                        chatCancelledByVoice = true;
                        Debug.Log("检测到新语音，已打断当前模型输出/TTS");
                    }
                    StartCoroutine(ProcessAudioSegments());
                }
            }
        }

        // 清理
        if (isRecording && recordingClip != null)
        {
            Microphone.End(microphoneDevice);
            isRecording = false;
        }
    }

    void FinalizeSegment()
    {
        if (currentSpeechBuffer.Count == 0)
        {
            currentSpeechBuffer.Clear();
            isInSpeech = false;
            return;
        }

        // 获取当前录音的clip
        AudioClip recordingClip = null;
        if (isRecording && Microphone.IsRecording(microphoneDevice))
        {
            // 注意：这里需要从外部获取recordingClip，为了简化，我们重新创建
            int sampleCount = currentSpeechBuffer.Count / 1; // 假设单声道
            if (sampleCount <= 0)
            {
                currentSpeechBuffer.Clear();
                isInSpeech = false;
                return;
            }

            AudioClip clip = AudioClip.Create(
                $"Speech_{audioFileCount++}",
                sampleCount,
                1, // 单声道
                sampleRate,
                false
            );

            clip.SetData(currentSpeechBuffer.ToArray(), 0);
            audioSegments.Add(clip);

            currentSpeechBuffer.Clear();
            isInSpeech = false;
        }
    }

    /// <summary>
    /// 简单的VAD检测（基于能量检测）
    /// </summary>
    bool CheckVADActivity(float[] samples, int channels)
    {
        if (samples == null || samples.Length == 0) return false;

        const int frameSize = (int)(sampleRate * 0.02f); // 20ms帧
        int activeFrames = 0;
        int totalFrames = 0;

        for (int i = 0; i < samples.Length - frameSize * channels; i += frameSize * channels)
        {
            // 计算帧的能量
            float energy = 0f;
            for (int j = 0; j < frameSize * channels; j++)
            {
                energy += Mathf.Abs(samples[i + j]);
            }
            energy /= (frameSize * channels);

            totalFrames++;
            if (energy > vadEnergyThreshold)
            {
                activeFrames++;
            }
        }

        if (totalFrames == 0) return false;

        float activityRate = (float)activeFrames / totalFrames;
        return activityRate >= vadActivityRate;
    }

    /// <summary>
    /// 处理音频段：合并、转文字、生成回复、播放TTS
    /// </summary>
    IEnumerator ProcessAudioSegments()
    {
        if (audioSegments.Count == 0 || isProcessingAudio) yield break;

        isProcessingAudio = true;

        // 停止当前TTS播放（如果有）
        AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : null;
        if (audioSourceToUse != null && audioSourceToUse.isPlaying)
        {
            audioSourceToUse.Stop();
            Debug.Log("检测到新的有效语音，已停止当前TTS播放");
        }

        // 合并所有音频段
        int segmentCount = audioSegments.Count;
        AudioClip mergedClip = MergeAudioClips(audioSegments);
        if (mergedClip == null)
        {
            isProcessingAudio = false;
            yield break;
        }

        Debug.Log($"处理音频段，共 {segmentCount} 段，总时长: {mergedClip.length:F2}秒");

        // 清空音频段列表
        audioSegments.Clear();

        // 转文字
        string transcribedText = "";
        bool transcribeCompleted = false;

        Task<string> transcribeTask = TranscribeAudioWithResult(mergedClip);
        StartCoroutine(WaitForTranscribeTask(transcribeTask, (text) => {
            transcribedText = text;
            transcribeCompleted = true;
        }));

        while (!transcribeCompleted)
        {
            yield return null;
        }

        if (string.IsNullOrEmpty(transcribedText))
        {
            Debug.LogWarning("转文字结果为空，跳过处理");
            isProcessingAudio = false;
            yield break;
        }

        Debug.Log($"转文字结果: {transcribedText}");

        // 显示用户消息
        AddMessage(transcribedText, true);

        // 生成AI回复 - 重新触发聊天流程
        yield return StartCoroutine(ChatWithRAGFlow(transcribedText));

        isProcessingAudio = false;
        lastActiveTime = Time.time;
    }

    /// <summary>
    /// 合并多个AudioClip
    /// </summary>
    AudioClip MergeAudioClips(List<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return null;

        int totalSamples = 0;
        int channels = clips[0].channels;
        int frequency = clips[0].frequency;

        foreach (var clip in clips)
        {
            totalSamples += clip.samples;
        }

        float[] mergedSamples = new float[totalSamples * channels];
        int offset = 0;

        foreach (var clip in clips)
        {
            float[] clipSamples = new float[clip.samples * clip.channels];
            clip.GetData(clipSamples, 0);
            Array.Copy(clipSamples, 0, mergedSamples, offset, clipSamples.Length);
            offset += clipSamples.Length;
        }

        AudioClip mergedClip = AudioClip.Create("MergedAudio", totalSamples, channels, frequency, false);
        mergedClip.SetData(mergedSamples, 0);

        return mergedClip;
    }

    /// <summary>
    /// 等待转文字任务完成
    /// </summary>
    IEnumerator WaitForTranscribeTask(Task<string> task, System.Action<string> onComplete)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsCompletedSuccessfully)
        {
            onComplete?.Invoke(task.Result);
        }
        else
        {
            Debug.LogError($"转文字任务失败: {task.Exception}");
            onComplete?.Invoke("");
        }
    }

    /// <summary>
    /// 使用讯飞STT转文字
    /// </summary>
    async Task<string> TranscribeAudioWithResult(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            return "";
        }

        if (xunFeiSpeechService == null)
        {
            Debug.LogError("讯飞语音服务未初始化，无法转文字");
            return "";
        }

        try
        {
            byte[] pcmData = XunFeiSpeechService.AudioClipToPcm16(audioClip);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                string transcribedText = await xunFeiSpeechService.RequestSttAsync(pcmData, cts.Token);
                return transcribedText;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("讯飞转文字任务被取消");
            return "";
        }
        catch (Exception e)
        {
            Debug.LogError($"讯飞转文字失败: {e.Message}");
            return "";
        }
    }
}