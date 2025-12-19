using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;
using System.Collections;

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
        public TMP_Text pptStatusText;
        public GameObject pptLoadingIndicator;

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

            // 设置PPT面板按钮
            if (generateSpeechButton != null)
                generateSpeechButton.onClick.AddListener(OnGenerateSpeech);

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

            // PPT选择下拉框
            GameObject pptSelectLabel = CreateLabel(pptPanel.transform, "PPT选择：", 16);
            
            // 创建PPT下拉框
            GameObject dropdownObj = new GameObject("PPTDropdown");
            dropdownObj.transform.SetParent(pptPanel.transform, false);
            RectTransform dropdownRect = dropdownObj.GetComponent<RectTransform>();
            if (dropdownRect == null)
            {
                dropdownRect = dropdownObj.AddComponent<RectTransform>();
            }
            dropdownRect.sizeDelta = new Vector2(0, 40);

            Image dropdownBg = dropdownObj.AddComponent<Image>();
            // 下拉框背景：浅灰
            dropdownBg.color = new Color(0.96f, 0.96f, 0.98f, 1f);

            pptDropdownTMP = dropdownObj.AddComponent<TMP_Dropdown>();
            
            // 创建标签
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            if (labelRect == null)
            {
                labelRect = labelObj.AddComponent<RectTransform>();
            }
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 6);
            labelRect.offsetMax = new Vector2(-25, -7);
            TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "选择PPT";
            labelText.fontSize = 16; // 调大一号 (14 -> 16)
            labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            ApplyFontToTMP(labelText);
            pptDropdownTMP.captionText = labelText;

            // 创建模板
            GameObject templateObj = new GameObject("Template");
            templateObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform templateRect = templateObj.GetComponent<RectTransform>();
            if (templateRect == null)
            {
                templateRect = templateObj.AddComponent<RectTransform>();
            }
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 2);
            templateRect.sizeDelta = new Vector2(0, 150);
            templateObj.SetActive(false);
            Image templateBg = templateObj.AddComponent<Image>();
            // 下拉框展开区域背景
            templateBg.color = new Color(1f, 1f, 1f, 1f);
            ScrollRect templateScroll = templateObj.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;
            templateScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            // 创建视口
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            if (viewportRect == null)
            {
                viewportRect = viewportObj.AddComponent<RectTransform>();
            }
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportMask = viewportObj.AddComponent<Image>();
            viewportMask.color = new Color(1f, 1f, 1f, 1f);
            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            templateScroll.viewport = viewportRect;

            // 创建内容
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = contentObj.AddComponent<RectTransform>();
            }
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            templateScroll.content = contentRect;

            // 创建项目模板
            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            if (itemRect == null)
            {
                itemRect = itemObj.AddComponent<RectTransform>();
            }
            itemRect.sizeDelta = new Vector2(0, 30);
            Toggle itemToggle = itemObj.AddComponent<Toggle>();
            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.96f, 0.96f, 0.98f, 1f);
            itemToggle.targetGraphic = itemBg;

            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
            if (itemLabelRect == null)
            {
                itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
            }
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10, 0);
            itemLabelRect.offsetMax = Vector2.zero;
            TMP_Text itemLabel = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabel.text = "选项";
            itemLabel.fontSize = 16; // 调大一号 (14 -> 16)
            itemLabel.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            ApplyFontToTMP(itemLabel);
            itemToggle.graphic = itemLabel;

            pptDropdownTMP.template = templateRect;
            pptDropdownTMP.itemText = itemLabel;

            // 创建DropdownManager组件
            pptDropdown = dropdownObj.AddComponent<DropdownManager>();
            pptDropdown.dropdown = pptDropdownTMP;

            // 初始化下拉框选项
            StartCoroutine(InitializePPTDropdown());
            
            // 演讲稿生成按钮
            generateSpeechButton = CreateButton(pptPanel.transform, "生成演讲稿", new Vector2(200, 40));

            // 演讲稿输入框（使用 TextMeshPro 版本）
            GameObject inputLabel = CreateLabel(pptPanel.transform, "演讲稿：", 16);
            GameObject inputObj = new GameObject("SpeechInputField");
            inputObj.transform.SetParent(pptPanel.transform, false);

            RectTransform inputRect = inputObj.GetComponent<RectTransform>();
            if (inputRect == null)
            {
                inputRect = inputObj.AddComponent<RectTransform>();
            }
            inputRect.sizeDelta = new Vector2(0, 200);

            Image inputBg = inputObj.AddComponent<Image>();
            // 输入框背景：浅灰，略带边界感
            inputBg.color = new Color(0.97f, 0.97f, 0.99f, 1f);

            // 使用 TMP_InputField + TextMeshProUGUI
            speechInputField = inputObj.AddComponent<TMP_InputField>();
            TMP_Text speechTextComp = CreateTextComponent(inputObj.transform, "");
            TMP_Text speechPlaceholderComp = CreateTextComponent(inputObj.transform, "演讲稿将显示在这里...");
            speechPlaceholderComp.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            speechInputField.textComponent = speechTextComp;
            speechInputField.placeholder = speechPlaceholderComp;

            // 状态文本
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(pptPanel.transform, false);
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            if (statusRect == null)
            {
                statusRect = statusObj.AddComponent<RectTransform>();
            }
            statusRect.sizeDelta = new Vector2(0, 30);
            pptStatusText = statusObj.AddComponent<TextMeshProUGUI>();
            pptStatusText.text = "就绪";
            pptStatusText.fontSize = 16; // 调大一号 (14 -> 16)
            // 状态文本使用稍深的绿色以适配浅色背景
            pptStatusText.color = new Color(0.0f, 0.55f, 0.27f, 1f);
            pptStatusText.alignment = TextAlignmentOptions.Left;
            ApplyFontToTMP(pptStatusText);

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
            // 加载指示器颜色：蓝色强调
            loadingImg.color = new Color(0.23f, 0.45f, 0.85f, 0.6f);
            pptLoadingIndicator.SetActive(false);
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

        void OnGenerateSpeech()
        {
            if (autoDesc == null)
            {
                Debug.LogWarning("AutoDesc组件未找到，无法生成演讲稿");
                if (pptStatusText != null)
                    pptStatusText.text = "错误：AutoDesc组件未找到";
                return;
            }

            // 检查是否选择了PPT
            if (pptDropdown == null || pptDropdown.dropdown == null || 
                pptDropdown.dropdown.options == null || pptDropdown.dropdown.options.Count == 0)
            {
                if (pptStatusText != null)
                    pptStatusText.text = "错误：请先选择PPT";
                return;
            }

            if (pptStatusText != null)
                pptStatusText.text = "正在生成演讲稿...";
            if (pptLoadingIndicator != null)
                pptLoadingIndicator.SetActive(true);

            // 同步PPT选择到AutoDesc
            if (autoDesc != null && pptDropdown != null)
            {
                // 使用反射设置AutoDesc的dropdown
                var dropdownField = autoDesc.GetType().GetField("dropdown", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dropdownField != null)
                {
                    var autoDescDropdown = dropdownField.GetValue(autoDesc) as DropdownManager;
                    if (autoDescDropdown != null && pptDropdown.dropdown != null)
                    {
                        // 同步当前选择的PPT
                        string currentPPT = pptDropdown.GetCurrentOptionText();
                        if (!string.IsNullOrEmpty(currentPPT))
                        {
                            autoDescDropdown.SetCurrentOptionText(currentPPT);
                        }
                    }
                }
            }

            // 调用AutoDesc的生成方法
            autoDesc.StartGetDescProcess();

            // 等待生成完成（通过协程检查）
            StartCoroutine(WaitForSpeechGeneration());
        }

        IEnumerator InitializePPTDropdown()
        {
            yield return null; // 等待一帧确保组件已初始化
            
            if (pptDropdown != null)
            {
                // DropdownManager会在Start中自动初始化
                // 这里可以添加额外的初始化逻辑
            }
        }

        IEnumerator WaitForSpeechGeneration()
        {
            // 等待一段时间让AutoDesc处理
            yield return new WaitForSeconds(0.5f);

            // 尝试获取生成的演讲稿
            if (autoDesc != null && speechInputField != null)
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
                        float timeout = 30f; // 30秒超时
                        float elapsed = 0f;
                        while (elapsed < timeout)
                        {
                            if (!string.IsNullOrEmpty(autoDescInput.text))
                            {
                                speechInputField.text = autoDescInput.text;
                                break;
                            }
                            yield return new WaitForSeconds(0.5f);
                            elapsed += 0.5f;
                        }
                    }
                }
            }

            if (pptStatusText != null)
                pptStatusText.text = "演讲稿生成完成";
            if (pptLoadingIndicator != null)
                pptLoadingIndicator.SetActive(false);
        }

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
    }
}

