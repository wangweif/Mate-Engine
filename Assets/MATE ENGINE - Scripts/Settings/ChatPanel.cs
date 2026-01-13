using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 聊天面板UI管理器
    /// 提供聊天界面,支持文本输入和实时语音对话
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        // UI组件 - 自动在代码中创建
        private Canvas parentCanvas;
        private GameObject mainPanel;
        private Button closeButton;
        private ScrollRect scrollRect;
        private RectTransform messageContainer;
        private TMP_InputField inputField;
        private Button voiceInputButton;
        private AudioSource ttsAudioSource;

        // 按钮图标 - 从Resources加载
        private Sprite voiceIcon;
        private Sprite keyboardIcon;

        // 消息预制件 - 可选,如果为null则自动创建
        [Header("Message Prefabs (Optional)")]
        public GameObject userMessagePrefab;
        public GameObject aiMessagePrefab;

        // 气泡设置 - 与AdvancedChatManager场景中的实际配置相同
        private float maxBubbleWidth = 580f;  // 场景中的实际值
        private float minBubbleWidth = 0f;      // 场景中的实际值
        private float bubblePadding = 20f;
        private float userMessageRightMargin = 20f;
        private float aiMessageLeftMargin = 20f;
        private float messageSpacing = 12f;

        // 视觉设置 - 与场景中的实际配置相同
        private Color userBubbleColor = new Color(1f, 1f, 1f, 1f);    // 白色背景
        private Color aiBubbleColor = new Color(0f, 0f, 0f, 0f);      // 透明背景
        private Color userTextColor = new Color(1f, 1f, 1f, 1f);     // 白色文字
        private Color aiTextColor = new Color(1f, 1f, 1f, 1f);       // 白色文字
        private Color textColor = Color.white;

        // 动画设置
        private float typingSpeed = 0.05f;

        // RAGFlow配置 - 与AdvancedChatManager相同
        private string ragflowHost = "192.168.8.88";
        private int ragflowPort = 9380;
        private string ragflowApiKey = "ragflow-cwZWU5YjBjMzUxODExZjBhNThhMDk2OD";
        private string ragflowAssistantId = "37fd87c8d3d711f097ac578fc36c86e8";
        private string ragflowLanguage = "Chinese";

        // 实时语音对话设置 - 与AdvancedChatManager相同
        private bool enableRealTimeVoiceChat = true;
        private float vadCheckInterval = 0.1f;
        private float noSpeechThreshold = 1.0f;
        private float vadEnergyThreshold = 0.01f;
        private float vadActivityRate = 0.6f;
        private float preRollSeconds = 0.4f;
        private float postRollSeconds = 0.5f;

        // 私有字段
        private float containerWidth;
        private Animator avatarAnimator;
        private XunFeiSpeechService xunFeiSpeechService;
        private string currentSessionId = "";
        private bool isRealTimeVoiceChatActive = false;
        private Coroutine realTimeVoiceChatCoroutine;
        private List<AudioClip> audioSegments = new List<AudioClip>();
        private float lastActiveTime = 0f;
        private int audioFileCount = 0;
        private bool isProcessingAudio = false;
        private int lastReadPosition = 0;
        private Coroutine currentTTSPlayCoroutine = null;
        private bool isTTSPlaying = false;
        private bool chatCancelledByVoice = false;
        private AudioClip currentTTSClip = null;
        private int preRollSamples;
        private Queue<float> preRollBuffer;
        private bool isInSpeech = false;
        private List<float> currentSpeechBuffer = new List<float>();
        private CancellationTokenSource ttsCts;
        private static readonly int isTalkingHash = Animator.StringToHash("isTalking");
        private const int sampleRate = 16000;
        private const int maxRecordingLength = 60;
        private string microphoneDevice;
        private bool isRecording = false;
        private const string TtsVoicePrefsKey = "MATE_ENGINE_TTS_VOICE";
        private bool chatInProgress = false;
        private GameObject currentAIBubble;

        [System.Serializable]
        public class MessageData
        {
            public string content;
            public bool isUserMessage;
        }

        void Start()
        {
            // 加载按钮图标
            LoadButtonIcons();

            InitializeComponents();
            SetupUI();

            // 查找或初始化相关组件
            FindAvatarAnimator();
            xunFeiSpeechService = new XunFeiSpeechService();

            // 创建或获取TTS音频源
            SetupTTSAudioSource();

            // 移除messageContainer上的Layout组件
            if (messageContainer != null)
            {
                RemoveLayoutComponents(messageContainer.gameObject);
            }

            // 计算容器宽度
            CalculateContainerWidth();
        }

        /// <summary>
        /// 加载按钮图标
        /// </summary>
        void LoadButtonIcons()
        {
            voiceIcon = Resources.Load<Sprite>("mic");
            keyboardIcon = Resources.Load<Sprite>("keyboard");

            if (voiceIcon == null)
            {
                Debug.LogWarning("[ChatPanel] 未找到Resources/mic.png,将使用默认样式");
            }
            if (keyboardIcon == null)
            {
                Debug.LogWarning("[ChatPanel] 未找到Resources/keyboard.png,将使用默认样式");
            }
        }

        /// <summary>
        /// 设置TTS音频源
        /// 创建专用的AudioSource,避免与场景中的其他脚本冲突
        /// </summary>
        void SetupTTSAudioSource()
        {
            // 如果已经有专用的AudioSource,直接返回
            if (ttsAudioSource != null && ttsAudioSource.gameObject.name.Contains("ChatPanelTTS_AudioSource"))
            {
                return;
            }

            // 创建专用的AudioSource对象(不使用场景中的,避免被其他脚本干扰)
            GameObject audioSourceObj = new GameObject("ChatPanelTTS_AudioSource");
            ttsAudioSource = audioSourceObj.AddComponent<AudioSource>();

            // 配置AudioSource - 独立配置,不受场景中其他AudioSource影响
            ttsAudioSource.playOnAwake = false;
            ttsAudioSource.loop = false;                   // TTS不循环播放
            ttsAudioSource.volume = 0.8f;                  // 设置合适的音量
            ttsAudioSource.priority = 128;                 // 标准优先级
            ttsAudioSource.spatialBlend = 0.0f;            // 2D声音

            DontDestroyOnLoad(audioSourceObj);
            Debug.Log($"[ChatPanel] 已创建专用TTS AudioSource, 名称: {ttsAudioSource.gameObject.name}, 音量: {ttsAudioSource.volume}");
        }

        void InitializeComponents()
        {
            // 确保EventSystem存在
            EnsureEventSystem();

            // 确保FontManager已初始化
            FontManager.Instance.GetSIMSUNFont();

            // 创建父Canvas
            if (parentCanvas == null)
            {
                GameObject canvasObj = new GameObject("ChatPanelCanvas");
                parentCanvas = canvasObj.AddComponent<Canvas>();
                parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                if (canvasObj.GetComponent<GraphicRaycaster>() == null)
                {
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }

            if (parentCanvas != null)
            {
                parentCanvas.overrideSorting = true;
                parentCanvas.sortingOrder = 1000;

                if (parentCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            // 如果主面板不存在,创建它
            if (mainPanel == null)
            {
                CreateMainPanel();
            }
        }

        void EnsureEventSystem()
        {
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystem = eventSystemObj.AddComponent<EventSystem>();
                StandaloneInputModule inputModule = eventSystemObj.AddComponent<StandaloneInputModule>();
                inputModule.forceModuleActive = true;
                Debug.Log("已创建 EventSystem 和 StandaloneInputModule");
            }
            else
            {
                if (!eventSystem.enabled)
                {
                    eventSystem.enabled = true;
                }

                StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (inputModule == null)
                {
                    inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                    inputModule.forceModuleActive = true;
                }
                else
                {
                    inputModule.enabled = true;
                    inputModule.forceModuleActive = true;
                }
            }

            if (EventSystem.current == null || EventSystem.current != eventSystem)
            {
                EventSystem.current = eventSystem;
            }
        }

        void SetupUI()
        {
            // 设置语音输入按钮点击事件
            if (voiceInputButton != null)
                voiceInputButton.onClick.AddListener(OnVoiceInputButtonClicked);

            // 设置输入框回车发送事件
            if (inputField != null)
                inputField.onSubmit.AddListener((text) => SendMessage());

            // 设置关闭按钮
            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);
        }

        void CreateMainPanel()
        {
            Debug.Log("开始创建ChatPanel主面板");

            // 创建主面板
            mainPanel = new GameObject("ChatPanel");
            mainPanel.transform.SetParent(parentCanvas.transform, false);

            Debug.Log("ChatPanel主面板已创建");

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.25f);
            panelRect.anchorMax = new Vector2(0.7f, 0.75f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            // 添加圆角遮罩(如果存在mask.png)
            Sprite maskSprite = Resources.Load<Sprite>("mask");
            if (maskSprite != null)
            {
                // 直接在主面板上添加Mask组件
                Image panelBg = mainPanel.AddComponent<Image>();
                panelBg.sprite = maskSprite;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = Color.white;
                panelBg.raycastTarget = true;

                Mask panelMask = mainPanel.AddComponent<Mask>();
                panelMask.showMaskGraphic = false; // 不显示遮罩图片

                // 创建边框层(使用圆角矩形.png)
                GameObject borderObj = new GameObject("BorderLayer");
                borderObj.transform.SetParent(mainPanel.transform, false);
                RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;
                borderObj.transform.SetAsFirstSibling(); // 最底层

                Image borderImg = borderObj.AddComponent<Image>();
                Sprite roundedRectSprite = Resources.Load<Sprite>("圆角矩形");
                if (roundedRectSprite != null)
                {
                    borderImg.sprite = roundedRectSprite;
                    borderImg.type = Image.Type.Sliced;
                    borderImg.color = new Color(0.3f, 0.4f, 0.6f, 1f); // 边框颜色
                }
                else
                {
                    // 如果没有圆角矩形.png,使用mask.png作为边框
                    borderImg.sprite = maskSprite;
                    borderImg.type = Image.Type.Sliced;
                    borderImg.color = new Color(0.3f, 0.4f, 0.6f, 1f);
                }
                borderImg.raycastTarget = false;

                // 创建背景层显示原来的背景图
                GameObject bgLayerObj = new GameObject("BackgroundLayer");
                bgLayerObj.transform.SetParent(mainPanel.transform, false);
                RectTransform bgLayerRect = bgLayerObj.AddComponent<RectTransform>();
                bgLayerRect.anchorMin = Vector2.zero;
                bgLayerRect.anchorMax = Vector2.one;
                bgLayerRect.offsetMin = Vector2.zero;
                bgLayerRect.offsetMax = Vector2.zero;
                bgLayerObj.transform.SetAsFirstSibling(); // 第二层

                Image bgLayerImg = bgLayerObj.AddComponent<Image>();
                Sprite settingsBackgroundSprite = Resources.Load<Sprite>("settingsBackground");
                if (settingsBackgroundSprite != null)
                {
                    bgLayerImg.sprite = settingsBackgroundSprite;
                    bgLayerImg.color = Color.white;
                }
                bgLayerImg.raycastTarget = false;

                Debug.Log("[ChatPanel] 已添加圆角遮罩、边框和背景层");
            }
            else
            {
                // 如果没有mask.png,直接使用背景图
                Image panelBg = mainPanel.AddComponent<Image>();
                Sprite settingsBackgroundSprite = Resources.Load<Sprite>("settingsBackground");
                panelBg.sprite = settingsBackgroundSprite;
                panelBg.color = Color.white;
                panelBg.raycastTarget = true;
                Debug.LogWarning("[ChatPanel] 未找到mask.png,使用普通背景");
            }

            // 确保主面板阻塞射线
            CanvasGroup panelCanvasGroup = mainPanel.AddComponent<CanvasGroup>();
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable = true;

            EventTrigger panelClickTrigger = mainPanel.GetComponent<EventTrigger>();
            if (panelClickTrigger == null)
            {
                panelClickTrigger = mainPanel.AddComponent<EventTrigger>();
            }
            EventTrigger.Entry panelClickEntry = new EventTrigger.Entry();
            panelClickEntry.eventID = EventTriggerType.PointerClick;
            panelClickEntry.callback.AddListener((data) => OnMainPanelClicked((PointerEventData)data));
            panelClickTrigger.triggers.Add(panelClickEntry);

            mainPanel.SetActive(true);

            // 创建标题栏
            CreateTitleBar();

            // 创建聊天内容区域
            CreateChatContentArea();

            // 创建聊天底部分隔线
            CreateChatBottomBorder();

            // 创建输入区域
            CreateInputArea();

            // 创建关闭按钮
            CreateCloseButton();

            mainPanel.SetActive(false);
        }

        void CreateTitleBar()
        {
            Debug.Log("开始创建标题栏");
            GameObject titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(mainPanel.transform, false);
            Debug.Log("标题栏已创建并设置父对象");

            RectTransform titleRect = titleBar.GetComponent<RectTransform>();
            if (titleRect == null)
            {
                titleRect = titleBar.AddComponent<RectTransform>();
            }
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0, 60);

            Image titleBg = titleBar.AddComponent<Image>();
            Sprite titleBgSprite = Resources.Load<Sprite>("titleBackground");
            if (titleBgSprite != null)
            {
                titleBg.sprite = titleBgSprite;
            }

            // 创建标题底部分隔线
            GameObject titleBorder = new GameObject("TitleBorder");
            titleBorder.transform.SetParent(titleBar.transform, false);
            RectTransform borderRect = titleBorder.AddComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0, 0);
            borderRect.anchorMax = new Vector2(1, 0);
            borderRect.pivot = new Vector2(0.5f, 0);
            borderRect.anchoredPosition = Vector2.zero;
            borderRect.sizeDelta = new Vector2(0, 2);
            Image borderImg = titleBorder.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.4f, 0.6f, 0.5f);

            GameObject titleText = new GameObject("TitleText");
            titleText.transform.SetParent(titleBar.transform, false);

            RectTransform titleTextRect = titleText.GetComponent<RectTransform>();
            if (titleTextRect == null)
            {
                titleTextRect = titleText.AddComponent<RectTransform>();
            }
            titleTextRect.anchorMin = Vector2.zero;
            titleTextRect.anchorMax = Vector2.one;
            titleTextRect.offsetMin = new Vector2(20, 0);
            titleTextRect.offsetMax = new Vector2(-20, 0);

            TMP_Text title = titleText.AddComponent<TextMeshProUGUI>();
            title.text = "AI助手";
            title.fontSize = 36;
            title.color = textColor;
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            FontManager.ApplyFont(title);
        }

        void CreateChatContentArea()
        {
            GameObject contentArea = new GameObject("ChatContentArea");
            contentArea.transform.SetParent(mainPanel.transform, false);

            RectTransform contentRect = contentArea.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = contentArea.AddComponent<RectTransform>();
            }

            // 计算布局:标题栏60 + 上边距10 + 输入框高度50 + 下边距10 + 分隔线间距
            float topOffset = 70f;   // 标题栏60 + 上边距10
            float bottomOffset = 67f; // 输入框50 + 下边距10 + 分隔线2 + 间距5

            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(10, bottomOffset); // 左边距10,底部留出空间给输入框
            contentRect.offsetMax = new Vector2(-10, -topOffset);  // 右边距10,顶部留出空间给标题栏

            // 创建滚动视图
            GameObject scrollObj = new GameObject("ChatScrollRect");
            scrollObj.transform.SetParent(contentArea.transform, false);
            RectTransform scrollRectTrans = scrollObj.GetComponent<RectTransform>();
            if (scrollRectTrans == null)
            {
                scrollRectTrans = scrollObj.AddComponent<RectTransform>();
            }
            scrollRectTrans.anchorMin = Vector2.zero;
            scrollRectTrans.anchorMax = Vector2.one;
            scrollRectTrans.offsetMin = Vector2.zero;
            scrollRectTrans.offsetMax = Vector2.zero;

            scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f; // 提高滚动灵敏度(默认是10)

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0f);

            // 创建视口
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            if (viewportRect == null)
            {
                viewportRect = viewport.AddComponent<RectTransform>();
            }
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportMask = viewport.AddComponent<Image>();
            viewportMask.color = new Color(0.15f, 0.16f, 0.20f, 0.3f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            scrollRect.viewport = viewportRect;

            // 创建消息容器
            GameObject messageContainerObj = new GameObject("MessageContainer");
            messageContainerObj.transform.SetParent(viewport.transform, false);

            RectTransform containerRect = messageContainerObj.GetComponent<RectTransform>();
            if (containerRect == null)
            {
                containerRect = messageContainerObj.AddComponent<RectTransform>();
            }
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(0.5f, 1);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(0, 100);

            messageContainer = containerRect;
            scrollRect.content = containerRect;
        }

        void CreateChatBottomBorder()
        {
            // 创建聊天区域底部分隔线(和标题栏一样长)
            GameObject chatBottomBorder = new GameObject("ChatBottomBorder");
            chatBottomBorder.transform.SetParent(mainPanel.transform, false);
            RectTransform chatBorderRect = chatBottomBorder.AddComponent<RectTransform>();
            chatBorderRect.anchorMin = new Vector2(0, 0);
            chatBorderRect.anchorMax = new Vector2(1, 0);
            chatBorderRect.pivot = new Vector2(0.5f, 0);
            chatBorderRect.anchoredPosition = new Vector2(0, 67f); // 距离底部67像素(与输入框顶部对齐)
            chatBorderRect.sizeDelta = new Vector2(0, 2);
            Image chatBorderImg = chatBottomBorder.AddComponent<Image>();
            chatBorderImg.color = new Color(0.3f, 0.4f, 0.6f, 0.5f);
        }

        void CreateInputArea()
        {
            GameObject inputArea = new GameObject("InputArea");
            inputArea.transform.SetParent(mainPanel.transform, false);

            RectTransform inputAreaRect = inputArea.GetComponent<RectTransform>();
            if (inputAreaRect == null)
            {
                inputAreaRect = inputArea.AddComponent<RectTransform>();
            }
            inputAreaRect.anchorMin = new Vector2(0, 0);
            inputAreaRect.anchorMax = new Vector2(0, 0);
            inputAreaRect.pivot = new Vector2(0, 0);
            inputAreaRect.anchoredPosition = new Vector2(5, 10);
            inputAreaRect.sizeDelta = new Vector2(760, 50); // 固定宽度760，高度50

            Image inputAreaBg = inputArea.AddComponent<Image>();
            // 设置为透明背景
            inputAreaBg.color = new Color(1f, 1f, 1f, 0f); // 完全透明

            // 创建输入框
            GameObject inputFieldObj = new GameObject("InputField");
            inputFieldObj.transform.SetParent(inputArea.transform, false);
            RectTransform inputFieldRect = inputFieldObj.GetComponent<RectTransform>();
            if (inputFieldRect == null)
            {
                inputFieldRect = inputFieldObj.AddComponent<RectTransform>();
            }
            // 手动定位：距离左边15像素，垂直居中
            inputFieldRect.anchorMin = new Vector2(0, 0.5f);
            inputFieldRect.anchorMax = new Vector2(0, 0.5f);
            inputFieldRect.pivot = new Vector2(0, 0.5f);
            inputFieldRect.anchoredPosition = new Vector2(15, 0);
            inputFieldRect.sizeDelta = new Vector2(670, 40); // 固定宽高
            // 使用LayoutElement控制宽度,避免Inspector显示问题
            LayoutElement inputFieldLayout = inputFieldObj.AddComponent<LayoutElement>();
            inputFieldLayout.preferredWidth = 670;
            inputFieldLayout.minHeight = 30;
            inputFieldLayout.preferredHeight = 30;

            Image inputBg = inputFieldObj.AddComponent<Image>();
            // 使用input.png作为输入框背景
            Sprite inputSprite = Resources.Load<Sprite>("input");
            if (inputSprite != null)
            {
                inputBg.sprite = inputSprite;
                inputBg.type = Image.Type.Sliced;
                inputBg.pixelsPerUnitMultiplier = 2f;
                inputBg.color = Color.white;
            }
            else
            {
                inputBg.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            }

            inputField = inputFieldObj.AddComponent<TMP_InputField>();

            GameObject textAreaObj = new GameObject("TextArea");
            textAreaObj.transform.SetParent(inputFieldObj.transform, false);
            RectTransform textAreaRect = textAreaObj.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(5, 5);
            textAreaRect.offsetMax = new Vector2(-5, -5);

            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textAreaObj.transform, false);
            RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            TMP_Text placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholder.text = "和小智对话...";
            placeholder.fontSize = 18;
            placeholder.color = new Color(0.45f, 0.48f, 0.55f, 0.6f);
            placeholder.alignment = TextAlignmentOptions.Left; // 左对齐,垂直居中
            FontManager.ApplyFont(placeholder);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(textAreaObj.transform, false);
            RectTransform textObjRect = textObj.AddComponent<RectTransform>();
            textObjRect.anchorMin = Vector2.zero;
            textObjRect.anchorMax = Vector2.one;
            textObjRect.offsetMin = Vector2.zero;
            textObjRect.offsetMax = new Vector2(-5, 0);

            TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Left; // 左对齐,垂直居中
            FontManager.ApplyFont(text);

            inputField.textViewport = textAreaRect;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            // 创建语音输入按钮
            GameObject voiceBtnObj = new GameObject("VoiceInputButton");
            voiceBtnObj.transform.SetParent(inputArea.transform, false);
            RectTransform voiceBtnRect = voiceBtnObj.AddComponent<RectTransform>();
            // 手动定位：距离右边15像素，垂直居中
            voiceBtnRect.anchorMin = new Vector2(1, 0.5f);
            voiceBtnRect.anchorMax = new Vector2(1, 0.5f);
            voiceBtnRect.pivot = new Vector2(1, 0.5f);
            voiceBtnRect.anchoredPosition = new Vector2(-15, 0);
            voiceBtnRect.sizeDelta = new Vector2(50, 50); // 设置为正方形50x50
            LayoutElement voiceBtnLayout = voiceBtnObj.AddComponent<LayoutElement>();
            voiceBtnLayout.minWidth = 50;
            voiceBtnLayout.preferredWidth = 50;
            voiceBtnLayout.minHeight = 50;
            voiceBtnLayout.preferredHeight = 50;
            voiceBtnLayout.flexibleWidth = 0; // 不允许伸缩,保持固定宽度

            voiceInputButton = voiceBtnObj.AddComponent<Button>();
            Image voiceBtnImage = voiceBtnObj.AddComponent<Image>();
            voiceBtnImage.preserveAspect = true; // 保持图标比例
            // 尝试加载语音图标
            Sprite voiceIconSprite = Resources.Load<Sprite>("mic");
            if (voiceIconSprite != null)
            {
                voiceBtnImage.sprite = voiceIconSprite;
                voiceBtnImage.color = Color.white;
            }
            else
            {
                Debug.LogWarning("[ChatPanel] 未找到Resources/mic.png");
                voiceBtnImage.color = new Color(0.23f, 0.45f, 0.85f, 1f);
            }
            voiceInputButton.targetGraphic = voiceBtnImage;
        }

        void CreateCloseButton()
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            // 将关闭按钮放在标题栏中
            Transform titleBar = null;

            // 尝试通过名称查找
            titleBar = mainPanel.transform.Find("TitleBar");

            // 如果找不到,遍历子对象查找
            if (titleBar == null)
            {
                foreach (Transform child in mainPanel.transform)
                {
                    if (child.name == "TitleBar")
                    {
                        titleBar = child;
                        break;
                    }
                }
            }

            if (titleBar == null)
            {
                Debug.LogError("找不到TitleBar,无法创建关闭按钮。当前mainPanel子对象数量: " + mainPanel.transform.childCount);
                // 输出所有子对象名称用于调试
                foreach (Transform child in mainPanel.transform)
                {
                    Debug.Log("子对象: " + child.name);
                }
                return;
            }

            closeBtnObj.transform.SetParent(titleBar, false);

            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            if (closeRect == null)
            {
                closeRect = closeBtnObj.AddComponent<RectTransform>();
            }
            // 定位在标题栏右侧，垂直居中
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-15, 0);
            closeRect.sizeDelta = new Vector2(45, 45);

            closeButton = closeBtnObj.AddComponent<Button>();
            Image closeImage = closeBtnObj.AddComponent<Image>();
            closeImage.sprite = Resources.Load<Sprite>("close@2x");
            closeImage.color = Color.white; // 确保图标颜色为白色
            closeButton.targetGraphic = closeImage;

            Debug.Log("关闭按钮已创建在标题栏中");
        }

        void RemoveLayoutComponents(GameObject obj)
        {
            if (obj == null) return;

            var verticalLayout = obj.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (verticalLayout != null)
            {
                Destroy(verticalLayout);
            }

            var horizontalLayout = obj.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (horizontalLayout != null)
            {
                Destroy(horizontalLayout);
            }

            var contentSizeFitter = obj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (contentSizeFitter != null)
            {
                Destroy(contentSizeFitter);
            }

            foreach (Transform child in obj.transform)
            {
                RemoveLayoutComponents(child.gameObject);
            }
        }

        void FindAvatarAnimator()
        {
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

        public void OpenPanel()
        {
            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
                StartCoroutine(FocusInputFieldDelayed());
            }
        }

        public void ClosePanel()
        {
            if (mainPanel != null)
            {
                mainPanel.SetActive(false);
            }
        }

        public void TogglePanel()
        {
            if (mainPanel != null)
            {
                bool willOpen = !mainPanel.activeSelf;
                mainPanel.SetActive(willOpen);

                if (willOpen)
                {
                    StartCoroutine(FocusInputFieldDelayed());
                }
            }
        }

        public bool IsPanelOpen()
        {
            return mainPanel != null && mainPanel.activeSelf;
        }

        void OnMainPanelClicked(PointerEventData eventData)
        {
            // 可以在这里处理面板点击事件
            // 例如：取消消息选中状态等
            Debug.Log("主面板被点击");
        }

        IEnumerator FocusInputFieldDelayed()
        {
            yield return new WaitForEndOfFrame();

            if (inputField != null)
            {
                inputField.ActivateInputField();
                inputField.Select();
                Debug.Log("聊天面板已打开,输入框已聚焦");
            }
        }

        void FocusInputField()
        {
            if (inputField != null && inputField.interactable)
            {
                inputField.ActivateInputField();
                inputField.Select();
            }
        }

        void OnVoiceInputButtonClicked()
        {
            if (isRealTimeVoiceChatActive)
            {
                StopRealTimeVoiceChat();
                Debug.Log("停止实时语音对话");
            }
            else
            {
                StartRealTimeVoiceChat();
                Debug.Log("启动实时语音对话");
            }
        }

        void UpdateVoiceButtonIcon(bool isVoiceMode)
        {
            if (voiceInputButton == null) return;

            Image buttonImage = voiceInputButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                // 根据模式切换图标
                Sprite targetIcon = null;

                if (isVoiceMode)
                {
                    // 语音模式:显示键盘图标(点击可切换回文字输入)
                    targetIcon = keyboardIcon;
                }
                else
                {
                    // 文字模式:显示语音图标(点击可切换到语音输入)
                    targetIcon = voiceIcon;
                }

                if (targetIcon != null)
                {
                    buttonImage.sprite = targetIcon;
                    buttonImage.color = Color.white;
                }
                else
                {
                    // 没有图标时,使用颜色区分模式
                    buttonImage.color = isVoiceMode ? new Color(0.8f, 0.3f, 0.3f, 1f) : new Color(0.23f, 0.45f, 0.85f, 1f);
                }
            }
        }

        public void SendMessage()
        {
            string text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            inputField.interactable = false;

            StopCurrentTTS();
            AddMessage(text, true);
            inputField.text = "";

            StartCoroutine(ChatWithRAGFlow(text));
        }

        void AddMessage(string message, bool isUserMessage)
        {
            CreateMessageObject(message, isUserMessage);
        }

        GameObject CreateMessageObject(string message, bool isUserMessage)
        {
            GameObject prefab = isUserMessage ? userMessagePrefab : aiMessagePrefab;
            bool usePrefab = prefab != null;

            GameObject messageObj;
            if (usePrefab)
            {
                messageObj = Instantiate(prefab, messageContainer);
            }
            else
            {
                // 如果没有设置预制件,创建简单的消息气泡
                messageObj = new GameObject(isUserMessage ? "UserMessage" : "AIMessage");
                messageObj.transform.SetParent(messageContainer, false);

                // 添加RectTransform
                RectTransform msgRect = messageObj.AddComponent<RectTransform>();

                // 添加背景图片
                Image bubbleImg = messageObj.AddComponent<Image>();

                // 为用户消息加载背景图片
                if (isUserMessage)
                {
                    Sprite userBubbleSprite = Resources.Load<Sprite>("chat");
                    if (userBubbleSprite != null)
                    {
                        bubbleImg.sprite = userBubbleSprite;
                        bubbleImg.type = Image.Type.Sliced; // 支持九宫格拉伸
                        bubbleImg.color = Color.white; // 图片显示原始颜色
                    }
                    else
                    {
                        Debug.LogWarning("[ChatPanel] 未找到Resources/chat.png,使用默认颜色");
                        bubbleImg.color = userBubbleColor;
                    }
                }
                else
                {
                    // AI消息使用纯色背景
                    bubbleImg.color = aiBubbleColor;
                }

                // 创建文本对象作为子对象
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(messageObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0, 1);
                textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 1);
                textRect.anchoredPosition = new Vector2(15, -15);
                textRect.sizeDelta = new Vector2(100, 30);

                TMP_Text textComp = textObj.AddComponent<TextMeshProUGUI>();
                textComp.fontSize = 18;
                textComp.color = isUserMessage ? userTextColor : aiTextColor;
                textComp.enableWordWrapping = true;
                FontManager.ApplyFont(textComp);
            }

            messageObj.name = isUserMessage ? "UserMessage" : "AIMessage";

            var layoutGroup = messageObj.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layoutGroup != null) Destroy(layoutGroup);

            var contentSizeFitter = messageObj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (contentSizeFitter != null) Destroy(contentSizeFitter);

            // 只有使用预制件时才尝试清理子对象的ContentSizeFitter
            if (usePrefab && messageObj.transform.childCount > 0)
            {
                var textObj = messageObj.transform.GetChild(0);
                if (textObj != null)
                {
                    var textFitter = textObj.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                    if (textFitter != null) Destroy(textFitter);
                }
            }

            RectTransform rect = messageObj.GetComponent<RectTransform>();

            if (isUserMessage)
            {
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-userMessageRightMargin, 0);
            }
            else
            {
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(aiMessageLeftMargin, 0);
            }
            rect.sizeDelta = new Vector2(minBubbleWidth, 40);

            TMP_Text textComponent = messageObj.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = message;
                textComponent.enableWordWrapping = true;
                textComponent.color = isUserMessage ? userTextColor : aiTextColor;
                FontManager.ApplyFont(textComponent);
            }

            // 如果使用预制件,也要设置用户消息的背景图片
            if (usePrefab && isUserMessage)
            {
                Image bubbleImage = messageObj.GetComponent<Image>();
                if (bubbleImage != null)
                {
                    Sprite userBubbleSprite = Resources.Load<Sprite>("chat");
                    if (userBubbleSprite != null)
                    {
                        bubbleImage.sprite = userBubbleSprite;
                        bubbleImage.type = Image.Type.Sliced;
                        bubbleImage.color = Color.white;
                    }
                }
            }

            StartCoroutine(UpdateBubbleSizeCoroutine(messageObj));

            return messageObj;
        }

        IEnumerator UpdateBubbleSizeCoroutine(GameObject bubble)
        {
            yield return new WaitForEndOfFrame();

            TMP_Text textComponent = bubble.GetComponentInChildren<TMP_Text>();
            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();

            if (textComponent == null || bubbleRect == null) yield break;

            bool isUserMessage = bubbleRect.anchorMin.x > 0.5f;

            if (isUserMessage)
            {
                bubbleRect.anchorMin = new Vector2(1, 1);
                bubbleRect.anchorMax = new Vector2(1, 1);
                bubbleRect.pivot = new Vector2(1, 1);
            }
            else
            {
                bubbleRect.anchorMin = new Vector2(0, 1);
                bubbleRect.anchorMax = new Vector2(0, 1);
                bubbleRect.pivot = new Vector2(0, 1);
            }

            float textWidth = textComponent.preferredWidth;
            float bubbleWidth = Mathf.Clamp(textWidth + bubblePadding * 2, minBubbleWidth, maxBubbleWidth);

            bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleRect.sizeDelta.y);

            RectTransform textRect = textComponent.GetComponent<RectTransform>();
            if (textRect != null)
            {
                float textWidthLimit = bubbleWidth - 30f;
                textRect.sizeDelta = new Vector2(textWidthLimit, textRect.sizeDelta.y);
            }

            textComponent.ForceMeshUpdate();

            float textHeight = textComponent.preferredHeight;
            float fontSize = textComponent.fontSize;
            float textPadding = 30f;
            float singleLineHeight = fontSize + textPadding;
            float bubbleHeight = textHeight + textPadding;

            if (bubbleHeight < singleLineHeight)
            {
                bubbleHeight = singleLineHeight;
            }

            bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

            float xPos = isUserMessage ? -userMessageRightMargin : aiMessageLeftMargin;
            bubbleRect.anchoredPosition = new Vector2(xPos, bubbleRect.anchoredPosition.y);

            UpdateAllMessagesVerticalPosition();
            ScrollToBottom();
        }

        void UpdateAllMessagesVerticalPosition()
        {
            if (messageContainer == null) return;

            float currentY = -10f;

            for (int i = 0; i < messageContainer.childCount; i++)
            {
                Transform child = messageContainer.GetChild(i);
                RectTransform rect = child.GetComponent<RectTransform>();

                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, currentY);
                    currentY -= (rect.rect.height + messageSpacing);
                }
            }

            UpdateContentHeight(-currentY + 10f);
        }

        void UpdateContentHeight(float newHeight)
        {
            if (messageContainer != null)
            {
                messageContainer.sizeDelta = new Vector2(messageContainer.sizeDelta.x, newHeight);
            }
        }

        void ScrollToBottom()
        {
            StartCoroutine(ScrollToBottomCoroutine());
        }

        IEnumerator ScrollToBottomCoroutine()
        {
            yield return new WaitForEndOfFrame();

            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                scrollRect.Rebuild(CanvasUpdate.Layout);
            }
        }

        // ==================== RAGFlow API 调用 ====================

        IEnumerator ChatWithRAGFlow(string userMessage)
        {
            chatInProgress = true;
            chatCancelledByVoice = false;

            currentAIBubble = CreateMessageObject("思考中...", false);

            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, true);

            bool chatCompleted = false;
            string fullResponse = "";

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
                    UpdateBubbleText(currentAIBubble, "抱歉,远程模型调用失败:" + error);
                    Debug.LogError(error);
                }
            ));

            while (!chatCompleted && !chatCancelledByVoice)
            {
                yield return null;
            }

            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);

            chatInProgress = false;
            ScrollToBottom();

            inputField.interactable = true;
            FocusInputField();

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
        }

        void UpdateBubbleText(GameObject bubble, string text)
        {
            if (bubble == null) return;

            TMP_Text textComponent = bubble.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = text;
                StartCoroutine(UpdateBubbleSizeCoroutine(bubble));
            }
        }

        IEnumerator CallRAGFlowAPI(string question,
            System.Action<string> onPartialResponse,
            System.Action onComplete,
            System.Action<string> onError)
        {
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

            string escapedQuestion = EscapeJsonString(question);
            string jsonData = $"{{\"question\":\"{escapedQuestion}\",\"stream\":true,\"session_id\":\"{currentSessionId}\",\"lang\":\"{ragflowLanguage}\"}}";

            UnityEngine.Networking.UnityWebRequest request = CreateRAGFlowRequest(questionUrl, jsonData);

            var operation = request.SendWebRequest();

            string lastProcessedText = "";

            while (!operation.isDone)
            {
                yield return null;

                if (request.downloadHandler != null)
                {
                    byte[] rawData = request.downloadHandler.data;
                    string responseText = (rawData != null && rawData.Length > 0)
                        ? System.Text.Encoding.UTF8.GetString(rawData)
                        : "";

                    if (responseText != lastProcessedText && !string.IsNullOrEmpty(responseText))
                    {
                        lastProcessedText = responseText;

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

        IEnumerator CreateRAGFlowSession()
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

        UnityEngine.Networking.UnityWebRequest CreateRAGFlowRequest(string url, string jsonData)
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

        string ParseSessionId(string jsonResponse)
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

        string ParseRAGFlowStreamResponse(string streamText)
        {
            string answer = "";

            try
            {
                string[] lines = streamText.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("\"data\": true") || line.Contains("\"data\":true"))
                        continue;

                    if (line.StartsWith("data:"))
                    {
                        string jsonLine = line.Substring(5).Trim();
                        if (!string.IsNullOrEmpty(jsonLine))
                        {
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

        string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }

        string UnescapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            return str.Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t")
                      .Replace("\\\"", "\"")
                      .Replace("\\\\", "\\");
        }

        // ==================== TTS语音播放功能 ====================

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

            // 强制加载音频数据
            if (audioClip.loadState == AudioDataLoadState.Unloaded)
            {
                audioClip.LoadAudioData();
            }

            // 等待音频数据完全加载
            float timeout = 5f;
            float elapsed = 0f;
            while (audioClip.loadState == AudioDataLoadState.Loading && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
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

            UpdateVoiceButtonIcon(true);

            Debug.Log("开始实时语音对话");
            realTimeVoiceChatCoroutine = StartCoroutine(RealTimeVoiceChatLoop());
        }

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

            StopCurrentTTS();
            CancelCurrentChat();

            audioSegments.Clear();

            UpdateVoiceButtonIcon(false);

            Debug.Log("实时语音对话已停止");
        }

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

                int currentPosition = Microphone.GetPosition(microphoneDevice);
                if (currentPosition < 0) continue;

                int samplesToRead = 0;
                if (currentPosition >= lastReadPosition)
                {
                    samplesToRead = currentPosition - lastReadPosition;
                }
                else
                {
                    samplesToRead = (recordingClip.samples - lastReadPosition) + currentPosition;
                }

                if (samplesToRead <= 0) continue;

                float[] samples = new float[samplesToRead * recordingClip.channels];
                int readStart = lastReadPosition;

                if (currentPosition >= lastReadPosition)
                {
                    recordingClip.GetData(samples, readStart);
                }
                else
                {
                    int firstPartSamples = (recordingClip.samples - lastReadPosition) * recordingClip.channels;
                    float[] firstPart = new float[firstPartSamples];
                    recordingClip.GetData(firstPart, lastReadPosition);

                    int secondPartSamples = currentPosition * recordingClip.channels;
                    float[] secondPart = new float[secondPartSamples];
                    recordingClip.GetData(secondPart, 0);

                    Array.Copy(firstPart, 0, samples, 0, firstPartSamples);
                    Array.Copy(secondPart, 0, samples, firstPartSamples, secondPartSamples);
                }

                lastReadPosition = currentPosition;

                float audioDuration = (float)samplesToRead / sampleRate;

                bool hasVoiceActivity = CheckVADActivity(samples, recordingClip.channels);

                if (hasVoiceActivity)
                {
                    Debug.Log($"检测到语音活动 (时长: {audioDuration:F2}秒)");
                    lastActiveTime = Time.time;

                    if (!isInSpeech)
                    {
                        currentSpeechBuffer.AddRange(preRollBuffer);
                        isInSpeech = true;
                    }

                    currentSpeechBuffer.AddRange(samples);
                }
                else if (isInSpeech)
                {
                    currentSpeechBuffer.AddRange(samples);
                }

                foreach (var s in samples)
                {
                    if (preRollBuffer.Count >= preRollSamples)
                        preRollBuffer.Dequeue();

                    preRollBuffer.Enqueue(s);
                }

                if (isInSpeech && Time.time - lastActiveTime > postRollSeconds)
                {
                    FinalizeSegment();
                }

                if (Time.time - lastActiveTime > noSpeechThreshold)
                {
                    if (audioSegments.Count > 0)
                    {
                        if (isTTSPlaying || chatInProgress)
                        {
                            CancelCurrentChat();
                            StopCurrentTTS();
                            chatCancelledByVoice = true;
                            Debug.Log("检测到新语音,已打断当前模型输出/TTS");
                        }
                        StartCoroutine(ProcessAudioSegments());
                    }
                }
            }

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

            int sampleCount = currentSpeechBuffer.Count / 1;
            if (sampleCount <= 0)
            {
                currentSpeechBuffer.Clear();
                isInSpeech = false;
                return;
            }

            AudioClip clip = AudioClip.Create(
                $"Speech_{audioFileCount++}",
                sampleCount,
                1,
                sampleRate,
                false
            );

            clip.SetData(currentSpeechBuffer.ToArray(), 0);
            audioSegments.Add(clip);

            currentSpeechBuffer.Clear();
            isInSpeech = false;
        }

        bool CheckVADActivity(float[] samples, int channels)
        {
            if (samples == null || samples.Length == 0) return false;

            const int frameSize = (int)(sampleRate * 0.02f);
            int activeFrames = 0;
            int totalFrames = 0;

            for (int i = 0; i < samples.Length - frameSize * channels; i += frameSize * channels)
            {
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

        IEnumerator ProcessAudioSegments()
        {
            if (audioSegments.Count == 0 || isProcessingAudio) yield break;

            isProcessingAudio = true;

            AudioSource audioSourceToUse = ttsAudioSource != null ? ttsAudioSource : null;
            if (audioSourceToUse != null && audioSourceToUse.isPlaying)
            {
                audioSourceToUse.Stop();
                Debug.Log("检测到新的有效语音,已停止当前TTS播放");
            }

            int segmentCount = audioSegments.Count;
            AudioClip mergedClip = MergeAudioClips(audioSegments);
            if (mergedClip == null)
            {
                isProcessingAudio = false;
                yield break;
            }

            Debug.Log($"处理音频段,共 {segmentCount} 段,总时长: {mergedClip.length:F2}秒");

            audioSegments.Clear();

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
                Debug.LogWarning("转文字结果为空,跳过处理");
                isProcessingAudio = false;
                yield break;
            }

            Debug.Log($"转文字结果: {transcribedText}");

            AddMessage(transcribedText, true);

            yield return StartCoroutine(ChatWithRAGFlow(transcribedText));

            isProcessingAudio = false;
            lastActiveTime = Time.time;
        }

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

        async Task<string> TranscribeAudioWithResult(AudioClip audioClip)
        {
            if (audioClip == null)
            {
                return "";
            }

            if (xunFeiSpeechService == null)
            {
                Debug.LogError("讯飞语音服务未初始化,无法转文字");
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

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) && inputField != null && inputField.isFocused)
            {
                SendMessage();
            }
        }
    }
}
