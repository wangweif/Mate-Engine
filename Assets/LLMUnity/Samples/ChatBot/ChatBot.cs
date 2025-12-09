using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System;
using System.Text;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.EventSystems;

namespace LLMUnitySamples
{
    public class ChatBot : MonoBehaviour
    {
        [Header("Containers")]
        public Transform chatContainer;         
        public Transform inputContainer;        

        [Header("Colors & Font")]
        public Color playerColor = new Color32(81, 164, 81, 255);
        public Color aiColor = new Color32(29, 29, 73, 255);
        public Color fontColor = Color.white;
        public Font font;
        public int fontSize = 16;

        [Header("Bubble Layout")]
        public int bubbleWidth = 400;
        public float textPadding = 10f;
        public float bubbleSpacing = 10f;
        public float bottomPadding = 10f;       
        public Sprite sprite;
        public Sprite roundedSprite16;
        public Sprite roundedSprite32;
        public Sprite roundedSprite64;

        // 新增：左右边距控制
        [Header("Bubble Margins")]
        public float leftMargin = 20f;    // AI气泡左边距
        public float rightMargin = 20f;   // 玩家气泡右边距
        public float sideIndent = 80f;   // 侧边缩进量，让气泡不要贴边

        [Header("LLM")]
        public LLMCharacter llmCharacter;

        [Header("Remote Model (RAGFlow)")]
        public bool useRemoteModel = true;
        public string ragflowHost = "192.168.8.88";
        public int ragflowPort = 9380;
        public string ragflowApiKey = "ragflow-cwZWU5YjBjMzUxODExZjBhNThhMDk2OD";
        public string ragflowAssistantId = "37fd87c8d3d711f097ac578fc36c86e8";
        public string ragflowFileUrlPrefix = "http://preview.bjzntd.com/onlinePreview?url=";
        public string ragflowFileUrl = "https://know.baafs.net.cn";
        public string ragflowLanguage = "Chinese";

        [Header("Input Settings")]
        public string inputPlaceholder = "Message me";

        [Header("Streaming Audio")]
        public AudioSource streamAudioSource;

        [Header("Bubble Materials")]
        public Material playerMaterial;         
        public Material aiMaterial;             
        [Header("Text Materials")]
        public Material playerTextMaterial;      
        public Material aiTextMaterial;        
        [Header("Scroll")]
        public ScrollRect scrollRect;           
        public bool autoScrollOnNewMessage = true;     
        public bool respectUserScroll = true;            

        [Header("History")]
        [Min(0)] public int maxMessages = 100;           
        public bool trimOnlyWhenAtBottom = true;       
        public bool enableOffscreenTrim = false;        

        [Header("Font Colors (per side)")]
        public Color playerFontColor = Color.white;
        public Color aiFontColor = Color.white;

        [Header("Rounded Sprite Radius")]
        [Range(0, 64)]
        public int cornerRadius = 16;
        private bool layoutDirty;

        [Header("气泡显示控制")]
        [Tooltip("默认显示的最新消息数量")]
        public int defaultVisibleMessages = 2;

        // 显示控制变量
        private int visibleMessageCount = 0;
        private bool hasHiddenMessages = false;

        private InputBubble inputBubble;
        private List<Bubble> chatBubbles = new List<Bubble>();
        private bool blockInput = true;
        private BubbleUI playerUI, aiUI;
        private bool warmUpDone = false;
        private int lastBubbleOutsideFOV = -1;

        private Animator avatarAnimator;
        private Animator lastAvatarAnimator;
        private static readonly int isTalkingHash = Animator.StringToHash("isTalking");

        public SmartWindowsTTS smartWindowsTTS; // 保留以兼容旧代码，但不再使用
        
        [Header("TTS Audio Source")]
        public AudioSource ttsAudioSource; // 用于播放TTS音频，如果为空则使用streamAudioSource

        // 语音录制相关
        private AudioClip recordingClip;
        private string microphoneDevice;
        private bool isRecording = false;
        private const int sampleRate = 16000;
        private const int maxRecordingLength = 60; // 最大录制60秒
        private const string whisperApiUrl = "http://192.168.8.88:8001/v1/audio/transcriptions";
        private const string whisperModel = "whisper-small";
        
        // TTS API配置
        private const string ttsApiBaseUrl = "http://192.168.8.88:5000/tts";
        private const string ttsApiKey = "bjzntd@123456";
        private const string ttsVoice = "zh-CN-XiaoshuangNeural";
        // 按住说话拖动取消相关
        private bool isCancellingRecording = false;
        private Vector2 holdButtonDownPosition;
        private const float cancelDragThreshold = 80f; // 上滑超过该像素视为取消

        // 实时语音对话相关
        [Header("实时语音对话设置")]
        public bool enableRealTimeVoiceChat = true;
        public float vadCheckInterval = 0.1f; // VAD检测间隔（秒）
        public float noSpeechThreshold = 1.0f; // 无效语音阈值（秒）
        public float vadEnergyThreshold = 0.01f; // VAD能量阈值
        public float vadActivityRate = 0.6f; // VAD活动率阈值（60%的块检测到语音才认为有活动）
        public float preRollSeconds = 0.4f; // 预录时长
        public float postRollSeconds = 0.5f; // 尾音保留时长
        
        private bool isRealTimeVoiceChatActive = false;
        private Coroutine realTimeVoiceChatCoroutine;
        private List<AudioClip> audioSegments = new List<AudioClip>();
        private float lastActiveTime = 0f;
        private int audioFileCount = 0;
        private bool isProcessingAudio = false; // 防止并发处理
        private int lastReadPosition = 0; // 上次读取的音频位置
        private Coroutine currentTTSPlayCoroutine = null; // 当前TTS播放协程
        private bool isTTSPlaying = false; // TTS是否正在播放
        private bool chatInProgress = false; // 模型是否正在输出
        private bool chatCancelledByVoice = false; // 是否因新语音打断了模型输出
        private AudioClip currentTTSClip = null; // 当前TTS音频
        private string currentSessionId = ""; // RAGFlow会话ID
        private int preRollSamples;
        private Queue<float> preRollBuffer;
        private bool isInSpeech = false;
        private List<float> currentSpeechBuffer = new List<float>();

        // 离线模式相关
        [Header("离线模式")]
        public NetworkConnectivityChecker networkChecker;
        private bool isOfflineMode = false;

        void Start()
        {
            avatarAnimator = GetComponent<Animator>();

            // 初始化网络检测器
            InitializeNetworkChecker();

            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cornerRadius <= 16) sprite = roundedSprite16;
            else if (cornerRadius <= 32) sprite = roundedSprite32;
            else sprite = roundedSprite64;

            playerUI = new BubbleUI
            {
                sprite = sprite,
                font = font,
                fontSize = fontSize,
                fontColor = playerFontColor,
                bubbleColor = playerColor,
                bottomPosition = 0,
                leftPosition = 1,
                textPadding = textPadding,
                bubbleOffset = bubbleSpacing,
                bubbleWidth = bubbleWidth,
                bubbleHeight = -1
            };

            aiUI = new BubbleUI
            {
                sprite = sprite,
                font = font,
                fontSize = fontSize,
                fontColor = aiFontColor,
                bubbleColor = aiColor,
                bottomPosition = 0,
                leftPosition = 0,
                textPadding = textPadding,
                bubbleOffset = bubbleSpacing,
                bubbleWidth = bubbleWidth,
                bubbleHeight = -1
            };

            Transform inputParent = inputContainer != null ? inputContainer : chatContainer;

            inputBubble = new InputBubble(inputParent, playerUI, "InputBubble", "加载中...", 4);
            inputBubble.AddSubmitListener(onInputFieldSubmit);
            inputBubble.AddValueChangedListener(onValueChanged);
            inputBubble.setInteractable(false);

            // 设置语音输入按钮事件
            SetupVoiceInputButtons();

            // 根据网络状态更新UI
            UpdateOfflineModeUI();

            ShowLoadedMessages();
            _ = llmCharacter.Warmup(WarmUpCallback);
            FindAvatarSmart();
        }

        void SetupVoiceInputButtons()
        {
            // 语音输入按钮点击事件 - 启动实时语音对话
            Button voiceButton = inputBubble.GetVoiceInputButton();
            if (voiceButton != null)
            {
                voiceButton.onClick.AddListener(() => {
                    // 离线模式下禁用语音功能
                    if (isOfflineMode)
                    {
                        Debug.LogWarning("离线模式：语音功能已禁用");
                        return;
                    }

                    Debug.Log("语音输入按钮被点击 - 启动实时语音对话");
                    if (enableRealTimeVoiceChat)
                    {
                        StartRealTimeVoiceChat();
                    }
                    else
                    {
                        inputBubble.SetVoiceMode(true);
                    }
                });
            }
            else
            {
                Debug.LogError("语音输入按钮未找到！");
            }

            // 键盘按钮点击事件 - 停止实时语音对话
            Button keyboardButton = inputBubble.GetKeyboardButton();
            if (keyboardButton != null)
            {
                keyboardButton.onClick.AddListener(() => {
                    if (isRealTimeVoiceChatActive)
                    {
                        StopRealTimeVoiceChat();
                    }
                    inputBubble.SetVoiceMode(false);
                });
            }

            // 按住说话按钮事件
            Button holdButton = inputBubble.GetHoldToSpeakButton();
            if (holdButton != null)
            {
                // 使用EventTrigger来处理按下 / 拖动 / 松开事件
                UnityEngine.EventSystems.EventTrigger trigger = holdButton.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (trigger == null)
                {
                    trigger = holdButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                }

                trigger.triggers ??= new System.Collections.Generic.List<UnityEngine.EventSystems.EventTrigger.Entry>();
                trigger.triggers.Clear();

                // 按下事件 - 开始录制
                UnityEngine.EventSystems.EventTrigger.Entry pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
                pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
                pointerDown.callback.AddListener((data) => { OnHoldToSpeakDown((UnityEngine.EventSystems.PointerEventData)data); });
                trigger.triggers.Add(pointerDown);

                // 拖动事件 - 检测上滑取消
                UnityEngine.EventSystems.EventTrigger.Entry drag = new UnityEngine.EventSystems.EventTrigger.Entry();
                drag.eventID = UnityEngine.EventSystems.EventTriggerType.Drag;
                drag.callback.AddListener((data) => { OnHoldToSpeakDrag((UnityEngine.EventSystems.PointerEventData)data); });
                trigger.triggers.Add(drag);

                // 松开事件 - 根据是否取消决定是否转文字
                UnityEngine.EventSystems.EventTrigger.Entry pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
                pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
                pointerUp.callback.AddListener((data) => { OnHoldToSpeakUp((UnityEngine.EventSystems.PointerEventData)data); });
                trigger.triggers.Add(pointerUp);
            }
        }

        /// <summary>
        /// 初始化网络检测器
        /// </summary>
        void InitializeNetworkChecker()
        {
            if (networkChecker == null)
            {
                // 尝试在场景中查找NetworkConnectivityChecker
                networkChecker = FindFirstObjectByType<NetworkConnectivityChecker>();
                
                // 如果没有找到，创建一个新的
                if (networkChecker == null)
                {
                    GameObject checkerObj = new GameObject("NetworkConnectivityChecker");
                    networkChecker = checkerObj.AddComponent<NetworkConnectivityChecker>();
                    networkChecker.serverHost = ragflowHost;
                    Debug.Log("[ChatBot] 已自动创建NetworkConnectivityChecker");
                }
            }

            // 订阅连接状态变化事件
            networkChecker.OnConnectivityChanged += OnNetworkConnectivityChanged;
            
            // 设置初始离线模式状态
            isOfflineMode = !networkChecker.IsOnline();
            Debug.Log($"[ChatBot] 初始模式: {(isOfflineMode ? "离线" : "在线")}");
        }

        /// <summary>
        /// 网络连接状态变化回调
        /// </summary>
        void OnNetworkConnectivityChanged(bool isOnline)
        {
            isOfflineMode = !isOnline;
            Debug.Log($"[ChatBot] 网络状态变化 - 切换到{(isOfflineMode ? "离线" : "在线")}模式");
            
            // 更新UI
            UpdateOfflineModeUI();
            
            // 如果切换到离线模式，停止所有网络相关功能
            if (isOfflineMode)
            {
                StopRealTimeVoiceChat();
                StopCurrentTTS();
            }
        }

        /// <summary>
        /// 更新离线模式UI
        /// </summary>
        void UpdateOfflineModeUI()
        {
            if (inputBubble == null) return;

            // 在离线模式下隐藏麦克风按钮
            Button voiceButton = inputBubble.GetVoiceInputButton();
            if (voiceButton != null)
            {
                voiceButton.gameObject.SetActive(!isOfflineMode);
            }

            // 如果当前在语音模式且切换到离线，强制切换回文本模式
            if (isOfflineMode && inputBubble.IsVoiceMode())
            {
                inputBubble.SetVoiceMode(false);
            }
        }

        void FindAvatarSmart()
        {
            Animator found = null;
            var loader = FindFirstObjectByType<VRMLoader>();
            if (loader != null)
            {
                var current = loader.GetCurrentModel();
                if (current != null) found = current.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a && a.gameObject.activeInHierarchy);
            }
            if (found == null)
            {
                var modelParent = GameObject.Find("Model");
                if (modelParent != null) found = modelParent.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a && a.gameObject.activeInHierarchy);
            }
            if (found == null)
            {
                var all = GameObject.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                found = all.FirstOrDefault(a => a && a.isActiveAndEnabled);
            }
            if (found != avatarAnimator)
            {
                avatarAnimator = found;
                lastAvatarAnimator = avatarAnimator;
            }
        }

        void RefreshAvatarIfChanged()
        {
            if (avatarAnimator == null || lastAvatarAnimator == null || avatarAnimator != lastAvatarAnimator)
            {
                FindAvatarSmart();
            }
        }


        private void MarkLayoutDirty()
        {
            layoutDirty = true;
        }

        void OnDisable()
        {
            // 取消订阅网络状态变化事件
            if (networkChecker != null)
            {
                networkChecker.OnConnectivityChanged -= OnNetworkConnectivityChanged;
            }

            StopRealTimeVoiceChat();
            StopCurrentTTS();
            if (streamAudioSource != null && streamAudioSource.isPlaying)
            {
                streamAudioSource.Stop();
                streamAudioSource.volume = 1f; 
            }
            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);
        }

        void OnDestroy()
        {
            StopRealTimeVoiceChat();
            StopCurrentTTS();
        }

        Bubble AddBubble(string message, bool isPlayerMessage)
        {
            Bubble bubble = new Bubble(chatContainer, isPlayerMessage ? playerUI : aiUI, isPlayerMessage ? "PlayerBubble" : "AIBubble", message);
            chatBubbles.Add(bubble);
            bubble.OnResize(MarkLayoutDirty);

            var image = bubble.GetRectTransform().GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.material = isPlayerMessage ? playerMaterial : aiMaterial;
            }
            var text = bubble.GetRectTransform().GetComponentInChildren<Text>(true);
            if (text != null)
            {
                Material m = isPlayerMessage ? playerTextMaterial : aiTextMaterial;
                if (m != null)
                {
                    text.material = m;
                }
            }

            // 新添加的消息总是显示的
            bubble.Show();

            if (autoScrollOnNewMessage && (!respectUserScroll || IsAtBottom()))
            {
                StartCoroutine(ScrollToBottomNextFrame());
            }

            TrimHistoryIfNeeded();

            return bubble;
        }

        void TrimHistoryIfNeeded()
        {
            // 禁用修剪功能，避免删除隐藏的气泡
            // 因为我们需要保留隐藏的气泡以支持滚动显示功能
            return;
        }

        bool IsAtBottom(float tolerance = 0.01f)
        {
            if (scrollRect == null) return true; 
            return scrollRect.verticalNormalizedPosition <= tolerance;
        }

        void ShowLoadedMessages()
        {
            int start = 1;
            int total = llmCharacter.chat.Count;
            if (maxMessages > 0)
                start = Mathf.Max(1, total - maxMessages);

            // 计算应该显示的消息数量
            visibleMessageCount = Math.Min(defaultVisibleMessages, total - start);

            for (int i = start; i < total; i++)
            {
                bool isPlayerMessage = i % 2 == 1;
                Bubble bubble = AddBubble(llmCharacter.chat[i].content, isPlayerMessage);

                // 控制气泡显示/隐藏
                int messageIndex = i - start;
                bool shouldHide = messageIndex < (total - start - visibleMessageCount);

                if (shouldHide)
                {
                    bubble.Hide();
                    hasHiddenMessages = true;
                }
                else
                {
                    bubble.Show();
                }
            }

            StartCoroutine(ScrollToBottomNextFrame());
        }

        void onInputFieldSubmit(string newText)
        {
            inputBubble.ActivateInputField();
            if (blockInput || newText.Trim() == "" || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                StartCoroutine(BlockInteraction());
                return;
            }
            blockInput = true;

            string message = inputBubble.GetText().Replace("\v", "\n");

            // 隐藏所有当前显示的消息，只保留新的对话
            HideCurrentVisibleMessages();

            AddBubble(message, true);
            Bubble aiBubble = AddBubble("...", false);

            // 暂停当前的播放
            StopCurrentTTS();

            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, true);

            chatCancelledByVoice = false;
            chatInProgress = true;

            // 根据配置选择使用本地模型还是远程模型
            if (useRemoteModel && !isOfflineMode)
            {
                // 使用RAGFlow远程模型
                StartCoroutine(ChatWithRAGFlow(
                    message,
                    (partial) => { aiBubble.SetText(partial); layoutDirty = true; },
                    () =>
                    {
                        if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);

                        aiBubble.SetText(aiBubble.GetText());
                        layoutDirty = true;
                        chatInProgress = false;


                        //调用tts
                        Debug.Log("开始调用tts");
                        StartCoroutine(PlayTTSFromAPI(aiBubble.GetText(), (success) => {
                            if (!success)
                            {
                                Debug.LogWarning($"⚠ 语音播放失败: {aiBubble.GetText()}");
                            }
                        }));
                        Debug.Log("完成调用tts");

                        AllowInput();
                    }
                ));
            }
            else
            {
                // 使用本地LLM模型
                Task chatTask = llmCharacter.Chat(
                    message,
                    (partial) => { aiBubble.SetText(partial); layoutDirty = true; },
                    () =>
                    {
                        if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);

                        aiBubble.SetText(aiBubble.GetText());
                        layoutDirty = true;
                        chatInProgress = false;


                        // 调用TTS（离线模式下跳过）
                        if (!isOfflineMode)
                        {
                            Debug.Log("开始调用tts");
                            StartCoroutine(PlayTTSFromAPI(aiBubble.GetText(), (success) => {
                                if (!success)
                                {
                                    Debug.LogWarning($"⚠ 语音播放失败: {aiBubble.GetText()}");
                                }
                            }));
                            Debug.Log("完成调用tts");
                        }
                        else
                        {
                            Debug.Log("离线模式：跳过TTS播放");
                        }

                        AllowInput();
                    }
                );
            }
            inputBubble.SetText("");
        }

        public void WarmUpCallback()
        {
            warmUpDone = true;
            inputBubble.SetPlaceHolderText(inputPlaceholder);
            AllowInput();
        }

        public void AllowInput()
        {
            blockInput = false;
            inputBubble.ReActivateInputField();
        }

        public void CancelRequests()
        {
            llmCharacter.CancelRequests();
            AllowInput();
        }

        IEnumerator<string> BlockInteraction()
        {
            inputBubble.setInteractable(false);
            yield return null;
            inputBubble.setInteractable(true);
            inputBubble.MoveTextEnd();
        }

        void onValueChanged(string newText)
        {
            if (Input.GetKey(KeyCode.Return))
            {
                if (inputBubble.GetText().Trim() == "")
                    inputBubble.SetText("");
            }
        }

        /*public void UpdateBubblePositions()
        {
            float y = bottomPadding;
            for (int i = chatBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform childRect = bubble.GetRectTransform();

                childRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);

                childRect.anchoredPosition = new Vector2(childRect.anchoredPosition.x, y);

                if (enableOffscreenTrim)
                {
                    float containerHeight = chatContainer.GetComponent<RectTransform>().rect.height;
                    if (y > containerHeight && lastBubbleOutsideFOV == -1)
                    {
                        lastBubbleOutsideFOV = i;
                    }
                }

                y += bubble.GetSize().y + bubbleSpacing;
            }
            var contentRect = chatContainer.GetComponent<RectTransform>();
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + bottomPadding);
        }*/
        public void UpdateBubblePositions()
        {
            float y = bottomPadding;
            float containerWidth = chatContainer.GetComponent<RectTransform>().rect.width;

            for (int i = chatBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform childRect = bubble.GetRectTransform();

                // 设置气泡宽度
                childRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);

                // 获取气泡类型
                bool isPlayerBubble = bubble.bubbleUI.leftPosition == 1;

                // 微信风格错开布局
                float xPosition;
                if (isPlayerBubble)
                {
                    // 玩家气泡：右侧，从容器右边界向左偏移
                    xPosition = containerWidth - bubbleWidth - rightMargin - sideIndent;
                }
                else
                {
                    // AI气泡：左侧，从容器左边界向右偏移
                    xPosition = leftMargin + sideIndent;
                }

                childRect.anchoredPosition = new Vector2(xPosition, y);

                if (enableOffscreenTrim)
                {
                    float containerHeight = chatContainer.GetComponent<RectTransform>().rect.height;
                    if (y > containerHeight && lastBubbleOutsideFOV == -1)
                    {
                        lastBubbleOutsideFOV = i;
                    }
                }

                y += bubble.GetSize().y + bubbleSpacing;
            }
            var contentRect = chatContainer.GetComponent<RectTransform>();
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + bottomPadding);
            DebugBubblePositions();
        }
        public void DebugBubblePositions()
        {
            float containerWidth = chatContainer.GetComponent<RectTransform>().rect.width;
            // Debug.Log($"容器宽度: {containerWidth}");

            for (int i = 0; i < chatBubbles.Count; i++)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform rect = bubble.GetRectTransform();
                bool isPlayer = bubble.bubbleUI.leftPosition == 1;
                // Debug.Log($"{(isPlayer ? "玩家" : "AI")}气泡 - 位置: {rect.anchoredPosition}, 锚点: {rect.anchorMin}");
            }
        }
        public void UpdateAllBulesWidth()
        {
            foreach(Bubble bubble in chatBubbles)
            {
                 RectTransform bubbleRect = bubble.GetRectTransform();
                if (bubbleRect != null)
                {
                    bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
                }
            }
            MarkLayoutDirty();
        }

        void Update()
        {
            RefreshAvatarIfChanged();

            if (inputBubble != null && !inputBubble.IsVoiceMode() && !inputBubble.inputFocused() && warmUpDone)
            {
                inputBubble.ActivateInputField();
                StartCoroutine(BlockInteraction());
            }

            // 新增：滚轮控制气泡显示
            HandleWheelScrollVisibility();

            // 禁用offscreen trim功能，避免删除隐藏的气泡
            // 因为我们需要保留隐藏的气泡以支持滚动显示功能
            if (false && enableOffscreenTrim && lastBubbleOutsideFOV != -1)
            {
                for (int i = 0; i <= lastBubbleOutsideFOV; i++)
                {
                    chatBubbles[i].Destroy();
                }
                chatBubbles.RemoveRange(0, lastBubbleOutsideFOV + 1);
                lastBubbleOutsideFOV = -1;
                UpdateBubblePositions();
            }
        }

        public void ExitGame()
        {
            Debug.Log("Exit button clicked");
            Application.Quit();
        }

        IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f; 
        }

        bool onValidateWarning = true;
        void OnValidate()
        {
            if (cornerRadius <= 16) sprite = roundedSprite16;
            else if (cornerRadius <= 32) sprite = roundedSprite32;
            else sprite = roundedSprite64;

            if (onValidateWarning && llmCharacter != null && !llmCharacter.remote && llmCharacter.llm != null && llmCharacter.llm.model == "")
            {
                Debug.LogWarning($"Please select a model in the {llmCharacter.llm.gameObject.name} GameObject!");
                onValidateWarning = false;
            }
        }

        void LateUpdate()
        {
            if (!layoutDirty) return;
            layoutDirty = false;

            UpdateBubblePositions();
            if (autoScrollOnNewMessage && (!respectUserScroll || IsAtBottom()))
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        // 语音录制相关方法
        void OnHoldToSpeakDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (isRecording) return;
            isCancellingRecording = false;
            holdButtonDownPosition = eventData.position;
            StartRecording();
        }

        void OnHoldToSpeakDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!isRecording) return;

            float deltaY = eventData.position.y - holdButtonDownPosition.y;
            bool shouldCancel = deltaY > cancelDragThreshold;

            if (shouldCancel != isCancellingRecording)
            {
                isCancellingRecording = shouldCancel;
                if (inputBubble != null)
                {
                    inputBubble.UpdateHoldButtonCancelState(isCancellingRecording);
                }
            }
        }

        void OnHoldToSpeakUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (!isRecording) return;

            if (isCancellingRecording)
            {
                CancelRecording();
            }
            else
            {
                StopRecordingAndTranscribe();
            }

            isCancellingRecording = false;
            if (inputBubble != null)
            {
                inputBubble.UpdateHoldButtonCancelState(false);
            }
        }

        void StartRecording()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("没有找到麦克风设备");
                return;
            }

            microphoneDevice = Microphone.devices[0];
            isRecording = true;
            recordingClip = Microphone.Start(microphoneDevice, false, maxRecordingLength, sampleRate);
            
            // 更新按钮视觉状态
            if (inputBubble != null)
            {
                inputBubble.UpdateHoldButtonState(true);
            }
            
            Debug.Log("开始录音...");
        }

        void CancelRecording()
        {
            if (!isRecording || recordingClip == null) return;

            Microphone.End(microphoneDevice);
            isRecording = false;

            // 恢复按钮视觉状态
            if (inputBubble != null)
            {
                inputBubble.UpdateHoldButtonState(false);
            }

            Debug.Log("语音输入已取消");
        }

        void StopRecordingAndTranscribe()
        {
            if (!isRecording || recordingClip == null) return;

            int position = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);
            isRecording = false;

            // 更新按钮视觉状态
            if (inputBubble != null)
            {
                inputBubble.UpdateHoldButtonState(false);
            }

            if (position <= 0)
            {
                Debug.LogWarning("录音时间太短，无法转文字");
                return;
            }

            // 裁剪音频片段
            float[] samples = new float[recordingClip.samples * recordingClip.channels];
            recordingClip.GetData(samples, 0);
            
            int clipLength = position * recordingClip.channels;
            float[] trimmedSamples = new float[clipLength];
            Array.Copy(samples, trimmedSamples, clipLength);

            AudioClip trimmedClip = AudioClip.Create("RecordedAudio", clipLength / recordingClip.channels, recordingClip.channels, recordingClip.frequency, false);
            trimmedClip.SetData(trimmedSamples, 0);

            Debug.Log("录音结束，开始转文字...");
            _ = TranscribeAudio(trimmedClip);
        }

        async Task TranscribeAudio(AudioClip audioClip)
        {
            try
            {
                // 将AudioClip转换为WAV格式的字节数组
                byte[] wavData = AudioClipToWav(audioClip);
                
                // 创建表单数据
                List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
                formData.Add(new MultipartFormFileSection("file", wavData, "audio.wav", "audio/wav"));
                formData.Add(new MultipartFormDataSection("model", whisperModel));
                formData.Add(new MultipartFormDataSection("language", "zh"));
                // 发送请求
                using (UnityWebRequest request = UnityWebRequest.Post(whisperApiUrl, formData))
                {
                    request.timeout = 30;
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.Log($"Whisper API响应: {responseText}");
                        
                        // 解析JSON响应
                        string transcribedText = ParseWhisperResponse(responseText);
                        if (!string.IsNullOrEmpty(transcribedText))
                        {
                            Debug.Log($"转文字结果: {transcribedText}");
                            // 将转文字的结果设置为输入框文本并发送
                            inputBubble.SetText(transcribedText);
                            onInputFieldSubmit(transcribedText);
                        }
                        else
                        {
                            Debug.LogWarning("转文字结果为空");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Whisper API请求失败: {request.error}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"转文字过程中发生错误: {e.Message}");
            }
        }

        byte[] AudioClipToWav(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // 转换为16位PCM
            short[] intData = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * 32767);
            }

            // 创建WAV文件头
            int hz = clip.frequency;
            int channels = clip.channels;
            int samplesCount = clip.samples;
            int sampleRate = hz;

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream))
                {
                    // WAV文件头
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + intData.Length * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16);
                    writer.Write((ushort)1);
                    writer.Write((ushort)channels);
                    writer.Write(sampleRate);
                    writer.Write(sampleRate * channels * 2);
                    writer.Write((ushort)(channels * 2));
                    writer.Write((ushort)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(intData.Length * 2);
                    foreach (short sample in intData)
                    {
                        writer.Write(sample);
                    }
                }
                return stream.ToArray();
            }
        }

        string ParseWhisperResponse(string jsonResponse)
        {
            try
            {
                // 简单的JSON解析，查找"text"字段
                int textIndex = jsonResponse.IndexOf("\"text\"");
                if (textIndex == -1) return "";

                int colonIndex = jsonResponse.IndexOf(":", textIndex);
                if (colonIndex == -1) return "";

                int quoteStart = jsonResponse.IndexOf("\"", colonIndex);
                if (quoteStart == -1) return "";

                int quoteEnd = jsonResponse.IndexOf("\"", quoteStart + 1);
                if (quoteEnd == -1) return "";

                return jsonResponse.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"解析Whisper响应失败: {e.Message}");
                return "";
            }
        }

        // ==================== 实时语音对话功能 ====================

        /// <summary>
        /// 启动实时语音对话
        /// </summary>
        void StartRealTimeVoiceChat()
        {
            // 离线模式下禁用
            if (isOfflineMode)
            {
                Debug.LogWarning("离线模式：无法启动实时语音对话");
                return;
            }

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

            // 切换到语音模式
            inputBubble.SetVoiceMode(true);
            
            // 隐藏按住说话按钮，因为现在是自动检测
            Button holdButton = inputBubble.GetHoldToSpeakButton();
            if (holdButton != null)
            {
                holdButton.gameObject.SetActive(false);
            }

            // 更新按钮文本提示
            UpdateVoiceButtonText("实时对话中...");

            Debug.Log("开始实时语音对话");
            realTimeVoiceChatCoroutine = StartCoroutine(RealTimeVoiceChatLoop());
        }

        /// <summary>
        /// 停止实时语音对话
        /// </summary>
        void StopRealTimeVoiceChat()
        {
            if (!isRealTimeVoiceChatActive) return;

            isRealTimeVoiceChatActive = false;

            if (realTimeVoiceChatCoroutine != null)
            {
                StopCoroutine(realTimeVoiceChatCoroutine);
                realTimeVoiceChatCoroutine = null;
            }

            if (isRecording && recordingClip != null)
            {
                Microphone.End(microphoneDevice);
                isRecording = false;
            }

            // 停止TTS播放
            StopCurrentTTS();
            // 停止模型输出
            CancelCurrentChat();

            audioSegments.Clear();
            UpdateVoiceButtonText("点击开始实时对话");
            Debug.Log("实时语音对话已停止");
        }

        /// <summary>
        /// 更新语音按钮文本
        /// </summary>
        void UpdateVoiceButtonText(string text)
        {
            Button holdButton = inputBubble.GetHoldToSpeakButton();
            if (holdButton != null)
            {
                Text holdButtonText = holdButton.GetComponentInChildren<Text>();
                if (holdButtonText != null)
                {
                    holdButtonText.text = text;
                }
            }
        }

        /// <summary>
        /// 实时语音对话主循环
        /// </summary>
        IEnumerator RealTimeVoiceChatLoop()
        {
            microphoneDevice = Microphone.devices[0];
            recordingClip = Microphone.Start(microphoneDevice, true, maxRecordingLength, sampleRate);
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

                // 推进预录缓冲，避免开头被截断（放在末尾，避免重复当前批次）
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
            if (recordingClip == null || currentSpeechBuffer.Count == 0)
            {
                currentSpeechBuffer.Clear();
                isInSpeech = false;
                return;
            }

            int sampleCount = currentSpeechBuffer.Count / recordingClip.channels;
            if (sampleCount <= 0)
            {
                currentSpeechBuffer.Clear();
                isInSpeech = false;
                return;
            }

            AudioClip clip = AudioClip.Create(
                $"Speech_{audioFileCount++}",
                sampleCount,
                recordingClip.channels,
                sampleRate,
                false
            );

            clip.SetData(currentSpeechBuffer.ToArray(), 0);
            audioSegments.Add(clip);

            currentSpeechBuffer.Clear();
            isInSpeech = false;
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
            AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : streamAudioSource;
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
            AddBubble(transcribedText, true);

            // 生成AI回复
            Bubble aiBubble = AddBubble("...", false);
            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, true);

            bool chatCompleted = false;
            string aiResponse = "";
            chatCancelledByVoice = false;
            chatInProgress = true;

            // 根据配置选择使用本地模型还是远程模型
            if (useRemoteModel && !isOfflineMode)
            {
                // 使用RAGFlow远程模型
                StartCoroutine(ChatWithRAGFlow(
                    transcribedText,
                    (partial) => { 
                        aiBubble.SetText(partial); 
                        layoutDirty = true; 
                    },
                    () =>
                    {
                        if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);
                        aiBubble.SetText(aiBubble.GetText());
                        aiResponse = aiBubble.GetText();
                        layoutDirty = true;
                        chatCompleted = true;

                    }
                ));
            }
            else
            {
                // 使用本地LLM模型
                Task chatTask = llmCharacter.Chat(
                    transcribedText,
                    (partial) => { 
                        aiBubble.SetText(partial); 
                        layoutDirty = true; 
                    },
                    () =>
                    {
                        if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);
                        aiBubble.SetText(aiBubble.GetText());
                        aiResponse = aiBubble.GetText();
                        layoutDirty = true;
                        chatCompleted = true;

                    }
                );
            }

            // 等待聊天完成
            while (!chatCompleted && !chatCancelledByVoice)
            {
                yield return null;
            }

            chatInProgress = false;

            if (chatCancelledByVoice)
            {
                Debug.Log("聊天已被新语音打断，停止当前处理");
                isProcessingAudio = false;
                chatInProgress = false;
                yield break;
            }

            // 播放TTS（非阻塞方式，允许在播放期间继续监听）（离线模式下跳过）
            if (!string.IsNullOrEmpty(aiResponse) && !isOfflineMode)
            {
                Debug.Log("开始进行TTS");
                // 使用非阻塞方式启动TTS播放，不等待完成
                currentTTSPlayCoroutine = StartCoroutine(PlayTTSFromAPI(aiResponse, (success) => {
                    if (!success)
                    {
                        Debug.LogWarning($"⚠ 语音播放失败: {aiResponse}");
                    }
                    isTTSPlaying = false;
                    Debug.Log("TTS播放完成");
                }));
            }
            else if (isOfflineMode)
            {
                Debug.Log("离线模式：跳过TTS播放");
            }

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
        /// 修改后的TranscribeAudio，支持回调返回结果
        /// </summary>
        async Task<string> TranscribeAudioWithResult(AudioClip audioClip)
        {
            try
            {
                byte[] wavData = AudioClipToWav(audioClip);
                //  保存到本地
                // string filePath = Path.Combine(Application.persistentDataPath, "audio.wav");
                // File.WriteAllBytes(filePath, wavData);
                // Debug.Log($"音频已保存到: {filePath}");

                List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
                formData.Add(new MultipartFormFileSection("file", wavData, "audio.wav", "audio/wav"));
                formData.Add(new MultipartFormDataSection("model", whisperModel));
                formData.Add(new MultipartFormDataSection("language", "zh"));
                
                using (UnityWebRequest request = UnityWebRequest.Post(whisperApiUrl, formData))
                {
                    request.timeout = 30;
                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.Log($"Whisper API响应: {responseText}");
                        
                        string transcribedText = ParseWhisperResponse(responseText);
                        return transcribedText;
                    }
                    else
                    {
                        Debug.LogError($"Whisper API请求失败: {request.error}");
                        return "";
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"转文字过程中发生错误: {e.Message}");
                return "";
            }
        }

        // ==================== RAGFlow 远程模型调用 ====================

        /// <summary>
        /// 创建RAGFlow会话
        /// </summary>
        IEnumerator CreateRAGFlowSession()
        {
            string sessionUrl = $"http://{ragflowHost}:{ragflowPort}/api/v1/chats/{ragflowAssistantId}/sessions";
            
            // 使用时间戳作为会话名称
            string sessionName = $"Unity_Session_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            string jsonData = $"{{\"name\":\"{sessionName}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest request = new UnityWebRequest(sessionUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + ragflowApiKey);
                request.timeout = 30;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;                    
                    try
                    {
                        // 解析响应获取session_id
                        int dataIndex = responseText.IndexOf("\"data\"");
                        if (dataIndex != -1)
                        {
                            int idIndex = responseText.IndexOf("\"id\"", dataIndex);
                            if (idIndex != -1)
                            {
                                int idStart = responseText.IndexOf("\"", idIndex + 4) + 1;
                                int idEnd = responseText.IndexOf("\"", idStart);
                                if (idEnd > idStart)
                                {
                                    currentSessionId = responseText.Substring(idStart, idEnd - idStart);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"解析会话ID失败: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"创建RAGFlow会话失败: {request.error}");
                }
            }
        }

        /// <summary>
        /// 使用RAGFlow远程API进行对话
        /// </summary>
        IEnumerator ChatWithRAGFlow(string userMessage, System.Action<string> onPartialResponse, System.Action onComplete)
        {
            // 如果没有session_id，先创建session
            if (string.IsNullOrEmpty(currentSessionId))
            {
                yield return StartCoroutine(CreateRAGFlowSession());
            }
            
            string questionUrl = $"http://{ragflowHost}:{ragflowPort}/api/v1/chats/{ragflowAssistantId}/completions";
            
            // 构建请求数据
            string jsonData = $"{{\"question\":\"{EscapeJsonString(userMessage)}\",\"stream\":true,\"session_id\":\"{currentSessionId}\",\"lang\":\"{ragflowLanguage}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest request = new UnityWebRequest(questionUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + ragflowApiKey);
                request.timeout = 60;
                
                var operation = request.SendWebRequest();
                
                StringBuilder fullResponse = new StringBuilder();
                List<RagFlowReference> references = new List<RagFlowReference>();
                
                string lastProcessedText = "";
                
                while (!operation.isDone)
                {
                    yield return null;
                    
                    // 检查是否被打断
                    if (chatCancelledByVoice || !chatInProgress)
                    {
                        Debug.Log("RAGFlow对话被打断，停止处理");
                        request.Abort();
                        yield break;
                    }
                    
                    // 处理流式响应
                    if (request.downloadHandler != null)
                    {
                        // 强制使用 UTF8 解码，避免服务器返回的 Content-Type 中 charset 异常（如“utf-8,text/event-stream”）导致 Unity 尝试创建无效编码而抛错
                        byte[] rawData = request.downloadHandler.data;
                        string responseText = (rawData != null && rawData.Length > 0)
                            ? Encoding.UTF8.GetString(rawData)
                            : "";
                        
                        // 只处理新增的数据
                        if (responseText != lastProcessedText && !string.IsNullOrEmpty(responseText))
                        {
                            lastProcessedText = responseText;
                            
                            // 解析流式数据 - 按行分割
                            string[] lines = responseText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string line in lines)
                            {
                                // 跳过 "data:true" 结束标记
                                if (line.Contains("\"data\": true") || line.Contains("\"data\":true"))
                                {
                                    continue;
                                }
                                
                                // 处理 "data:{...}" 格式的行
                                if (line.StartsWith("data:"))
                                {
                                    string jsonLine = line.Substring(5).Trim();
                                    if (!string.IsNullOrEmpty(jsonLine))
                                    {
                                        try
                                        {
                                            var parsedData = ParseRAGFlowResponse(jsonLine);
                                            if (!string.IsNullOrEmpty(parsedData.answer) && !parsedData.answer.Contains("* is running..."))
                                            {
                                                // 直接使用完整answer，不累加
                                                fullResponse.Clear();
                                                fullResponse.Append(parsedData.answer);
                                                onPartialResponse?.Invoke(fullResponse.ToString());
                                                
                                                // 收集引用
                                                if (parsedData.references != null && parsedData.references.Count > 0)
                                                {
                                                    references.Clear();
                                                    references.AddRange(parsedData.references);
                                                }
                                                
                                                // 保存session_id
                                                if (!string.IsNullOrEmpty(parsedData.sessionId))
                                                {
                                                    currentSessionId = parsedData.sessionId;
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogWarning($"解析RAGFlow响应失败: {e.Message}\n原始数据: {jsonLine}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    // 显示引用信息
                    if (references.Count > 0)
                    {
                        LogReferences(references);
                    }
                    
                    onComplete?.Invoke();
                }
                else
                {
                    Debug.LogError($"RAGFlow API请求失败: {request.error}");
                    onPartialResponse?.Invoke("抱歉，远程模型调用失败。");
                    onComplete?.Invoke();
                }
            }
        }

        /// <summary>
        /// 解析RAGFlow响应
        /// </summary>
        (string answer, List<RagFlowReference> references, string sessionId) ParseRAGFlowResponse(string jsonResponse)
        {
            string answer = "";
            List<RagFlowReference> references = new List<RagFlowReference>();
            string sessionId = "";
            
            try
            {
                // 简单的JSON解析
                int dataIndex = jsonResponse.IndexOf("\"data\"");
                if (dataIndex == -1) return (answer, references, sessionId);
                
                // 提取answer
                int answerIndex = jsonResponse.IndexOf("\"answer\"", dataIndex);
                if (answerIndex != -1)
                {
                    int answerStart = jsonResponse.IndexOf("\"", answerIndex + 8) + 1;
                    int answerEnd = jsonResponse.IndexOf("\"", answerStart);
                    if (answerEnd > answerStart)
                    {
                        answer = jsonResponse.Substring(answerStart, answerEnd - answerStart);
                        answer = UnescapeJsonString(answer);
                    }
                }
                
                // 提取session_id
                int sessionIdIndex = jsonResponse.IndexOf("\"session_id\"", dataIndex);
                if (sessionIdIndex != -1)
                {
                    int sessionIdStart = jsonResponse.IndexOf("\"", sessionIdIndex + 12) + 1;
                    int sessionIdEnd = jsonResponse.IndexOf("\"", sessionIdStart);
                    if (sessionIdEnd > sessionIdStart)
                    {
                        sessionId = jsonResponse.Substring(sessionIdStart, sessionIdEnd - sessionIdStart);
                    }
                }
                
                // 提取references中的chunks
                int referenceIndex = jsonResponse.IndexOf("\"reference\"", dataIndex);
                if (referenceIndex != -1)
                {
                    int chunksIndex = jsonResponse.IndexOf("\"chunks\"", referenceIndex);
                    if (chunksIndex != -1)
                    {
                        references = ParseRAGFlowChunks(jsonResponse, chunksIndex);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析RAGFlow响应时出错: {e.Message}");
            }
            
            return (answer, references, sessionId);
        }

        /// <summary>
        /// 解析RAGFlow chunks
        /// </summary>
        List<RagFlowReference> ParseRAGFlowChunks(string jsonResponse, int chunksStartIndex)
        {
            List<RagFlowReference> references = new List<RagFlowReference>();
            
            try
            {
                int arrayStart = jsonResponse.IndexOf("[", chunksStartIndex);
                if (arrayStart == -1) return references;
                
                int arrayEnd = jsonResponse.IndexOf("]", arrayStart);
                if (arrayEnd == -1) return references;
                
                string chunksArray = jsonResponse.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                
                // 简单解析每个chunk对象
                string[] chunkObjects = chunksArray.Split(new[] { "},{" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string chunkObj in chunkObjects)
                {
                    string chunk = chunkObj.Trim('{', '}', ' ', ',');
                    
                    RagFlowReference reference = new RagFlowReference();
                    
                    // 提取document_id
                    int docIdIndex = chunk.IndexOf("\"document_id\"");
                    if (docIdIndex != -1)
                    {
                        int docIdStart = chunk.IndexOf("\"", docIdIndex + 13) + 1;
                        int docIdEnd = chunk.IndexOf("\"", docIdStart);
                        if (docIdEnd > docIdStart)
                        {
                            reference.documentId = chunk.Substring(docIdStart, docIdEnd - docIdStart);
                        }
                    }
                    
                    // 提取document_name
                    int docNameIndex = chunk.IndexOf("\"document_name\"");
                    if (docNameIndex != -1)
                    {
                        int docNameStart = chunk.IndexOf("\"", docNameIndex + 15) + 1;
                        int docNameEnd = chunk.IndexOf("\"", docNameStart);
                        if (docNameEnd > docNameStart)
                        {
                            reference.documentName = chunk.Substring(docNameStart, docNameEnd - docNameStart);
                        }
                    }
                    
                    // 提取url
                    int urlIndex = chunk.IndexOf("\"url\"");
                    if (urlIndex != -1)
                    {
                        int urlStart = chunk.IndexOf("\"", urlIndex + 5) + 1;
                        int urlEnd = chunk.IndexOf("\"", urlStart);
                        if (urlEnd > urlStart)
                        {
                            reference.url = chunk.Substring(urlStart, urlEnd - urlStart);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(reference.documentId) || !string.IsNullOrEmpty(reference.url))
                    {
                        references.Add(reference);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"解析chunks时出错: {e.Message}");
            }
            
            return references;
        }

        /// <summary>
        /// 记录引用信息
        /// </summary>
        void LogReferences(List<RagFlowReference> references)
        {
            Debug.Log("=== RAGFlow 引用文档 ===");
            
            Dictionary<string, string> uniqueDocs = new Dictionary<string, string>();
            
            foreach (var reference in references)
            {
                string key = !string.IsNullOrEmpty(reference.url) ? reference.url : reference.documentId;
                
                if (!string.IsNullOrEmpty(key) && !uniqueDocs.ContainsKey(key))
                {
                    uniqueDocs[key] = reference.documentName;
                    
                    string displayUrl = key;
                    if (!string.IsNullOrEmpty(reference.url))
                    {
                        displayUrl = ragflowFileUrlPrefix + ragflowFileUrl + reference.url;
                    }
                    
                    Debug.Log($"文档: {reference.documentName} | URL: {displayUrl}");
                }
            }
            
            Debug.Log("=======================");
        }

        /// <summary>
        /// 转义JSON字符串
        /// </summary>
        string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }

        /// <summary>
        /// 反转义JSON字符串
        /// </summary>
        string UnescapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            return str.Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t")
                      .Replace("\\\"", "\"")
                      .Replace("\\\\", "\\");
        }

        // ==================== HTTP TTS 功能 ====================

        /// <summary>
        /// 停止当前TTS播放
        /// </summary>
        void StopCurrentTTS()
        {
            if (currentTTSPlayCoroutine != null)
            {
                StopCoroutine(currentTTSPlayCoroutine);
                currentTTSPlayCoroutine = null;
            }

            AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : streamAudioSource;
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
        /// 打断当前模型输出（聊天）
        /// </summary>
        void CancelCurrentChat()
        {
            if (!chatInProgress) return;
            chatCancelledByVoice = true;
            chatInProgress = false;
            llmCharacter.CancelRequests();

            if (avatarAnimator != null)
            {
                avatarAnimator.SetBool(isTalkingHash, false);
            }
            if (streamAudioSource != null && streamAudioSource.isPlaying)
            {
                streamAudioSource.Stop();
            }
        }

        /// <summary>
        /// 从HTTP API获取TTS并播放
        /// </summary>
        IEnumerator PlayTTSFromAPI(string text, System.Action<bool> onComplete)
        {
            // 离线模式下直接返回
            if (isOfflineMode)
            {
                Debug.Log("离线模式：TTS功能已禁用");
                onComplete?.Invoke(false);
                yield break;
            }

            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("TTS文本为空");
                isTTSPlaying = false;
                onComplete?.Invoke(false);
                yield break;
            }

            isTTSPlaying = true;

            // 构建TTS API URL
            string encodedText = UnityEngine.Networking.UnityWebRequest.EscapeURL(text);
            string ttsUrl = $"{ttsApiBaseUrl}?text={encodedText}&voice={ttsVoice}&api_key={ttsApiKey}";
            
            Debug.Log($"请求TTS: {ttsUrl}");

            // 创建临时文件路径
            string tempDir = Path.Combine(Application.persistentDataPath, "TTSTemp");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            string tempFilePath = Path.Combine(tempDir, $"tts_{System.Guid.NewGuid()}.mp3");

            AudioClip audioClip = null;
            bool shouldPlay = true;

            // 下载MP3文件
            using (UnityWebRequest request = UnityWebRequest.Get(ttsUrl))
            {
                request.timeout = 30;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // 检查是否被中断
                    if (!isTTSPlaying)
                    {
                        Debug.Log("TTS下载过程中被中断");
                        onComplete?.Invoke(false);
                        yield break;
                    }

                    // 保存到临时文件
                    byte[] audioData = request.downloadHandler.data;
                    File.WriteAllBytes(tempFilePath, audioData);
                    Debug.Log($"TTS MP3文件已保存到: {tempFilePath}, 大小: {audioData.Length} 字节");

                    // 从文件加载AudioClip
                    string fileUrl = "file://" + tempFilePath;
                    using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
                    {
                        yield return audioRequest.SendWebRequest();

                        // 再次检查是否被中断
                        if (!isTTSPlaying)
                        {
                            Debug.Log("TTS加载过程中被中断");
                            try
                            {
                                if (File.Exists(tempFilePath))
                                {
                                    File.Delete(tempFilePath);
                                }
                            }
                            catch { }
                            onComplete?.Invoke(false);
                            yield break;
                        }

                        if (audioRequest.result == UnityWebRequest.Result.Success)
                        {
                            audioClip = DownloadHandlerAudioClip.GetContent(audioRequest);
                            if (audioClip != null)
                            {
                                Debug.Log($"TTS音频加载成功，时长: {audioClip.length:F2}秒");
                            }
                            else
                            {
                                Debug.LogError("TTS音频剪辑为空");
                                shouldPlay = false;
                            }
                        }
                        else
                        {
                            Debug.LogError($"加载TTS音频文件失败: {audioRequest.error}");
                            shouldPlay = false;
                        }
                    }
                }
                else
                {
                    Debug.LogError($"TTS API请求失败: {request.error}, URL: {ttsUrl}");
                    shouldPlay = false;
                }
            }

            // 播放音频
            if (shouldPlay && audioClip != null && isTTSPlaying)
            {
                AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : streamAudioSource;
                if (audioSourceToUse != null)
                {
                    audioSourceToUse.loop = false;
                    // 停止当前播放
                    if (audioSourceToUse.isPlaying)
                    {
                        audioSourceToUse.Stop();
                    }
                    
                    // 记录当前clip，方便打断时销毁
                    currentTTSClip = audioClip;
                    audioSourceToUse.clip = audioClip;
                    audioSourceToUse.Play();
                    
                    // 等待播放完成，但检查是否被中断
                    while (audioSourceToUse.isPlaying && isTTSPlaying && audioSourceToUse.clip == audioClip)
                    {
                        yield return null;
                    }
                    
                    // 如果被中断，停止播放
                    if (!isTTSPlaying || audioSourceToUse.clip != audioClip)
                    {
                        if (audioSourceToUse.isPlaying)
                        {
                            audioSourceToUse.Stop();
                        }
                        Debug.Log("TTS播放被中断");
                    }
                    else
                    {
                        Debug.Log("TTS播放完成");
                    }
                    
                    // 清理临时文件
                    try
                    {
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"清理临时文件失败: {e.Message}");
                    }
                    
                    // 清理临时文件
                    try
                    {
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"清理临时文件失败: {e.Message}");
                    }
                    
                    // 清理AudioClip（若未被中断则销毁当前clip）
                    if (currentTTSClip != null)
                    {
                        Destroy(currentTTSClip);
                        currentTTSClip = null;
                    }
                    
                    isTTSPlaying = false;
                    onComplete?.Invoke(true);
                }
                else
                {
                    Debug.LogError("没有可用的AudioSource播放TTS");
                    if (audioClip != null)
                    {
                        Destroy(audioClip);
                    }
                    isTTSPlaying = false;
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                // 清理临时文件
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch { }

                // 若被中断/失败，清理当前clip
                if (currentTTSClip != null)
                {
                    Destroy(currentTTSClip);
                    currentTTSClip = null;
                }

                isTTSPlaying = false;
                onComplete?.Invoke(false);
            }
        }

        /// <summary>
        /// 处理滚轮控制气泡显示/隐藏
        /// </summary>
        void HandleWheelScrollVisibility()
        {
            if (chatBubbles.Count == 0)
                return;

            // 检查是否有隐藏的消息
            int currentlyVisible = GetVisibleBubbleCount();
            int totalBubbles = chatBubbles.Count;

            

            // 检查滚轮输入
            float scrollWheel = Input.GetAxis("Mouse ScrollWheel");

            if (scrollWheel > 0f) // 向上滚动，显示更多消息
            {
                // 如果当前可见的消息数等于总数，则没有隐藏的消息可以显示
                if (currentlyVisible >= totalBubbles)
                    return;
                ShowMoreMessages();
            }
            else if (scrollWheel < 0f) // 向下滚动，隐藏消息回到默认显示
            {
                //print("滚轮向下");
                HideToDefaultMessages();
            }
        }

        /// <summary>
        /// 显示更多消息
        /// </summary>
        void ShowMoreMessages()
        {
            int currentlyVisible = GetVisibleBubbleCount();
            int hiddenCount = chatBubbles.Count - currentlyVisible;

            if (hiddenCount <= 0)
                return;

            // 每次显示最多2条隐藏的消息
            int toShowCount = Math.Min(2, hiddenCount);
            int shownCount = 0;

            // 从最旧的气泡开始显示（从头开始）
            for (int i = 0; i < chatBubbles.Count && shownCount < toShowCount; i++)
            {
                if (!chatBubbles[i].IsVisible())
                {
                    chatBubbles[i].Show();
                    shownCount++;
                }
            }

            UpdateBubblePositions();

            // 计算新的滚动位置，让用户能看到新显示的内容
            if (scrollRect != null)
            {
                // 稍微向上滚动一点，让用户能看到新显示的内容
                float newPosition = scrollRect.verticalNormalizedPosition - 0.15f;
                scrollRect.verticalNormalizedPosition = Mathf.Max(0.1f, newPosition);
            }
        }

        /// <summary>
        /// 隐藏消息回到默认显示数量
        /// </summary>
        void HideToDefaultMessages()
        {
            int currentlyVisible = GetVisibleBubbleCount();

            // print($"HideToDefaultMessages: 当前可见={currentlyVisible}, 默认显示={defaultVisibleMessages}");

            // 如果当前可见消息数量已经等于或少于默认显示数量，则不需要隐藏
            if (currentlyVisible <= defaultVisibleMessages)
            {
                // print("当前消息数已经等于或少于默认显示数量，无需隐藏");
                return;
            }

            // 从最旧的气泡开始隐藏，只保留最新的 defaultVisibleMessages 条消息
            int targetVisibleCount = defaultVisibleMessages;
            int hideCount = currentlyVisible - targetVisibleCount;
            int hidden = 0;

            // 计算需要保留的起始索引（最后 targetVisibleCount 条消息）
            int keepStartIndex = chatBubbles.Count - targetVisibleCount;

            // print($"计划隐藏 {hideCount} 条消息，保留从索引 {keepStartIndex} 开始的 {targetVisibleCount} 条消息");

            // 隐藏索引小于 keepStartIndex 的所有消息
            for (int i = 0; i < keepStartIndex && hidden < hideCount; i++)
            {
                if (chatBubbles[i].IsVisible())
                {
                    chatBubbles[i].Hide();
                    hidden++;
                    // print($"隐藏消息 {i}: {chatBubbles[i].GetText().Substring(0, Math.Min(15, chatBubbles[i].GetText().Length))}...");
                }
            }

            UpdateBubblePositions();
            StartCoroutine(ScrollToBottomNextFrame());

            // print($"成功隐藏了 {hidden} 条消息，当前显示 {GetVisibleBubbleCount()} 条");
        }

        /// <summary>
        /// 隐藏当前所有可见的消息气泡
        /// </summary>
        void HideCurrentVisibleMessages()
        {
            int hiddenCount = 0;
            for (int i = 0; i < chatBubbles.Count; i++)
            {
                if (chatBubbles[i].IsVisible())
                {
                    chatBubbles[i].Hide();
                    hiddenCount++;
                }
            }

            // 标记有隐藏的消息，但不重置visibleMessageCount
            hasHiddenMessages = true;

            UpdateBubblePositions();
        }

        /// <summary>
        /// 获取当前可见的气泡数量
        /// </summary>
        int GetVisibleBubbleCount()
        {
            int count = 0;
            foreach (var bubble in chatBubbles)
            {
                if (bubble.IsVisible())
                    count++;
            }
            return count;
        }

    }

    /// <summary>
    /// RAGFlow引用文档数据结构
    /// </summary>
    public class RagFlowReference
    {
        public string documentId;
        public string documentName;
        public string url;
    }
}