using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 设置界面UI管理器
    /// 包含PPT、模型、设置三个标签页
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        [Header("Canvas Reference")]
        public Canvas parentCanvas;

        [Header("Main Panel")]
        public GameObject mainPanel;
        public Button closeButton;

        [Header("Tab Buttons")]
        public Button pptTabButton;
        public Button modelTabButton;
        public Button settingsTabButton;

        [Header("Tab Panels")]
        public GameObject pptPanel;
        public GameObject modelPanel;
        public GameObject settingsPanel;

        [Header("PPT Panel Components")]
        public DropdownManager pptDropdown;
        public TMP_Dropdown pptDropdownTMP;
        public Button generateSpeechButton;
        public TMP_InputField speechInputField;
        public GameObject pptLoadingIndicator;
        
        // 新的PPT面板组件
        public Button addPPTButton;
        public ScrollRect pptListScrollRect;
        public GameObject pptListContent;
        public Button configButton;
        public Button playButton;
        public GameObject configPanel;
        public TMP_InputField configInputField;
        public Button confirmConfigButton;
        private GameObject configOverlay;

        [Header("Model Panel Components")]
        public Button changeModelButton;
        public TMP_Text currentModelText;
        public Button resetModelButton;

        [Header("Settings Panel Components")]
        public ScrollRect changelogScrollRect;
        public TMP_Text changelogText;
        public Button exitButton;
        
        [Header("Canvas Sorting")]
        [Tooltip("设置面板所在Canvas的排序层级，确保高于数字人以拦截点击")]
        public int canvasSortingOrder = 1000;

        private AutoDesc autoDesc;
        private VRMLoader vrmLoader;
        private int currentTabIndex = 0; // 0=PPT, 1=Model, 2=Settings
        private TMP_FontAsset simsunFont; // SIMSUN 字体资源
        
        // PPT列表管理
        private List<PPTListItem> pptListItems = new List<PPTListItem>();
        private PPTListItem selectedPPTItem = null;

        [SerializeField] private PPTController pptController;
        
        // PPT列表项数据结构
        private class PPTListItem
        {
            public GameObject itemObj;
            public PPTInfo pptInfo;
            public Button itemButton;
            public TMP_Text fileNameText;
            public TMP_Text pageCountText;
            public TMP_Text statusText;
        }
        
        // Windows API 导入（用于文件对话框）
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        void Start()
        {
            InitializeComponents();
            SetupUI();
            LoadChangelog();
            // 注意：如果面板是关闭的，ShowTab 可能不会正确显示，所以在 OpenPanel 时再调用
        }

        void Awake()
        {
            // 确保组件在 Awake 时也能初始化
            if (mainPanel == null)
            {
                InitializeComponents();
            }
        }

        void InitializeComponents()
        {
            // 确保 EventSystem 存在（UI 点击需要）
            EnsureEventSystem();
            
            // 加载 SIMSUN 字体
            LoadSIMSUNFont();
            
            // 创建父Canvas
            if (parentCanvas == null)
            {
                GameObject canvasObj = new GameObject("SettingsPanelCanvas");
                parentCanvas = canvasObj.AddComponent<Canvas>();
                parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // 不设置过高的sortingOrder
                
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                
                // 确保有 GraphicRaycaster（用于 UI 点击检测）
                if (canvasObj.GetComponent<GraphicRaycaster>() == null)
                {
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }
            
            if (parentCanvas != null)
            {
                parentCanvas.overrideSorting = true;
                parentCanvas.sortingOrder = canvasSortingOrder;
                
                // 追加一个GraphicRaycaster（若不存在）
                if (parentCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            // 查找相关组件
            if (autoDesc == null)
                autoDesc = FindFirstObjectByType<AutoDesc>();

            if (vrmLoader == null)
                vrmLoader = FindFirstObjectByType<VRMLoader>();

            // 如果主面板不存在，创建它
            if (mainPanel == null)
            {
                CreateMainPanel();
            }
        }
        
        void EnsureEventSystem()
        {
            // 检查是否存在 EventSystem
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystem = eventSystemObj.AddComponent<EventSystem>();
                
                // 添加 StandaloneInputModule（用于鼠标和键盘输入）
                StandaloneInputModule inputModule = eventSystemObj.AddComponent<StandaloneInputModule>();
                inputModule.forceModuleActive = true;
                
                Debug.Log("已创建 EventSystem 和 StandaloneInputModule");
            }
            else
            {
                // 确保 EventSystem 已启用
                if (!eventSystem.enabled)
                {
                    eventSystem.enabled = true;
                }
                
                // 确保有 StandaloneInputModule
                StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (inputModule == null)
                {
                    inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                    inputModule.forceModuleActive = true;
                }
                else
                {
                    // 确保输入模块已启用并强制激活
                    inputModule.enabled = true;
                    inputModule.forceModuleActive = true;
                }
                
                Debug.Log($"使用现有 EventSystem: {eventSystem.name}");
            }
            
            // 确保 EventSystem.current 指向正确的 EventSystem
            if (EventSystem.current == null || EventSystem.current != eventSystem)
            {
                EventSystem.current = eventSystem;
            }
        }
        
        void LoadSIMSUNFont()
        {
            // 尝试从 Resources 加载
            simsunFont = Resources.Load<TMP_FontAsset>("SIMSUN SDF");
            
            // 如果 Resources 中没有，尝试从 Assets 路径加载
            if (simsunFont == null)
            {
                string fontPath = "Assets/MATE ENGINE - Fonts/Asia Fonts/SIMSUN SDF.asset";
                #if UNITY_EDITOR
                simsunFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
                #endif
            }
            
            // 如果还是找不到，尝试通过名称查找
            if (simsunFont == null)
            {
                TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var font in allFonts)
                {
                    if (font.name.Contains("SIMSUN") || font.name.Contains("SimSun"))
                    {
                        simsunFont = font;
                        break;
                    }
                }
            }
            
            if (simsunFont != null)
            {
                Debug.Log($"已加载字体: {simsunFont.name}");
            }
            else
            {
                Debug.LogWarning("未找到 SIMSUN 字体，将使用默认字体");
            }
        }
        
        void ApplyFontToTMP(TMP_Text tmpText)
        {
            if (tmpText != null && simsunFont != null)
            {
                tmpText.font = simsunFont;
            }
        }
        
        void ApplyFontToAllTMP()
        {
            if (simsunFont == null) return;
            
            // 查找面板内所有 TextMeshPro 组件并应用字体
            if (mainPanel != null)
            {
                TMP_Text[] allTMPs = mainPanel.GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in allTMPs)
                {
                    if (tmp != null)
                    {
                        tmp.font = simsunFont;
                    }
                }
                Debug.Log($"已应用字体到 {allTMPs.Length} 个 TextMeshPro 组件");
            }
        }
        
        void EnsureUIInteractable()
        {
            if (mainPanel == null) return;
            
            // 确保所有按钮可以交互
            Button[] allButtons = mainPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (btn != null)
                {
                    btn.interactable = true;
                    // 确保按钮的 Image 组件可以接收射线检测
                    Image btnImage = btn.GetComponent<Image>();
                    if (btnImage != null)
                    {
                        btnImage.raycastTarget = true;
                    }
                }
            }
            
            // 确保所有 Image 组件可以接收射线检测（用于点击）
            Image[] allImages = mainPanel.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                if (img != null)
                {
                    // 只有作为按钮背景的 Image 需要接收射线
                    if (img.GetComponent<Button>() != null || img.GetComponent<Toggle>() != null)
                    {
                        img.raycastTarget = true;
                    }
                }
            }
            
            // 确保所有 TMP_Dropdown 可以交互
            TMP_Dropdown[] allDropdowns = mainPanel.GetComponentsInChildren<TMP_Dropdown>(true);
            foreach (var dropdown in allDropdowns)
            {
                if (dropdown != null)
                {
                    dropdown.interactable = true;
                }
            }
            
            // 确保所有 InputField 可以交互（旧版 UI）
            InputField[] allInputFields = mainPanel.GetComponentsInChildren<InputField>(true);
            foreach (var inputField in allInputFields)
            {
                if (inputField != null)
                {
                    inputField.interactable = true;
                }
            }

            // 确保所有 TMP_InputField 可以交互（TMP 版本输入框）
            TMP_InputField[] allTMPInputFields = mainPanel.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var tmpInputField in allTMPInputFields)
            {
                if (tmpInputField != null)
                {
                    tmpInputField.interactable = true;
                }
            }
            
            Debug.Log($"已确保 {allButtons.Length} 个按钮、{allDropdowns.Length} 个下拉框、{allInputFields.Length + allTMPInputFields.Length} 个输入框可交互");
        }

        void SetupUI()
        {
            // 设置标签页按钮
            if (pptTabButton != null)
                pptTabButton.onClick.AddListener(() => ShowTab(0));

            if (modelTabButton != null)
                modelTabButton.onClick.AddListener(() => ShowTab(1));

            if (settingsTabButton != null)
                settingsTabButton.onClick.AddListener(() => ShowTab(2));

            // 设置关闭按钮
            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            // 注意: generateSpeechButton的点击事件已在CreatePPTPanelContent中设置

            // 设置模型面板按钮
            if (changeModelButton != null)
                changeModelButton.onClick.AddListener(OnChangeModel);

            if (resetModelButton != null)
                resetModelButton.onClick.AddListener(OnResetModel);

            // 设置退出按钮
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExit);

            UpdateModelInfo();
        }

        void CreateMainPanel()
        {
            // 创建主面板
            mainPanel = new GameObject("SettingsPanel");
            mainPanel.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.1f);
            panelRect.anchorMax = new Vector2(0.9f, 0.9f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            Image panelBg = mainPanel.AddComponent<Image>();
            // 主面板背景：白色半透明，现代浅色风格
            panelBg.color = new Color(1f, 1f, 1f, 0.98f);
            // 确保使用默认UI材质，避免_MainTex警告
            panelBg.material = null;
            panelBg.raycastTarget = true; // 阻止点击穿透到场景

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

            // 先激活面板，确保子组件能正确创建和初始化
            mainPanel.SetActive(true);

            // 创建标题栏
            CreateTitleBar();

            // 创建标签页按钮栏
            CreateTabBar();

            // 创建内容区域
            CreateContentArea();

            // 创建关闭按钮
            CreateCloseButton();

            // 创建完所有子组件后，再关闭面板
            mainPanel.SetActive(false);
        }

        void CreateTitleBar()
        {
            GameObject titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(mainPanel.transform, false);

            // 确保有 RectTransform 组件（添加 UI 组件会自动创建 RectTransform）
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
            // 标题栏背景：浅灰色
            titleBg.color = new Color(0.95f, 0.95f, 0.97f, 1f);

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
            title.text = "设置";
            title.fontSize = 28; // 调大一号 (24 -> 28)
            // 标题文字使用深色，提高在浅色背景上的可读性
            title.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            title.alignment = TextAlignmentOptions.Center;
            ApplyFontToTMP(title);
        }

        void CreateTabBar()
        {
            GameObject tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(mainPanel.transform, false);

            RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
            if (tabBarRect == null)
            {
                tabBarRect = tabBar.AddComponent<RectTransform>();
            }
            tabBarRect.anchorMin = new Vector2(0, 1);
            tabBarRect.anchorMax = new Vector2(1, 1);
            tabBarRect.pivot = new Vector2(0.5f, 1);
            tabBarRect.anchoredPosition = new Vector2(0, -60);
            tabBarRect.sizeDelta = new Vector2(0, 50);

            HorizontalLayoutGroup layout = tabBar.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 5;
            layout.padding = new RectOffset(10, 10, 5, 5);

            // 创建PPT标签按钮
            pptTabButton = CreateTabButton(tabBar.transform, "PPT", 0);
            // 创建模型标签按钮
            modelTabButton = CreateTabButton(tabBar.transform, "模型", 1);
            // 创建设置标签按钮
            settingsTabButton = CreateTabButton(tabBar.transform, "设置", 2);
        }

        Button CreateTabButton(Transform parent, string text, int tabIndex)
        {
            GameObject btnObj = new GameObject($"TabButton_{text}");
            btnObj.transform.SetParent(parent, false);

            Image btnBg = btnObj.AddComponent<Image>();
            // 标签按钮默认背景：与通用按钮一致的蓝色
            btnBg.color = new Color(0.23f, 0.45f, 0.85f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            // 标签按钮颜色：与其它按钮风格统一（蓝底白字）
            colors.normalColor = new Color(0.23f, 0.45f, 0.85f, 1f);
            colors.selectedColor = new Color(0.20f, 0.38f, 0.70f, 1f);
            btn.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null)
            {
                textRect = textObj.AddComponent<RectTransform>();
            }
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text btnText = textObj.AddComponent<TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 20; // 调大一号 (18 -> 20)
            // 标签按钮文字颜色：白色，与其它按钮一致
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;
            ApplyFontToTMP(btnText);

            return btn;
        }

        void CreateContentArea()
        {
            GameObject contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(mainPanel.transform, false);

            RectTransform contentRect = contentArea.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = contentArea.AddComponent<RectTransform>();
            }
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(10, 10);
            contentRect.offsetMax = new Vector2(-10, -120);

            // 创建三个标签页面板
            pptPanel = CreateTabPanel(contentArea.transform, "PPTPanel");
            modelPanel = CreateTabPanel(contentArea.transform, "ModelPanel");
            settingsPanel = CreateTabPanel(contentArea.transform, "SettingsPanel");

            // 创建PPT面板内容
            CreatePPTPanelContent();
            // 创建模型面板内容
            CreateModelPanelContent();
            // 创建设置面板内容
            CreateSettingsPanelContent();
        }

        GameObject CreateTabPanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = panel.AddComponent<RectTransform>();
            }
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.SetActive(false);
            return panel;
        }

        void CreatePPTPanelContent()
        {
            VerticalLayoutGroup layout = pptPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            // 创建标题和添加按钮的容器
            GameObject headerContainer = new GameObject("HeaderContainer");
            headerContainer.transform.SetParent(pptPanel.transform, false);
            RectTransform headerRect = headerContainer.GetComponent<RectTransform>();
            if (headerRect == null)
            {
                headerRect = headerContainer.AddComponent<RectTransform>();
            }
            headerRect.sizeDelta = new Vector2(0, 40);
            
            HorizontalLayoutGroup headerLayout = headerContainer.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = true;
            headerLayout.spacing = 10;
            headerLayout.padding = new RectOffset(0, 0, 0, 0);

            // PPT列表标题
            GameObject pptListLabel = CreateLabel(headerContainer.transform, "PPT列表：", 16);
            RectTransform labelRect = pptListLabel.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(0, 40);
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 0.5f);

            // 添加PPT按钮
            addPPTButton = CreateButton(headerContainer.transform, "添加PPT", new Vector2(120, 40));
            RectTransform btnRect = addPPTButton.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1, 0);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.pivot = new Vector2(1, 0.5f);
            btnRect.anchoredPosition = Vector2.zero;
            addPPTButton.onClick.AddListener(OnAddPPTClicked);

            // 创建PPT列表滚动视图
            GameObject scrollObj = new GameObject("PPTListScrollView");
            scrollObj.transform.SetParent(pptPanel.transform, false);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            if (scrollRect == null)
            {
                scrollRect = scrollObj.AddComponent<RectTransform>();
            }
            scrollRect.sizeDelta = new Vector2(0, 200);

            pptListScrollRect = scrollObj.AddComponent<ScrollRect>();
            pptListScrollRect.horizontal = false;
            pptListScrollRect.vertical = true;

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.96f, 0.96f, 0.98f, 1f);

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
            viewportMask.color = new Color(1f, 1f, 1f, 1f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            pptListScrollRect.viewport = viewportRect;

            // 创建内容
            pptListContent = new GameObject("Content");
            pptListContent.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = pptListContent.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = pptListContent.AddComponent<RectTransform>();
            }
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup contentLayout = pptListContent.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.spacing = 5;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);

            ContentSizeFitter contentFitter = pptListContent.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            pptListScrollRect.content = contentRect;

            // 创建配置和播放按钮容器
            GameObject buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(pptPanel.transform, false);
            RectTransform buttonContainerRect = buttonContainer.GetComponent<RectTransform>();
            if (buttonContainerRect == null)
            {
                buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
            }
            buttonContainerRect.sizeDelta = new Vector2(0, 50);

            HorizontalLayoutGroup buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = true;
            buttonLayout.spacing = 10;
            buttonLayout.padding = new RectOffset(0, 0, 0, 0);
            buttonLayout.childAlignment = TextAnchor.MiddleLeft;

            // 配置按钮
            configButton = CreateButton(buttonContainer.transform, "配置", new Vector2(100, 40));
            configButton.onClick.AddListener(OnConfigClicked);
            configButton.interactable = false; // 默认不可点击，需先选中PPT

            // 播放按钮
            playButton = CreateButton(buttonContainer.transform, "播放", new Vector2(100, 40));
            playButton.onClick.AddListener(OnPlayClicked);
            playButton.interactable = false; // 默认不可点击

            configOverlay = new GameObject("ConfigOverlay");
            configOverlay.transform.SetParent(mainPanel.transform, false);
            RectTransform configOverlayRect = configOverlay.GetComponent<RectTransform>();
            if (configOverlayRect == null)
            {
                configOverlayRect = configOverlay.AddComponent<RectTransform>();
            }
            configOverlayRect.anchorMin = Vector2.zero;
            configOverlayRect.anchorMax = Vector2.one;
            configOverlayRect.offsetMin = Vector2.zero;
            configOverlayRect.offsetMax = Vector2.zero;

            Image overlayBg = configOverlay.AddComponent<Image>();
            overlayBg.color = new Color(0f, 0f, 0f, 0.45f);

            configPanel = new GameObject("ConfigPanel");
            configPanel.transform.SetParent(configOverlay.transform, false);
            RectTransform configPanelRect = configPanel.GetComponent<RectTransform>();
            if (configPanelRect == null)
            {
                configPanelRect = configPanel.AddComponent<RectTransform>();
            }
            configPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            configPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            configPanelRect.pivot = new Vector2(0.5f, 0.5f);
            configPanelRect.anchoredPosition = Vector2.zero;
            configPanelRect.sizeDelta = new Vector2(720, 420);

            Image configPanelBg = configPanel.AddComponent<Image>();
            configPanelBg.color = new Color(0.98f, 0.98f, 0.99f, 1f);

            VerticalLayoutGroup configLayout = configPanel.AddComponent<VerticalLayoutGroup>();
            configLayout.spacing = 10;
            configLayout.padding = new RectOffset(20, 20, 20, 20);
            configLayout.childForceExpandWidth = true;
            configLayout.childControlHeight = false;

            // 演讲稿输入框标签
            GameObject configInputLabel = CreateLabel(configPanel.transform, "演讲稿：", 16);

            // 演讲稿输入框（带滚动）
            GameObject configInputScrollObj = new GameObject("ConfigInputScrollView");
            configInputScrollObj.transform.SetParent(configPanel.transform, false);
            RectTransform configInputScrollRect = configInputScrollObj.GetComponent<RectTransform>();
            if (configInputScrollRect == null)
            {
                configInputScrollRect = configInputScrollObj.AddComponent<RectTransform>();
            }
            configInputScrollRect.sizeDelta = new Vector2(0, 220);

            Image configInputScrollBg = configInputScrollObj.AddComponent<Image>();
            configInputScrollBg.color = new Color(0.97f, 0.97f, 0.99f, 1f);

            ScrollRect configScrollRect = configInputScrollObj.AddComponent<ScrollRect>();
            configScrollRect.horizontal = false;
            configScrollRect.vertical = true;
            configScrollRect.movementType = ScrollRect.MovementType.Clamped;
            configScrollRect.scrollSensitivity = 25f;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(configInputScrollObj.transform, false);
            RectTransform configViewportRect = viewportObj.GetComponent<RectTransform>();
            if (configViewportRect == null)
            {
                configViewportRect = viewportObj.AddComponent<RectTransform>();
            }
            configViewportRect.anchorMin = Vector2.zero;
            configViewportRect.anchorMax = Vector2.one;
            configViewportRect.offsetMin = new Vector2(10, 8);
            configViewportRect.offsetMax = new Vector2(-10, -8);

            Image viewportImg = viewportObj.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0f);
            viewportImg.raycastTarget = false;
            RectMask2D configViewportMask = viewportObj.AddComponent<RectMask2D>();

            configScrollRect.viewport = configViewportRect;

            GameObject configInputObj = new GameObject("ConfigInputField");
            configInputObj.transform.SetParent(viewportObj.transform, false);
            RectTransform configInputRect = configInputObj.GetComponent<RectTransform>();
            if (configInputRect == null)
            {
                configInputRect = configInputObj.AddComponent<RectTransform>();
            }
            configInputRect.anchorMin = new Vector2(0, 1);
            configInputRect.anchorMax = new Vector2(1, 1);
            configInputRect.pivot = new Vector2(0.5f, 1);
            configInputRect.anchoredPosition = Vector2.zero;
            configInputRect.sizeDelta = new Vector2(0, 0);

            ContentSizeFitter inputFitter = configInputObj.AddComponent<ContentSizeFitter>();
            inputFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            inputFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            configScrollRect.content = configInputRect;

            configInputField = configInputObj.AddComponent<TMP_InputField>();
            TMP_Text configTextComp = CreateTextComponent(configInputObj.transform, "");
            TMP_Text configPlaceholderComp = CreateTextComponent(configInputObj.transform, "请输入演讲稿或使用AI生成...");
            configPlaceholderComp.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            configInputField.textComponent = configTextComp;
            configInputField.placeholder = configPlaceholderComp;
            configInputField.textViewport = configViewportRect;
            configInputField.lineType = TMP_InputField.LineType.MultiLineNewline;

            // 生成演讲稿和确认按钮容器
            GameObject configButtonContainer = new GameObject("ConfigButtonContainer");
            configButtonContainer.transform.SetParent(configPanel.transform, false);
            RectTransform configButtonContainerRect = configButtonContainer.GetComponent<RectTransform>();
            if (configButtonContainerRect == null)
            {
                configButtonContainerRect = configButtonContainer.AddComponent<RectTransform>();
            }
            configButtonContainerRect.sizeDelta = new Vector2(0, 40);

            HorizontalLayoutGroup configButtonLayout = configButtonContainer.AddComponent<HorizontalLayoutGroup>();
            configButtonLayout.childForceExpandWidth = false;
            configButtonLayout.childForceExpandHeight = true;
            configButtonLayout.spacing = 10;
            configButtonLayout.padding = new RectOffset(0, 0, 0, 0);
            configButtonLayout.childAlignment = TextAnchor.MiddleLeft;

            // 生成演讲稿按钮
            generateSpeechButton = CreateButton(configButtonContainer.transform, "生成演讲稿", new Vector2(150, 40));
            generateSpeechButton.onClick.AddListener(OnGenerateSpeech);

            // 确认按钮
            confirmConfigButton = CreateButton(configButtonContainer.transform, "确认", new Vector2(100, 40));
            confirmConfigButton.onClick.AddListener(OnConfirmConfig);

            configOverlay.SetActive(false);

            // 加载指示器
            pptLoadingIndicator = new GameObject("LoadingIndicator");
            pptLoadingIndicator.transform.SetParent(pptPanel.transform, false);
            RectTransform loadingRect = pptLoadingIndicator.GetComponent<RectTransform>();
            if (loadingRect == null)
            {
                loadingRect = pptLoadingIndicator.AddComponent<RectTransform>();
            }
            loadingRect.sizeDelta = new Vector2(30, 30);
            Image loadingImg = pptLoadingIndicator.AddComponent<Image>();
            loadingImg.color = new Color(0.23f, 0.45f, 0.85f, 0.6f);
            pptLoadingIndicator.SetActive(false);

            // 加载PPT列表
            RefreshPPTList();
        }

        void CreateModelPanelContent()
        {
            VerticalLayoutGroup layout = modelPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            // 当前模型信息
            GameObject modelInfoLabel = CreateLabel(modelPanel.transform, "当前模型：", 16);
            
            GameObject modelTextObj = new GameObject("CurrentModelText");
            modelTextObj.transform.SetParent(modelPanel.transform, false);
            RectTransform modelTextRect = modelTextObj.GetComponent<RectTransform>();
            if (modelTextRect == null)
            {
                modelTextRect = modelTextObj.AddComponent<RectTransform>();
            }
            modelTextRect.sizeDelta = new Vector2(0, 30);
            currentModelText = modelTextObj.AddComponent<TextMeshProUGUI>();
            currentModelText.text = "加载中...";
            currentModelText.fontSize = 16; // 调大一号 (14 -> 16)
            currentModelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            currentModelText.alignment = TextAlignmentOptions.Left;
            ApplyFontToTMP(currentModelText);

            // 更改模型按钮
            changeModelButton = CreateButton(modelPanel.transform, "更改模型", new Vector2(200, 40));

            // 重置模型按钮
            resetModelButton = CreateButton(modelPanel.transform, "重置为默认模型", new Vector2(200, 40));
        }

        void CreateSettingsPanelContent()
        {
            VerticalLayoutGroup layout = settingsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            // 更新日志标题
            GameObject changelogLabel = CreateLabel(settingsPanel.transform, "更新日志：", 16);

            // 更新日志滚动视图
            GameObject scrollObj = new GameObject("ChangelogScrollView");
            scrollObj.transform.SetParent(settingsPanel.transform, false);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            if (scrollRect == null)
            {
                scrollRect = scrollObj.AddComponent<RectTransform>();
            }
            scrollRect.sizeDelta = new Vector2(0, 400);

            changelogScrollRect = scrollObj.AddComponent<ScrollRect>();
            changelogScrollRect.horizontal = false;
            changelogScrollRect.vertical = true;

            Image scrollBg = scrollObj.AddComponent<Image>();
            // 更新日志滚动区域背景：浅灰
            scrollBg.color = new Color(0.96f, 0.96f, 0.98f, 1f);

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
            viewportMask.color = new Color(1f, 1f, 1f, 1f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            changelogScrollRect.viewport = viewportRect;

            // 创建内容
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = content.AddComponent<RectTransform>();
            }
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.padding = new RectOffset(10, 10, 10, 10);

            changelogScrollRect.content = contentRect;

            // 更新日志文本
            GameObject changelogTextObj = new GameObject("ChangelogText");
            changelogTextObj.transform.SetParent(content.transform, false);
            RectTransform changelogTextRect = changelogTextObj.GetComponent<RectTransform>();
            if (changelogTextRect == null)
            {
                changelogTextRect = changelogTextObj.AddComponent<RectTransform>();
            }
            changelogTextRect.sizeDelta = new Vector2(0, 0);
            changelogText = changelogTextObj.AddComponent<TextMeshProUGUI>();
            changelogText.text = "加载中...";
            changelogText.fontSize = 16; // 调大一号 (14 -> 16)
            changelogText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            changelogText.alignment = TextAlignmentOptions.TopLeft;
            ApplyFontToTMP(changelogText);

            ContentSizeFitter fitter = changelogTextObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 退出按钮
            exitButton = CreateButton(settingsPanel.transform, "退出程序", new Vector2(200, 40));
            Image exitBtnImg = exitButton.GetComponent<Image>();
            // 退出按钮：鲜明的红色强调
            exitBtnImg.color = new Color(0.91f, 0.26f, 0.26f, 1f);
        }

        void CreateCloseButton()
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(mainPanel.transform, false);

            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            if (closeRect == null)
            {
                closeRect = closeBtnObj.AddComponent<RectTransform>();
            }
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-10, -10);
            closeRect.sizeDelta = new Vector2(40, 40);

            Image closeBg = closeBtnObj.AddComponent<Image>();
            // 关闭按钮背景：柔和红色
            closeBg.color = new Color(0.95f, 0.35f, 0.35f, 1f);

            closeButton = closeBtnObj.AddComponent<Button>();

            GameObject closeText = new GameObject("Text");
            closeText.transform.SetParent(closeBtnObj.transform, false);
            RectTransform closeTextRect = closeText.GetComponent<RectTransform>();
            if (closeTextRect == null)
            {
                closeTextRect = closeText.AddComponent<RectTransform>();
            }
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            TMP_Text text = closeText.AddComponent<TextMeshProUGUI>();
            text.text = "×";
            text.fontSize = 36; // 调大一号 (30 -> 36)
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            ApplyFontToTMP(text);
        }

        GameObject CreateLabel(Transform parent, string text, float fontSize)
        {
            GameObject label = new GameObject($"Label_{text}");
            label.transform.SetParent(parent, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = label.AddComponent<RectTransform>();
            }
            fontSize += 2; // 调大一号
            rect.sizeDelta = new Vector2(0, fontSize + 10);
            TMP_Text labelText = label.AddComponent<TextMeshProUGUI>();
            labelText.text = text;
            labelText.fontSize = fontSize;
            // 标签文字：深色
            labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            ApplyFontToTMP(labelText);
            return label;
        }

        Button CreateButton(Transform parent, string text, Vector2 size)
        {
            GameObject btnObj = new GameObject($"Button_{text}");
            btnObj.transform.SetParent(parent, false);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            if (btnRect == null)
            {
                btnRect = btnObj.AddComponent<RectTransform>();
            }
            btnRect.sizeDelta = size;

            LayoutElement btnLayoutElement = btnObj.AddComponent<LayoutElement>();
            btnLayoutElement.minWidth = size.x;
            btnLayoutElement.minHeight = size.y;
            btnLayoutElement.preferredWidth = size.x;
            btnLayoutElement.preferredHeight = size.y;

            Image btnBg = btnObj.AddComponent<Image>();
            // 通用按钮背景：蓝色主色
            btnBg.color = new Color(0.23f, 0.45f, 0.85f, 1f);

            Button btn = btnObj.AddComponent<Button>();

            GameObject btnText = new GameObject("Text");
            btnText.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
            if (btnTextRect == null)
            {
                btnTextRect = btnText.AddComponent<RectTransform>();
            }
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TMP_Text textComp = btnText.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 18; // 调大一号 (16 -> 18)
            textComp.color = Color.white;
            textComp.alignment = TextAlignmentOptions.Center;
            ApplyFontToTMP(textComp);

            return btn;
        }

        TMP_Text CreateTextComponent(Transform parent, string text)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parent, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null)
            {
                textRect = textObj.AddComponent<RectTransform>();
            }
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            TMP_Text textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 16; // 调大一号 (14 -> 16)
            // 输入内容文字：深色
            textComp.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            textComp.alignment = TextAlignmentOptions.TopLeft;
            ApplyFontToTMP(textComp);

            return textComp;
        }

        public void ShowTab(int tabIndex)
        {
            currentTabIndex = tabIndex;

            // 如果面板引用丢失，尝试重新初始化
            if (pptPanel == null || modelPanel == null || settingsPanel == null)
            {
                ReinitializePanelReferences();
            }

            // 隐藏所有面板
            if (pptPanel != null) 
            {
                pptPanel.SetActive(false);
            }
            else
            {
                Debug.LogWarning("ShowTab: pptPanel 为 null");
            }
            
            if (modelPanel != null) 
            {
                modelPanel.SetActive(false);
            }
            else
            {
                Debug.LogWarning("ShowTab: modelPanel 为 null");
            }
            
            if (settingsPanel != null) 
            {
                settingsPanel.SetActive(false);
            }
            else
            {
                Debug.LogWarning("ShowTab: settingsPanel 为 null");
            }

            // 重置所有按钮颜色
            ResetTabButtonColors();

            // 显示选中的面板
            switch (tabIndex)
            {
                case 0: // PPT
                    if (pptPanel != null)
                    {
                        pptPanel.SetActive(true);
                        Debug.Log("显示PPT面板");
                    }
                    else
                    {
                        Debug.LogError("无法显示PPT面板：pptPanel 为 null");
                    }
                    if (pptTabButton != null)
                    {
                        Image img = pptTabButton.GetComponent<Image>();
                        // 选中 Tab：略深一点的蓝色
                        if (img != null) img.color = new Color(0.20f, 0.38f, 0.70f, 1f);
                    }
                    break;
                case 1: // Model
                    if (modelPanel != null)
                    {
                        modelPanel.SetActive(true);
                        Debug.Log("显示模型面板");
                    }
                    else
                    {
                        Debug.LogError("无法显示模型面板：modelPanel 为 null");
                    }
                    if (modelTabButton != null)
                    {
                        Image img = modelTabButton.GetComponent<Image>();
                        if (img != null) img.color = new Color(0.20f, 0.38f, 0.70f, 1f);
                    }
                    UpdateModelInfo();
                    break;
                case 2: // Settings
                    if (settingsPanel != null)
                    {
                        settingsPanel.SetActive(true);
                        Debug.Log("显示设置面板");
                    }
                    else
                    {
                        Debug.LogError("无法显示设置面板：settingsPanel 为 null");
                    }
                    if (settingsTabButton != null)
                    {
                        Image img = settingsTabButton.GetComponent<Image>();
                        if (img != null) img.color = new Color(0.20f, 0.38f, 0.70f, 1f);
                    }
                    break;
            }
        }

        void ResetTabButtonColors()
        {
            if (pptTabButton != null)
            {
                Image img = pptTabButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.23f, 0.45f, 0.85f, 1f);
            }
            if (modelTabButton != null)
            {
                Image img = modelTabButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.23f, 0.45f, 0.85f, 1f);
            }
            if (settingsTabButton != null)
            {
                Image img = settingsTabButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.23f, 0.45f, 0.85f, 1f);
            }
        }

        // 旧的OnGenerateSpeech和WaitForSpeechGeneration方法已移除，新版本在文件末尾

        void OnChangeModel()
        {
            if (vrmLoader == null)
            {
                Debug.LogWarning("VRMLoader组件未找到");
                return;
            }

            // 注意：根据VRMLoader的实现，LoadVRM方法已被禁用
            // 这里可以显示一个提示
            Debug.Log("模型更改功能：当前版本仅支持默认模型");
            if (currentModelText != null)
                currentModelText.text = "提示：当前版本仅支持默认模型";
        }

        void OnResetModel()
        {
            if (vrmLoader == null)
            {
                Debug.LogWarning("VRMLoader组件未找到");
                return;
            }

            vrmLoader.ResetModel();
            UpdateModelInfo();
        }

        void UpdateModelInfo()
        {
            if (currentModelText == null) return;

            if (vrmLoader == null)
            {
                currentModelText.text = "VRMLoader未找到";
                return;
            }

            // 尝试获取当前模型名称
            Transform modelRoot = GameObject.Find("Model")?.transform;
            if (modelRoot != null)
            {
                for (int i = 0; i < modelRoot.childCount; i++)
                {
                    var child = modelRoot.GetChild(i).gameObject;
                    if (child.activeInHierarchy)
                    {
                        currentModelText.text = $"当前模型：{child.name}";
                        return;
                    }
                }
            }

            currentModelText.text = "当前模型：默认模型";
        }

        void LoadChangelog()
        {
            if (changelogText == null) return;

            string changelogPath = Path.Combine(Application.dataPath, "..", "version.md");
            if (File.Exists(changelogPath))
            {
                try
                {
                    string content = File.ReadAllText(changelogPath);
                    changelogText.text = content;
                }
                catch (System.Exception e)
                {
                    changelogText.text = $"加载更新日志失败：{e.Message}";
                }
            }
            else
            {
                changelogText.text = "更新日志文件未找到";
            }
        }

        void OnExit()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }

        public void OpenPanel()
        {
            // 确保 EventSystem 存在
            EnsureEventSystem();
            
            // 如果面板不存在，先初始化
            if (mainPanel == null)
            {
                InitializeComponents();
            }

            if (mainPanel != null)
            {
                // 如果面板还没有创建子组件，先创建它们
                if (mainPanel.transform.childCount == 0)
                {
                    Debug.Log("设置面板子组件未创建，开始创建...");
                    // 临时激活面板以确保子组件正确创建
                    bool wasActive = mainPanel.activeSelf;
                    mainPanel.SetActive(true);
                    
                    CreateTitleBar();
                    CreateTabBar();
                    CreateContentArea();
                    CreateCloseButton();
                    
                    // 设置UI事件监听
                    SetupUI();
                    LoadChangelog();
                    
                    // 应用字体到所有 TextMeshPro 组件
                    ApplyFontToAllTMP();
                    
                    Debug.Log($"设置面板子组件创建完成，子对象数量: {mainPanel.transform.childCount}");
                    
                    // 恢复原来的状态
                    if (!wasActive)
                        mainPanel.SetActive(false);
                }
                else
                {
                    // 子组件已存在，但需要确保引用正确
                    // 尝试重新查找面板引用（如果丢失）
                    if (pptPanel == null || modelPanel == null || settingsPanel == null)
                    {
                        Debug.Log("面板引用丢失，尝试重新查找...");
                        ReinitializePanelReferences();
                    }
                    
                    // 确保UI事件监听已设置
                    SetupUI();
                    
                    // 应用字体到所有 TextMeshPro 组件
                    ApplyFontToAllTMP();
                }

                // 确保Canvas存在且正确设置（使用与数字人相同的Canvas）
                if (parentCanvas == null)
                {
                    GameObject canvasObj = new GameObject("SettingsPanelCanvas");
                    parentCanvas = canvasObj.AddComponent<Canvas>();
                    parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    // 不设置过高的sortingOrder，保持与数字人同一层级
                    
                    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    
                    // 确保有 GraphicRaycaster
                    if (canvasObj.GetComponent<GraphicRaycaster>() == null)
                    {
                        canvasObj.AddComponent<GraphicRaycaster>();
                    }
                }
                
                // 确保 Canvas 有 GraphicRaycaster
                if (parentCanvas != null && parentCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }
                
                // 确保排序层级高于数字人，避免被3D对象截获点击
                parentCanvas.overrideSorting = true;
                parentCanvas.sortingOrder = canvasSortingOrder;
                
                // 确保面板是Canvas的子对象
                if (mainPanel.transform.parent != parentCanvas.transform)
                {
                    mainPanel.transform.SetParent(parentCanvas.transform, false);
                }
                
                // 确保所有按钮和可交互元素可以接收点击
                EnsureUIInteractable();
                
                // 确保主面板具备阻塞射线的组件
                CanvasGroup cg = mainPanel.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = mainPanel.AddComponent<CanvasGroup>();
                }
                cg.blocksRaycasts = true;
                cg.interactable = true;
                
                Image bgImage = mainPanel.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.raycastTarget = true;
                }

                // 显示面板
                mainPanel.SetActive(true);
                
                // 确保面板的RectTransform设置正确
                RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
                if (panelRect != null)
                {
                    panelRect.anchoredPosition = Vector2.zero;
                    panelRect.localScale = Vector3.one;
                }
                
                // 确保面板显示在最上层（在Canvas内的层级）
                mainPanel.transform.SetAsLastSibling();
                
                // 确保Canvas激活（但不修改sortingOrder，保持与数字人同一层级）
                if (parentCanvas != null)
                {
                    parentCanvas.gameObject.SetActive(true);
                    Debug.Log($"Canvas已激活: {parentCanvas.name}, sortingOrder: {parentCanvas.sortingOrder}");
                }
                
                // 确保默认标签页显示
                ShowTab(0);
                
                // 验证面板状态
                ValidatePanelState();
                
                // 调试信息
                Debug.Log($"设置面板已打开 - PPT面板: {(pptPanel != null ? "存在" : "null")}, " +
                         $"模型面板: {(modelPanel != null ? "存在" : "null")}, " +
                         $"设置面板: {(settingsPanel != null ? "存在" : "null")}");
            }
            else
            {
                Debug.LogError("无法打开设置面板：mainPanel 为 null");
            }
        }
        
        void ReinitializePanelReferences()
        {
            // 查找ContentArea
            Transform contentArea = mainPanel.transform.Find("ContentArea");
            if (contentArea != null)
            {
                // 重新查找各个面板
                if (pptPanel == null)
                    pptPanel = contentArea.Find("PPTPanel")?.gameObject;
                if (modelPanel == null)
                    modelPanel = contentArea.Find("ModelPanel")?.gameObject;
                if (settingsPanel == null)
                    settingsPanel = contentArea.Find("SettingsPanel")?.gameObject;
                
                // 重新查找标签按钮
                Transform tabBar = mainPanel.transform.Find("TabBar");
                if (tabBar != null)
                {
                    if (pptTabButton == null)
                        pptTabButton = tabBar.Find("TabButton_PPT")?.GetComponent<Button>();
                    if (modelTabButton == null)
                        modelTabButton = tabBar.Find("TabButton_模型")?.GetComponent<Button>();
                    if (settingsTabButton == null)
                        settingsTabButton = tabBar.Find("TabButton_设置")?.GetComponent<Button>();
                }
                
                // 重新查找关闭按钮
                if (closeButton == null)
                    closeButton = mainPanel.transform.Find("CloseButton")?.GetComponent<Button>();
            }
        }
        
        void ValidatePanelState()
        {
            if (mainPanel == null)
            {
                Debug.LogError("ValidatePanelState: mainPanel 为 null");
                return;
            }
            
            Debug.Log($"面板验证 - 主面板激活: {mainPanel.activeSelf}, " +
                     $"子对象数量: {mainPanel.transform.childCount}, " +
                     $"Canvas: {(parentCanvas != null ? parentCanvas.name : "null")}");
            
            // 检查各个子组件
            Transform titleBar = mainPanel.transform.Find("TitleBar");
            Transform tabBar = mainPanel.transform.Find("TabBar");
            Transform contentArea = mainPanel.transform.Find("ContentArea");
            Transform closeBtn = mainPanel.transform.Find("CloseButton");
            
            Debug.Log($"子组件检查 - 标题栏: {(titleBar != null ? "存在" : "缺失")}, " +
                     $"标签栏: {(tabBar != null ? "存在" : "缺失")}, " +
                     $"内容区域: {(contentArea != null ? "存在" : "缺失")}, " +
                     $"关闭按钮: {(closeBtn != null ? "存在" : "缺失")}");
            
            if (contentArea != null)
            {
                Debug.Log($"内容区域子对象数量: {contentArea.childCount}");
                for (int i = 0; i < contentArea.childCount; i++)
                {
                    Transform child = contentArea.GetChild(i);
                    Debug.Log($"  - {child.name}: 激活={child.gameObject.activeSelf}");
                }
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
                    // 确保在最上层并禁止场景交互
                    mainPanel.transform.SetAsLastSibling();
                }
            }
            
            // 如果面板显示，确保它在最上层
            if (mainPanel != null && mainPanel.activeSelf)
            {
                mainPanel.transform.SetAsLastSibling();
            }
        }

        public bool IsPanelOpen()
        {
            return mainPanel != null && mainPanel.activeSelf;
        }

        // ========== PPT列表相关方法 ==========

        /// <summary>
        /// 刷新PPT列表
        /// </summary>
        void RefreshPPTList()
        {
            if (pptListContent == null) return;

            // 使用协程来延迟UI操作，避免在渲染过程中修改UI
            StartCoroutine(RefreshPPTListCoroutine());
        }

        /// <summary>
        /// 刷新PPT列表协程
        /// </summary>
        IEnumerator RefreshPPTListCoroutine()
        {
            // 等待一帧，确保不在渲染过程中
            yield return null;

            // 清除现有列表项
            foreach (var item in pptListItems)
            {
                if (item.itemObj != null)
                {
                    Destroy(item.itemObj);
                }
            }
            pptListItems.Clear();
            selectedPPTItem = null;
            if (configButton != null) configButton.interactable = false;
            UpdateButtonStates();

            // 再等待一帧，确保销毁完成
            yield return null;

            // 加载所有PPT信息
            List<string> jsonFiles = PPTDataManager.GetAllPPTInfoJsonFiles();
            foreach (string jsonFile in jsonFiles)
            {
                PPTInfo pptInfo = PPTDataManager.LoadPPTInfoFromJson(Path.GetFileName(jsonFile));
                if (pptInfo != null)
                {
                    CreatePPTListItem(pptInfo);
                }
            }

            // 等待一帧，确保创建完成
            yield return null;

            // 更新按钮状态
            UpdateButtonStates();
        }

        /// <summary>
        /// 创建PPT列表项
        /// </summary>
        void CreatePPTListItem(PPTInfo pptInfo)
        {
            if (pptListContent == null) return;

            GameObject itemObj = new GameObject($"PPTItem_{pptInfo.filename}");
            itemObj.transform.SetParent(pptListContent.transform, false);
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            if (itemRect == null)
            {
                itemRect = itemObj.AddComponent<RectTransform>();
            }
            itemRect.sizeDelta = new Vector2(0, 40);

            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.98f, 0.98f, 1f, 1f);

            Button itemButton = itemObj.AddComponent<Button>();
            ColorBlock colors = itemButton.colors;
            colors.normalColor = new Color(0.98f, 0.98f, 1f, 1f);
            colors.selectedColor = new Color(0.85f, 0.90f, 1f, 1f);
            colors.highlightedColor = new Color(0.90f, 0.93f, 1f, 1f);
            itemButton.colors = colors;

            HorizontalLayoutGroup itemLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
            itemLayout.childForceExpandWidth = true;
            itemLayout.childForceExpandHeight = true;
            itemLayout.spacing = 10;
            itemLayout.padding = new RectOffset(10, 10, 5, 5);

            // 文件名
            GameObject fileNameObj = new GameObject("FileName");
            fileNameObj.transform.SetParent(itemObj.transform, false);

            RectTransform fileNameRect = fileNameObj.GetComponent<RectTransform>();
            if (fileNameRect == null)
            {
                fileNameRect = fileNameObj.AddComponent<RectTransform>();
            }
            TMP_Text fileNameText = fileNameObj.AddComponent<TextMeshProUGUI>();
            fileNameText.text = pptInfo.filename;
            fileNameText.fontSize = 16;
            fileNameText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            fileNameText.alignment = TextAlignmentOptions.Left;
            ApplyFontToTMP(fileNameText);

            // 页数
            GameObject pageCountObj = new GameObject("PageCount");
            pageCountObj.transform.SetParent(itemObj.transform, false);
            RectTransform pageCountRect = pageCountObj.GetComponent<RectTransform>();
            if (pageCountRect == null)
            {
                pageCountRect = pageCountObj.AddComponent<RectTransform>();
            }
            pageCountRect.sizeDelta = new Vector2(80, 0);
            TMP_Text pageCountText = pageCountObj.AddComponent<TextMeshProUGUI>();
            pageCountText.text = $"页数: {pptInfo.pageCount}";
            pageCountText.fontSize = 14;
            pageCountText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            pageCountText.alignment = TextAlignmentOptions.Center;
            ApplyFontToTMP(pageCountText);

            // 配置状态
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(itemObj.transform, false);
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            if (statusRect == null)
            {
                statusRect = statusObj.AddComponent<RectTransform>();
            }
            statusRect.sizeDelta = new Vector2(100, 0);
            TMP_Text statusText = statusObj.AddComponent<TextMeshProUGUI>();
            string statusStr = GetStatusString(pptInfo.configStatus);
            statusText.text = statusStr;
            statusText.fontSize = 14;
            statusText.color = GetStatusColor(pptInfo.configStatus);
            statusText.alignment = TextAlignmentOptions.Center;
            ApplyFontToTMP(statusText);

            // 创建列表项数据
            PPTListItem listItem = new PPTListItem
            {
                itemObj = itemObj,
                pptInfo = pptInfo,
                itemButton = itemButton,
                fileNameText = fileNameText,
                pageCountText = pageCountText,
                statusText = statusText
            };

            // 添加点击事件
            itemButton.onClick.AddListener(() => OnPPTItemSelected(listItem));

            pptListItems.Add(listItem);
        }

        /// <summary>
        /// 获取状态字符串
        /// </summary>
        string GetStatusString(int status)
        {
            switch (status)
            {
                case 0: return "未配置";
                case 1: return "进行中";
                case 2: return "已配置";
                default: return "未知";
            }
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        Color GetStatusColor(int status)
        {
            switch (status)
            {
                case 0: return new Color(0.7f, 0.7f, 0.7f, 1f); // 灰色
                case 1: return new Color(1f, 0.65f, 0f, 1f); // 橙色
                case 2: return new Color(0f, 0.7f, 0f, 1f); // 绿色
                default: return Color.black;
            }
        }

        /// <summary>
        /// PPT项被选中
        /// </summary>
        void OnPPTItemSelected(PPTListItem item)
        {
            // 取消之前选中项的高亮
            if (selectedPPTItem != null && selectedPPTItem.itemButton != null)
            {
                Image prevImg = selectedPPTItem.itemButton.GetComponent<Image>();
                if (prevImg != null)
                {
                    prevImg.color = new Color(0.98f, 0.98f, 1f, 1f);
                }
            }

            // 设置当前选中项
            selectedPPTItem = item;

            // 高亮当前选中项
            if (item.itemButton != null)
            {
                Image img = item.itemButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(0.85f, 0.90f, 1f, 1f);
                }
            }

            if (configButton != null) configButton.interactable = (selectedPPTItem != null);

            // 更新播放按钮状态
            UpdateButtonStates();
        }

        void ClearPPTSelection()
        {
            if (selectedPPTItem != null && selectedPPTItem.itemButton != null)
            {
                Image prevImg = selectedPPTItem.itemButton.GetComponent<Image>();
                if (prevImg != null)
                {
                    prevImg.color = new Color(0.98f, 0.98f, 1f, 1f);
                }
            }

            selectedPPTItem = null;
            UpdateButtonStates();
        }

        void OnMainPanelClicked(PointerEventData eventData)
        {
            if (selectedPPTItem == null) return;
            if (configOverlay != null && configOverlay.activeSelf) return;

            if (eventData == null) return;
            if (IsPointerOverPPTListItem(eventData)) return;

            ClearPPTSelection();
        }

        bool IsPointerOverPPTListItem(PointerEventData eventData)
        {
            if (pptListContent == null) return false;
            if (EventSystem.current == null) return false;

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++)
            {
                Transform t = results[i].gameObject != null ? results[i].gameObject.transform : null;
                if (t == null) continue;
                if (t == pptListContent.transform) return true;
                if (t.IsChildOf(pptListContent.transform)) return true;
            }

            return false;
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        void UpdateButtonStates()
        {
            if (playButton == null) return;

            if (configButton != null) configButton.interactable = (selectedPPTItem != null);

            // 如果选中的PPT未配置或配置中，播放按钮不可点击
            if (selectedPPTItem == null || selectedPPTItem.pptInfo.configStatus != 2)
            {
                playButton.interactable = false;
            }
            else
            {
                playButton.interactable = true;
            }
        }

        /// <summary>
        /// 添加PPT按钮点击
        /// </summary>
        void OnAddPPTClicked()
        {
            StartCoroutine(OpenFileDialogCoroutine());
        }

        /// <summary>
        /// 打开文件对话框协程
        /// </summary>
        IEnumerator OpenFileDialogCoroutine()
        {
            // 确保Unity窗口在前台
            IntPtr hWnd = GetActiveWindow();
            if (hWnd != IntPtr.Zero)
            {
                SetForegroundWindow(hWnd);
            }

            // 等待渲染完成
            yield return new WaitForEndOfFrame();
            yield return null; // 额外等待一帧，确保渲染完全完成

            OpenFileName ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.dlgOwner = hWnd;
            ofn.filter = "PowerPoint Files\0*.ppt;*.pptx\0All Files\0*.*\0\0";
            ofn.file = new string(new char[256]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[64]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = "选择PPT文件";
            ofn.initialDir = Application.streamingAssetsPath.Replace('/', '\\');
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00040000;

            if (LocalDialog.GetOpenFileName(ofn))
            {
                string selectedFilePath = ofn.file;
                string newFileName = Path.GetFileName(selectedFilePath);
                string newFilePath = selectedFilePath;

                // 创建新的PPTInfo
                PPTInfo newInfo = new PPTInfo();
                newInfo.filename = newFileName;
                newInfo.file_path = newFilePath;
                newInfo.desc = new string[] { "" };
                newInfo.is_uploaded = false;
                newInfo.pageCount = 0; // 暂时设为0，后续可以通过COM接口获取
                newInfo.configStatus = 0; // 未配置

                // 保存到JSON
                PPTDataManager.SavePPTInfoToJson(newInfo, Path.ChangeExtension(newFileName, ".json"));

                // 等待一帧后再刷新列表，避免在渲染过程中修改UI
                yield return null;
                RefreshPPTList();
            }

            // 文件选择完成后再次确保Unity窗口在前台
            if (hWnd != IntPtr.Zero)
            {
                SetForegroundWindow(hWnd);
            }
        }

        /// <summary>
        /// 配置按钮点击
        /// </summary>
        void OnConfigClicked()
        {
            if (selectedPPTItem == null)
            {
                return;
            }

            // 显示配置面板
            if (configOverlay != null)
            {
                configOverlay.SetActive(true);

                // 加载当前PPT的演讲稿
                if (configInputField != null && selectedPPTItem.pptInfo.desc != null && selectedPPTItem.pptInfo.desc.Length > 0)
                {
                    configInputField.text = string.Join("\n", selectedPPTItem.pptInfo.desc);
                }
                else if (configInputField != null)
                {
                    configInputField.text = "";
                }
            }
        }

        /// <summary>
        /// 播放按钮点击
        /// </summary>
        void OnPlayClicked()
        {
            if (selectedPPTItem == null || selectedPPTItem.pptInfo.configStatus != 2)
            {
                return;
            }

            if (pptController == null)
            {
                pptController = FindObjectOfType<PPTController>();
            }

            if (pptController == null)
            {
                Debug.LogWarning("PPTController 未找到，无法播放PPT");
                return;
            }

            if (!string.IsNullOrEmpty(selectedPPTItem.pptInfo.file_path))
            {
                pptController.SetPPTFile(selectedPPTItem.pptInfo.file_path);
            }

            pptController.OpenPPT();
        }

        /// <summary>
        /// 确认配置按钮点击
        /// </summary>
        void OnConfirmConfig()
        {
            if (selectedPPTItem == null)
            {
                return;
            }

            // 使用协程来延迟UI更新，避免在渲染过程中修改UI
            StartCoroutine(OnConfirmConfigCoroutine());
        }

        /// <summary>
        /// 确认配置协程
        /// </summary>
        IEnumerator OnConfirmConfigCoroutine()
        {
            // 等待一帧，确保不在渲染过程中
            yield return null;

            // 保存演讲稿
            if (configInputField != null)
            {
                string[] descLines = configInputField.text.Split('\n');
                selectedPPTItem.pptInfo.desc = descLines;
                selectedPPTItem.pptInfo.configStatus = 2; // 已配置

                // 保存到JSON
                string jsonName = Path.ChangeExtension(selectedPPTItem.pptInfo.filename, ".json");
                PPTDataManager.SavePPTInfoToJson(selectedPPTItem.pptInfo, jsonName);

                // 再等待一帧后更新UI
                yield return null;

                // 更新状态显示
                if (selectedPPTItem.statusText != null)
                {
                    selectedPPTItem.statusText.text = GetStatusString(selectedPPTItem.pptInfo.configStatus);
                    selectedPPTItem.statusText.color = GetStatusColor(selectedPPTItem.pptInfo.configStatus);
                }

                // 更新按钮状态
                UpdateButtonStates();

                // 隐藏配置面板
                if (configOverlay != null)
                {
                    configOverlay.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 修改生成演讲稿方法，使其使用选中的PPT
        /// </summary>
        void OnGenerateSpeech()
        {
            if (selectedPPTItem == null)
            {
                return;
            }

            if (autoDesc == null)
            {
                Debug.LogWarning("AutoDesc组件未找到，无法生成演讲稿");
                return;
            }

            // 设置配置状态为进行中
            selectedPPTItem.pptInfo.configStatus = 1;
            if (selectedPPTItem.statusText != null)
            {
                selectedPPTItem.statusText.text = GetStatusString(selectedPPTItem.pptInfo.configStatus);
                selectedPPTItem.statusText.color = GetStatusColor(selectedPPTItem.pptInfo.configStatus);
            }
            UpdateButtonStates();

            if (pptLoadingIndicator != null)
                pptLoadingIndicator.SetActive(true);

            // 同步PPT选择到AutoDesc（如果AutoDesc使用dropdown）
            if (autoDesc != null)
            {
                // 使用反射设置AutoDesc的dropdown（如果存在）
                var dropdownField = autoDesc.GetType().GetField("dropdown", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dropdownField != null)
                {
                    var autoDescDropdown = dropdownField.GetValue(autoDesc) as DropdownManager;
                    if (autoDescDropdown != null && autoDescDropdown.dropdown != null)
                    {
                        // 同步当前选择的PPT
                        autoDescDropdown.SetCurrentOptionText(selectedPPTItem.pptInfo.filename);
                    }
                }
            }

            // 调用AutoDesc的生成方法
            autoDesc.StartGetDescProcess();

            // 等待生成完成
            StartCoroutine(WaitForSpeechGeneration());
        }

        /// <summary>
        /// 等待演讲稿生成完成
        /// </summary>
        IEnumerator WaitForSpeechGeneration()
        {
            yield return new WaitForSeconds(0.5f);

            // 尝试获取生成的演讲稿
            if (autoDesc != null && configInputField != null)
            {
                // 使用反射获取AutoDesc的inputField
                var inputField = autoDesc.GetType().GetField("inputField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (inputField != null)
                {
                    var autoDescInput = inputField.GetValue(autoDesc) as InputField;
                    if (autoDescInput != null)
                    {
                        // 定期检查并同步演讲稿内容
                        float timeout = 30f;
                        float elapsed = 0f;
                        while (elapsed < timeout)
                        {
                            if (!string.IsNullOrEmpty(autoDescInput.text))
                            {
                                // 等待一帧后再更新UI，避免在渲染过程中修改
                                yield return null;
                                configInputField.text = autoDescInput.text;
                                break;
                            }
                            yield return new WaitForSeconds(0.5f);
                            elapsed += 0.5f;
                        }
                    }
                }
            }

            // 等待一帧，确保不在渲染过程中更新UI
            yield return null;

            // 更新配置状态为已配置
            if (selectedPPTItem != null)
            {
                selectedPPTItem.pptInfo.configStatus = 2;
                if (selectedPPTItem.statusText != null)
                {
                    selectedPPTItem.statusText.text = GetStatusString(selectedPPTItem.pptInfo.configStatus);
                    selectedPPTItem.statusText.color = GetStatusColor(selectedPPTItem.pptInfo.configStatus);
                }
                UpdateButtonStates();
            }

            if (pptLoadingIndicator != null)
                pptLoadingIndicator.SetActive(false);
        }
    }
}

