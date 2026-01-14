using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Reflection;
using Unity.VisualScripting;

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

        [Header("Styling")]
        public Sprite roundedListSprite;
        public Sprite roundedItemSprite;
        public Color textColor = Color.white;

        [Header("PPT Panel Components")]
        public DropdownManager pptDropdown;
        public TMP_Dropdown pptDropdownTMP;
        public Button generateSpeechButton;
        public TMP_InputField speechInputField;
        public GameObject pptLoadingIndicator;

        [Header("Button Backgrounds")]
        public Sprite buttonBackgroundBlue;
        public Sprite buttonBackgroundGrey;
        public Sprite buttonBackgroundRed;
        public Sprite longButtonBackgroundBlue;
        public Sprite tabBackgroundBlue;
        public Sprite tabBackgroundGrey;
        
        // 新的PPT面板组件
        public Button addPPTButton;
        public ScrollRect pptListScrollRect;
        public GameObject pptListContent;
        public Button deleteButton;
        public Button configButton;
        public Button playButton;
        public GameObject configPanel;
        public TMP_InputField configInputField;
        public Button confirmConfigButton;
        public Button cancelConfigButton;
        private GameObject configOverlay;
        private RectTransform configOverlayRect;
        private Vector2 configOverlayOriginalAnchorMin;
        private Vector2 configOverlayOriginalAnchorMax;
        private bool configOverlayExpanded = false;
        private TMP_Text pptPageCountLabel; // 显示PPT页数的文本组件
        private TextMeshProUGUI configParagraphNumberText;
        private bool configNumberScrollOffsetInitialized;
        private Vector2 configNumberScrollBaseAnchoredPos;
        private Vector2 configContentScrollBaseAnchoredPos;
        private Vector2 configNumberScrollLastViewportSize;
        private float configNumberScrollLastCanvasScaleFactor;
        private RectTransform configViewportRect;
        private RectTransform configContentTextRect;
        private RectTransform configPlaceholderRect;
        private int lastConfigTextLength = 0;

        // 全屏按钮引用
        private Button configFullscreenButton;

        [Header("Model Panel Reference")]
        public ModelPanelUI modelPanelUI;

        [Header("Settings Panel Reference")]
        public SettingsPanelContent settingsPanelContent;        
        [Header("Canvas Sorting")]
        [Tooltip("设置面板所在Canvas的排序层级，确保高于数字人以拦截点击")]
        public int canvasSortingOrder = 1000;

        private AutoDesc autoDesc;
        private int currentTabIndex = 0; // 0=PPT, 1=Model, 2=Settings
        
        // PPT列表管理
        private List<PPTListItem> pptListItems = new List<PPTListItem>();
        private PPTListItem selectedPPTItem = null;

        [SerializeField] private PPTController pptController;
        [SerializeField] private PPTControlUI pptControlUI;
        
        // 全屏功能相关字段
        private bool isFullscreen = false;

        // 全屏功能备份的原始状态(仅用于configPanel)
        private Vector2 fullscreenOriginalAnchorMin;
        private Vector2 fullscreenOriginalAnchorMax;
        private Vector2 fullscreenOriginalOffsetMin;
        private Vector2 fullscreenOriginalOffsetMax;
        private Transform fullscreenOriginalParent;
        private int fullscreenOriginalSiblingIndex;

        // configOverlay扩展功能备份的原始状态(仅用于configOverlay)
        private Transform overlayOriginalParent;
        private int overlayOriginalSiblingIndex;

        // 备份演讲稿内容，用于生成失败时恢复
        private string[] backupSpeechContent;

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
            
            // 查找或创建PPTControlUI组件
            if (pptControlUI == null)
            {
                pptControlUI = FindObjectOfType<PPTControlUI>();
                
                // 如果场景中没有,自动创建一个
                if (pptControlUI == null)
                {
                    GameObject pptControlUIObj = new GameObject("PPTControlUI");
                    pptControlUI = pptControlUIObj.AddComponent<PPTControlUI>();
                    Debug.Log("[SettingsPanelUI] 自动创建PPTControlUI组件");
                }
            }
            
            // 注意：如果面板是关闭的，ShowTab 可能不会正确显示，所以在 OpenPanel 时再调用
        }

        void OnEnable()
        {
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
        }

        private void OnWillRenderCanvases()
        {
            SyncConfigParagraphNumberScroll();
        }

        void LateUpdate()
        {
            SyncConfigParagraphNumberScroll();
        }

        void Awake()
        {
            buttonBackgroundBlue = Resources.Load<Sprite>("buttonBackgroundBlue");
            buttonBackgroundGrey = Resources.Load<Sprite>("buttonBackgroundGrey");
            buttonBackgroundRed = Resources.Load<Sprite>("buttonBackgroundRed");
            longButtonBackgroundBlue = Resources.Load<Sprite>("longButtonBackgroundBlue");
            tabBackgroundBlue = Resources.Load<Sprite>("tabBackgroundBlue");
            tabBackgroundGrey = Resources.Load<Sprite>("tabBackgroundGrey");
            
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
            
            // 确保FontManager已初始化
            FontManager.Instance.GetSIMSUNFont();
            
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
            {
                GameObject _ = new GameObject("AutoDesc");
                autoDesc = _.AddComponent<AutoDesc>();
            }
            
            // 初始化ModelPanelUI组件
            if (modelPanelUI == null)
            {
                modelPanelUI = gameObject.GetComponent<ModelPanelUI>();
                if (modelPanelUI == null)
                {
                    modelPanelUI = gameObject.AddComponent<ModelPanelUI>();
                }
            }

            // 初始化SettingsPanelContent组件
            if (settingsPanelContent == null)
            {
                settingsPanelContent = gameObject.GetComponent<SettingsPanelContent>();
                if (settingsPanelContent == null)
                {
                    settingsPanelContent = gameObject.AddComponent<SettingsPanelContent>();
                }
            }

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
        }

        void CreateMainPanel()
        {
            // 创建主面板
            mainPanel = new GameObject("SettingsPanel");
            mainPanel.transform.SetParent(parentCanvas.transform, false);

            RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.3f, 0.25f);
            panelRect.anchorMax = new Vector2(0.7f, 0.75f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            Image panelBg = mainPanel.AddComponent<Image>();
            Sprite settingsBackgroundSprite = Resources.Load<Sprite>("background");
            panelBg.sprite = settingsBackgroundSprite;
            panelBg.raycastTarget = true;
            
            // 主面板可拖动
            DragHandler mainPanelDrag = mainPanel.AddComponent<DragHandler>();
            mainPanelDrag.dragSmooth = 10f;

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
            titleRect.anchoredPosition = new Vector2(0, -2f);
            titleRect.sizeDelta = new Vector2(0, 60);

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
            title.fontSize = 36;
            // 标题文字使用明亮的白色，带有轻微发光效果
            title.color = textColor;
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            FontManager.ApplyFont(title);
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
            tabBarRect.anchoredPosition = new Vector2(0, -70);
            tabBarRect.sizeDelta = new Vector2(0, 50);

            HorizontalLayoutGroup layout = tabBar.AddComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 8;
            layout.padding = new RectOffset(15, 15, 8, 8);
            
            // 添加标签栏背景：深色背景
            Image tabBarBg = tabBar.AddComponent<Image>();
            tabBarBg.color = new Color(0.10f, 0.11f, 0.14f, 0f);

            // 创建PPT标签按钮
            pptTabButton = CreateTabButton(tabBar.transform, "PPT", 0);
            // 创建模型标签按钮
            modelTabButton = CreateTabButton(tabBar.transform, "角色", 1);
            // 创建设置标签按钮
            settingsTabButton = CreateTabButton(tabBar.transform, "系统设置", 2);
        }

        Button CreateTabButton(Transform parent, string text, int tabIndex)
        {
            GameObject btnObj = new GameObject($"TabButton_{text}");
            btnObj.transform.SetParent(parent, false);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.sprite = tabBackgroundGrey;

            Button btn = btnObj.AddComponent<Button>();

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
            btnText.fontSize = 24;
            // 标签按钮文字颜色：明亮的浅色
            btnText.color = textColor;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.fontStyle = FontStyles.Bold;
            FontManager.ApplyFont(btnText);

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
            // 让中间滚动区域填充剩余空间：让布局控制子对象高度并允许扩展
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            // 创建标题和添加按钮的容器
            GameObject headerContainer = new GameObject("HeaderContainer");
            headerContainer.transform.SetParent(pptPanel.transform, false);
            RectTransform headerRect = headerContainer.GetComponent<RectTransform>();
            if (headerRect == null)
            {
                headerRect = headerContainer.AddComponent<RectTransform>();
            }
            // 固定标题高度，不随布局伸缩
            LayoutElement headerLayoutElement = headerContainer.AddComponent<LayoutElement>();
            headerLayoutElement.preferredHeight = 40;
            headerLayoutElement.flexibleHeight = 0;
            
            HorizontalLayoutGroup headerLayout = headerContainer.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = true;
            headerLayout.spacing = 10;
            headerLayout.padding = new RectOffset(0, 0, 0, 0);

            // PPT列表标题
            GameObject pptListLabel = CreateLabel(headerContainer.transform, "PPT列表：", 16);

            GameObject marginBlank = new GameObject("marginBlank", typeof(RectTransform));
            marginBlank.transform.SetParent(headerContainer.transform, false);

            // 添加PPT按钮
            addPPTButton = CreateButton(headerContainer.transform, "+ 添加PPT", new Vector2(120, 40), "longButtonBackgroundBlue");
            addPPTButton.onClick.AddListener(OnAddPPTClicked);

            // 创建PPT列表滚动视图
            GameObject scrollObj = new GameObject("PPTListScrollView");
            scrollObj.transform.SetParent(pptPanel.transform, false);
            RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
            if (scrollRect == null)
            {
                scrollRect = scrollObj.AddComponent<RectTransform>();
            }
            // 让滚动视图在垂直方向上可伸缩以填充布局剩余空间
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            LayoutElement scrollLayoutElement = scrollObj.AddComponent<LayoutElement>();
            scrollLayoutElement.preferredHeight = -1;
            scrollLayoutElement.flexibleHeight = 1;

            pptListScrollRect = scrollObj.AddComponent<ScrollRect>();
            pptListScrollRect.horizontal = false;
            pptListScrollRect.vertical = true;
            pptListScrollRect.scrollSensitivity = 20f;

            Image scrollBg = scrollObj.AddComponent<Image>();
            Sprite roundedCornerSprite = Resources.Load<Sprite>("圆角矩形");
            if (roundedCornerSprite != null)
            {
                scrollBg.sprite = roundedCornerSprite;
                scrollBg.type = Image.Type.Sliced;
            }
            scrollBg.color = new Color(41f/255f, 58f/255f, 108f/255f, 1f);
            scrollBg.raycastTarget = false;
            
            // 添加滚动视图边框
            // Outline scrollOutline = scrollObj.AddComponent<Outline>();
            // scrollOutline.effectColor = new Color(0.25f, 0.30f, 0.40f, 0.5f);
            // scrollOutline.effectDistance = new Vector2(1, -1);

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
            viewportRect.offsetMin = new Vector2(8, 15);
            viewportRect.offsetMax = new Vector2(-8, -15);

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
            contentLayout.padding = new RectOffset(2, 2, 2, 2);

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
            // 固定底部按钮容器高度
            LayoutElement btnContainerLayout = buttonContainer.AddComponent<LayoutElement>();
            btnContainerLayout.preferredHeight = 50;
            btnContainerLayout.flexibleHeight = 0;

            HorizontalLayoutGroup buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = true;
            buttonLayout.spacing = 10;
            buttonLayout.padding = new RectOffset(0, 0, 0, 0);
            buttonLayout.childAlignment = TextAnchor.MiddleRight;

            // 删除按钮
            deleteButton = CreateButton(buttonContainer.transform, "删除", new Vector2(100, 40), "buttonBackgroundRed");
            deleteButton.onClick.AddListener(OnDeleteClicked);
            deleteButton.interactable = false; // 默认不可点击，需先选中PPT

            // 配置按钮
            configButton = CreateButton(buttonContainer.transform, "配置", new Vector2(100, 40), "buttonBackgroundBlue");
            configButton.onClick.AddListener(OnConfigClicked);
            configButton.interactable = false; // 默认不可点击，需先选中PPT

            // 播放按钮
            playButton = CreateButton(buttonContainer.transform, "播放", new Vector2(100, 40), "buttonBackgroundBlue");
            playButton.onClick.AddListener(OnPlayClicked);
            playButton.interactable = false; // 默认不可点击

            configOverlay = new GameObject("ConfigOverlay");
            configOverlay.transform.SetParent(mainPanel.transform, false);
            configOverlayRect = configOverlay.GetComponent<RectTransform>();
            if (configOverlayRect == null)
            {
                configOverlayRect = configOverlay.AddComponent<RectTransform>();
            }
            configOverlayRect.anchorMin = Vector2.zero;
            configOverlayRect.anchorMax = Vector2.one;
            configOverlayRect.offsetMin = Vector2.zero;
            configOverlayRect.offsetMax = Vector2.zero;

            // 备份原始anchor值
            configOverlayOriginalAnchorMin = configOverlayRect.anchorMin;
            configOverlayOriginalAnchorMax = configOverlayRect.anchorMax;

            Image overlayBg = configOverlay.AddComponent<Image>();
            overlayBg.color = new Color(0f, 0f, 0f, 0f);

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
            configPanelRect.sizeDelta = new Vector2(1000, 650);

            Image configPanelBg = configPanel.AddComponent<Image>();
            configPanelBg.sprite = Resources.Load<Sprite>("background");
            configPanelBg.raycastTarget = true;

            // 演讲稿输入框标签和AI生成按钮容器
            GameObject labelContainer = new GameObject("LabelContainer");
            labelContainer.transform.SetParent(configPanel.transform, false);
            RectTransform labelContainerRect = labelContainer.GetComponent<RectTransform>();
            if (labelContainerRect == null)
            {
                labelContainerRect = labelContainer.AddComponent<RectTransform>();
            }
            // 固定在顶部
            labelContainerRect.anchorMin = new Vector2(0, 1);
            labelContainerRect.anchorMax = new Vector2(1, 1);
            labelContainerRect.pivot = new Vector2(0.5f, 1);
            labelContainerRect.anchoredPosition = new Vector2(0, -15);
            labelContainerRect.sizeDelta = new Vector2(-40, 40);

            HorizontalLayoutGroup labelLayout = labelContainer.AddComponent<HorizontalLayoutGroup>();
            labelLayout.childForceExpandWidth = false;
            labelLayout.childForceExpandHeight = true;
            labelLayout.spacing = 10;
            labelLayout.padding = new RectOffset(0, 0, 0, 0);
            labelLayout.childAlignment = TextAnchor.MiddleLeft;

            // 演讲稿输入框标签
            GameObject configInputLabel = CreateLabel(labelContainer.transform, "演讲稿(一段对应一页)：", 16);
            RectTransform configInputLabelRect = configInputLabel.GetComponent<RectTransform>();
            configInputLabelRect.sizeDelta = new Vector2(250, 40);

            // 添加PPT页数显示
            GameObject pageCountLabelObj = CreateLabel(labelContainer.transform, "共0页", 14);
            RectTransform pageCountRect = pageCountLabelObj.GetComponent<RectTransform>();
            pageCountRect.sizeDelta = new Vector2(80, 40);
            // 设置文字颜色为浅灰色
            pptPageCountLabel = pageCountLabelObj.GetComponent<TMP_Text>();
            pptPageCountLabel.color = textColor;

            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(labelContainer.transform, false);
            RectTransform spacerRect = spacer.AddComponent<RectTransform>();
            LayoutElement spacerElement = spacer.AddComponent<LayoutElement>();
            spacerElement.flexibleWidth = 1;

            // AI生成演讲稿按钮（放在标签右侧）
            generateSpeechButton = CreateButton(labelContainer.transform, "AI生成演讲稿", new Vector2(200, 40), "longButtonBackgroundBlue");

            GameObject configFullscreenButtonObj = new GameObject("configFullscreenButtonObj");
            configFullscreenButtonObj.transform.SetParent(labelContainer.transform, false);
            LayoutElement le = configFullscreenButtonObj.AddComponent<LayoutElement>();
            le.minWidth = 40;
            le.preferredWidth = 40;
            le.preferredHeight = 40;
            // 全屏按钮（放在最右侧）
            configFullscreenButton = configFullscreenButtonObj.AddComponent<Button>();
            Image configFullscreenButtonImage = configFullscreenButtonObj.AddComponent<Image>();
            configFullscreenButtonImage.sprite = Resources.Load<Sprite>("fullscreen");
            configFullscreenButtonImage.preserveAspect = true;
            configFullscreenButton.targetGraphic = configFullscreenButtonImage;

            // 添加全屏按钮点击事件
            configFullscreenButton.onClick.AddListener(() => {
                ToggleFullscreen();
            });

            // 关闭按钮
            GameObject configCloseButtonObj = new GameObject("ConfigCloseButton"); 
            configCloseButtonObj.transform.SetParent(labelContainer.transform, false); 
            Button configCloseButton = configCloseButtonObj.AddComponent<Button>(); 
            Image configCloseButtonImage = configCloseButtonObj.AddComponent<Image>(); 
            configCloseButtonImage.sprite = Resources.Load<Sprite>("close@2x"); 
            configCloseButtonImage.preserveAspect = true;
            LayoutElement lle = configCloseButtonObj.AddComponent<LayoutElement>();
            lle.minWidth = 40;
            lle.preferredWidth = 40;
            lle.preferredHeight = 40;

            configCloseButton.targetGraphic = configCloseButtonImage; 
            // 添加关闭按钮点击事件 
            configCloseButton.onClick.AddListener(() => { OnCancelConfig(); });

            // 演讲稿输入框（使用TMP_InputField自带滚动）
            GameObject configInputObj = new GameObject("ConfigInputField");
            configInputObj.transform.SetParent(configPanel.transform, false);
            RectTransform configInputRect = configInputObj.GetComponent<RectTransform>();
            if (configInputRect == null)
            {
                configInputRect = configInputObj.AddComponent<RectTransform>();
            }
            // 固定在标签下方，底部留出空间给按钮
            configInputRect.anchorMin = new Vector2(0, 0);
            configInputRect.anchorMax = new Vector2(1, 1);
            configInputRect.pivot = new Vector2(0.5f, 1);
            configInputRect.anchoredPosition = new Vector2(0, -78); // 标签高度40 + 间距18 + 顶部边距20
            configInputRect.sizeDelta = new Vector2(-40, -165); // 左右边距20，底部留出空间给按钮容器(70) + 顶部(78) + 底部边距(20)
            
            // 添加背景图片组件（Outline需要Graphic组件才能工作）
            Image inputBackground = configInputObj.AddComponent<Image>();
            inputBackground.color = new Color(0.1608f, 0.1922f, 0.3725f, 1f); // 半透明白色背景
            
            // 添加输入框边框效果
            Outline inputOutline = configInputObj.AddComponent<Outline>();
            inputOutline.effectColor = new Color(0.25f, 0.30f, 0.45f, 0.5f);
            inputOutline.effectDistance = new Vector2(2, -2);

            configInputField = configInputObj.AddComponent<TMP_InputField>();
            
            // 创建视口用于裁剪文本
            GameObject configViewportObj = new GameObject("Viewport");
            configViewportObj.transform.SetParent(configInputObj.transform, false);
            configViewportRect = configViewportObj.GetComponent<RectTransform>();
            if (configViewportRect == null)
            {
                configViewportRect = configViewportObj.AddComponent<RectTransform>();
            }
            configViewportRect.anchorMin = Vector2.zero;
            configViewportRect.anchorMax = Vector2.one;
            configViewportRect.offsetMin = new Vector2(5, 5);
            configViewportRect.offsetMax = new Vector2(-5, -5);
            
            // 添加RectMask2D来裁剪溢出的文本
            RectMask2D configViewportMask = configViewportObj.AddComponent<RectMask2D>();
            
            // 创建文本区域
            GameObject textAreaObj = new GameObject("Text Area");
            textAreaObj.transform.SetParent(configViewportObj.transform, false);
            RectTransform textAreaRect = textAreaObj.GetComponent<RectTransform>();
            if (textAreaRect == null)
            {
                textAreaRect = textAreaObj.AddComponent<RectTransform>();
            }
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(5, 5);
            textAreaRect.offsetMax = new Vector2(-5, 0); // 增加底部边距到15像素，确保文本显示完整
 
            GameObject paragraphLayoutObj = new GameObject("ParagraphLayout");
            paragraphLayoutObj.transform.SetParent(textAreaObj.transform, false);
            RectTransform paragraphLayoutRect = paragraphLayoutObj.GetComponent<RectTransform>();
            if (paragraphLayoutRect == null)
            {
                paragraphLayoutRect = paragraphLayoutObj.AddComponent<RectTransform>();
            }
            paragraphLayoutRect.anchorMin = Vector2.zero;
            paragraphLayoutRect.anchorMax = Vector2.one;
            paragraphLayoutRect.offsetMin = Vector2.zero;
            paragraphLayoutRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup paragraphLayoutGroup = paragraphLayoutObj.AddComponent<HorizontalLayoutGroup>();
            paragraphLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            paragraphLayoutGroup.childControlWidth = true;
            paragraphLayoutGroup.childControlHeight = true;
            paragraphLayoutGroup.childForceExpandWidth = false;
            paragraphLayoutGroup.childForceExpandHeight = true;
            paragraphLayoutGroup.padding = new RectOffset(0, 0, 0, 0);

            GameObject numberTextObj = new GameObject("NumberText");
            numberTextObj.transform.SetParent(paragraphLayoutObj.transform, false);
            RectTransform numberTextRect = numberTextObj.GetComponent<RectTransform>();
            if (numberTextRect == null)
            {
                numberTextRect = numberTextObj.AddComponent<RectTransform>();
            }
            numberTextRect.anchorMin = new Vector2(0, 0);
            numberTextRect.anchorMax = new Vector2(0, 1);
            numberTextRect.pivot = new Vector2(0, 1);

            LayoutElement numberLayout = numberTextObj.AddComponent<LayoutElement>();
            numberLayout.preferredWidth = 50;
            numberLayout.flexibleWidth = 0;

            configParagraphNumberText = numberTextObj.AddComponent<TextMeshProUGUI>();
            configParagraphNumberText.text = "";
            configParagraphNumberText.fontSize = 20;
            configParagraphNumberText.color = textColor;
            configParagraphNumberText.alignment = TextAlignmentOptions.TopRight;
            configParagraphNumberText.enableWordWrapping = false;
            configParagraphNumberText.overflowMode = TextOverflowModes.Overflow;
            configParagraphNumberText.margin = new Vector4(5, 5, 5, 5);
            configParagraphNumberText.lineSpacing = 60;
            FontManager.ApplyFont(configParagraphNumberText);

            GameObject contentContainerObj = new GameObject("ContentContainer");
            contentContainerObj.transform.SetParent(paragraphLayoutObj.transform, false);
            RectTransform contentContainerRect = contentContainerObj.GetComponent<RectTransform>();
            if (contentContainerRect == null)
            {
                contentContainerRect = contentContainerObj.AddComponent<RectTransform>();
            }
            contentContainerRect.anchorMin = Vector2.zero;
            contentContainerRect.anchorMax = Vector2.one;
            contentContainerRect.offsetMin = Vector2.zero;
            contentContainerRect.offsetMax = Vector2.zero;

            RectMask2D contentColumnMask = contentContainerObj.AddComponent<RectMask2D>();

            LayoutElement contentLayoutElement = contentContainerObj.AddComponent<LayoutElement>();
            contentLayoutElement.flexibleWidth = 1;

            GameObject textObj = new GameObject("ContentText");
            textObj.transform.SetParent(contentContainerObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect == null)
            {
                textRect = textObj.AddComponent<RectTransform>();
            }
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TMP_Text configTextComp = textObj.AddComponent<TextMeshProUGUI>();
            configTextComp.text = "";
            configTextComp.fontSize = 20;
            configTextComp.color = textColor;
            configTextComp.alignment = TextAlignmentOptions.TopLeft;
            configTextComp.overflowMode = TextOverflowModes.Overflow;
            configTextComp.margin = new Vector4(25, 5, 5, 5);  
            configTextComp.lineSpacing = 60;
            FontManager.ApplyFont(configTextComp);
            
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(contentContainerObj.transform, false);
            RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
            if (placeholderRect == null)
            {
                placeholderRect = placeholderObj.AddComponent<RectTransform>();
            }
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            configPlaceholderRect = placeholderRect;

            TMP_Text configPlaceholderComp = placeholderObj.AddComponent<TextMeshProUGUI>();
            configPlaceholderComp.text = "请输入演讲稿或使用AI生成...";
            configPlaceholderComp.fontSize = 20;
            configPlaceholderComp.color = new Color(0.45f, 0.48f, 0.55f, 0.6f);
            configPlaceholderComp.alignment = TextAlignmentOptions.TopLeft;
            configPlaceholderComp.margin = new Vector4(25, 5, 5, 5);  
            configPlaceholderComp.lineSpacing = 60;
            FontManager.ApplyFont(configPlaceholderComp);
            
            // 配置输入框
            configInputField.textComponent = configTextComp;
            configInputField.placeholder = configPlaceholderComp;
            configInputField.textViewport = contentContainerRect;
            configInputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            configInputField.scrollSensitivity = 5f;
            configInputField.onFocusSelectAll = false;
            configInputField.caretWidth = 2;

            configInputField.onValueChanged.AddListener(OnConfigInputFieldValueChanged);

            // 设置AI生成演讲稿按钮的点击事件
            generateSpeechButton.onClick.AddListener(OnGenerateSpeech);

            // 确认和取消按钮容器 - 固定在配置面板最底部
            GameObject configButtonContainer = new GameObject("ConfigButtonContainer");
            configButtonContainer.transform.SetParent(configPanel.transform, false);
            RectTransform configButtonContainerRect = configButtonContainer.GetComponent<RectTransform>();
            if (configButtonContainerRect == null)
            {
                configButtonContainerRect = configButtonContainer.AddComponent<RectTransform>();
            }
            // 固定在底部
            configButtonContainerRect.anchorMin = new Vector2(0, 0);
            configButtonContainerRect.anchorMax = new Vector2(1, 0);
            configButtonContainerRect.pivot = new Vector2(0.5f, 0);
            configButtonContainerRect.anchoredPosition = new Vector2(0, 20); // 底部边距20
            configButtonContainerRect.sizeDelta = new Vector2(-40, 50); // 左右边距20，高度50

            HorizontalLayoutGroup configButtonLayout = configButtonContainer.AddComponent<HorizontalLayoutGroup>();
            configButtonLayout.childForceExpandWidth = false;
            configButtonLayout.childForceExpandHeight = true;
            configButtonLayout.spacing = 10;
            configButtonLayout.padding = new RectOffset(0, 0, 0, 0);
            configButtonLayout.childAlignment = TextAnchor.MiddleRight;

            // 确认按钮
            confirmConfigButton = CreateButton(configButtonContainer.transform, "确认", new Vector2(100, 40), "buttonBackgroundBlue");
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
            // 委托给ModelPanelUI组件处理模型面板内容创建
            if (modelPanelUI != null)
            {
                modelPanelUI.SetModelPanel(modelPanel);
                modelPanelUI.CreateModelPanelContent();
            }
            else
            {
                Debug.LogError("ModelPanelUI组件未找到，无法创建模型面板内容");
            }
        }

        void CreateSettingsPanelContent()
        {
            // 委托给SettingsPanelContent组件处理设置面板内容创建
            if (settingsPanelContent != null)
            {
                settingsPanelContent.SetSettingsPanel(settingsPanel);
                settingsPanelContent.CreateSettingsPanelContent(CreateLabel);
            }
            else
            {
                Debug.LogError("SettingsPanelContent组件未找到，无法创建设置面板内容");
            }
        }

        void CreateCloseButton()
        {
            GameObject closeBtnObj = new GameObject("CloseButton");
            // 将关闭按钮放在标题栏中
            Transform titleBar = mainPanel.transform.Find("TitleBar");
            closeBtnObj.transform.SetParent(titleBar != null ? titleBar : mainPanel.transform, false);

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
            closeButton.targetGraphic = closeImage;
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
            fontSize += 6; // 调大字体
            rect.sizeDelta = new Vector2(0, fontSize + 10);
            TMP_Text labelText = label.AddComponent<TextMeshProUGUI>();
            labelText.text = text;
            labelText.fontSize = fontSize;
            labelText.color = textColor;
            labelText.alignment = TextAlignmentOptions.Left;
            FontManager.ApplyFont(labelText);
            return label;
        }

        Button CreateButton(Transform parent, string text, Vector2 size, string bg = "buttonBackgroundBlue")
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
            if(bg == "buttonBackgroundBlue") btnBg.sprite = buttonBackgroundBlue;
            else if(bg == "buttonBackgroundGrey") btnBg.sprite = buttonBackgroundGrey;
            else if(bg == "buttonBackgroundRed") btnBg.sprite = buttonBackgroundRed;
            else if(bg == "longButtonBackgroundBlue") btnBg.sprite = longButtonBackgroundBlue;
            else btnBg.sprite = buttonBackgroundBlue;

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
            textComp.fontSize = 20; // 调整字体大小
            textComp.color = textColor;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.fontStyle = FontStyles.Bold;
            FontManager.ApplyFont(textComp);

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
            textComp.fontSize = 20;
            textComp.color = textColor;
            textComp.alignment = TextAlignmentOptions.TopLeft;
            FontManager.ApplyFont(textComp);

            return textComp;
        }

        public void ShowTab(int tabIndex)
        {
            currentTabIndex = tabIndex;
            pptTabButton.GetComponent<Image>().sprite = tabBackgroundGrey;
            modelTabButton.GetComponent<Image>().sprite = tabBackgroundGrey;
            settingsTabButton.GetComponent<Image>().sprite = tabBackgroundGrey;
            pptPanel.SetActive(false);
            modelPanel.SetActive(false);
            settingsPanel.SetActive(false);
           
            // 显示选中的面板
            switch (tabIndex)
            {
                case 0: // PPT
                    pptPanel.SetActive(true);
                    pptTabButton.GetComponent<Image>().sprite = tabBackgroundBlue;
                    break;
                case 1: // Model
                    modelPanel.SetActive(true);
                    modelTabButton.GetComponent<Image>().sprite = tabBackgroundBlue;
                    break;
                case 2: // Settings
                    settingsPanel.SetActive(true);
                    settingsTabButton.GetComponent<Image>().sprite = tabBackgroundBlue;
                    break;
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
                    
                    // 应用字体到所有 TextMeshPro 组件
                    FontManager.ApplyFontToAll(mainPanel);
                    
                    Debug.Log($"设置面板子组件创建完成，子对象数量: {mainPanel.transform.childCount}");
                    
                    // 恢复原来的状态
                    if (!wasActive)
                        mainPanel.SetActive(false);
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
            itemBg.color = Color.white;
            
            // 添加列表项边框
            Outline itemOutline = itemObj.AddComponent<Outline>();
            itemOutline.effectColor = new Color(0.25f, 0.30f, 0.40f, 0.5f); // 暗色边框
            itemOutline.effectDistance = new Vector2(1, -1);

            Button itemButton = itemObj.AddComponent<Button>();
            itemButton.transition = Selectable.Transition.None;
            itemButton.targetGraphic = itemBg;
            itemBg.color = new Color(0.039f, 0.102f, 0.243f, 1f);

            HorizontalLayoutGroup itemLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
            itemLayout.childForceExpandWidth = false;
            itemLayout.childForceExpandHeight = true;
            itemLayout.spacing = 10;
            itemLayout.padding = new RectOffset(40, 10, 5, 5);

            // 添加PPTicon
            GameObject pptIconObj = new GameObject("PPTiconObj");
            pptIconObj.transform.SetParent(itemObj.transform, false);
            LayoutElement pptIconLayout = pptIconObj.AddComponent<LayoutElement>();
            pptIconLayout.minWidth = 64;
            pptIconLayout.preferredWidth = 64;
            Image pptIconImage = pptIconObj.AddComponent<Image>();
            pptIconImage.sprite = Resources.Load<Sprite>("ppt");
            pptIconImage.preserveAspect = true;

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
            fileNameText.fontSize = 20;
            fileNameText.color = textColor;
            fileNameText.alignment = TextAlignmentOptions.Left;
            fileNameText.enableWordWrapping = false;
            fileNameText.overflowMode = TextOverflowModes.Ellipsis;
            fileNameText.extraPadding = true;
            FontManager.ApplyFont(fileNameText);

            LayoutElement fileNameLayout = fileNameObj.AddComponent<LayoutElement>();
            fileNameLayout.flexibleWidth = 1;

            // 页数
            GameObject pageCountObj = new GameObject("PageCount");
            pageCountObj.transform.SetParent(itemObj.transform, false);
            RectTransform pageCountRect = pageCountObj.GetComponent<RectTransform>();
            if (pageCountRect == null)
            {
                pageCountRect = pageCountObj.AddComponent<RectTransform>();
            }
            pageCountRect.sizeDelta = new Vector2(150, 0);
            LayoutElement pageCountLayout = pageCountObj.AddComponent<LayoutElement>();
            pageCountLayout.minWidth = 150;
            pageCountLayout.preferredWidth = 150;
            pageCountLayout.flexibleWidth = 0;
            TMP_Text pageCountText = pageCountObj.AddComponent<TextMeshProUGUI>();
            pageCountText.text = $"页数: {pptInfo.pageCount}";
            pageCountText.fontSize = 18;
            pageCountText.color = textColor;
            pageCountText.alignment = TextAlignmentOptions.Center;
            FontManager.ApplyFont(pageCountText);

            // 配置状态
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(itemObj.transform, false);
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            if (statusRect == null)
            {
                statusRect = statusObj.AddComponent<RectTransform>();
            }
            statusRect.sizeDelta = new Vector2(150, 0);
            LayoutElement statusLayout = statusObj.AddComponent<LayoutElement>();
            statusLayout.minWidth = 150;
            statusLayout.preferredWidth = 150;
            statusLayout.flexibleWidth = 0;
            TMP_Text statusText = statusObj.AddComponent<TextMeshProUGUI>();
            string statusStr = GetStatusString(pptInfo.configStatus);
            statusText.text = statusStr;
            statusText.fontSize = 18;
            statusText.color = GetStatusColor(pptInfo.configStatus);
            statusText.alignment = TextAlignmentOptions.Center;
            FontManager.ApplyFont(statusText);

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
                case 3: return "失败";
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
                case 0: return textColor;
                case 1: return new Color(1f, 0.75f, 0.2f, 1f); // 亮橙色
                case 2: return textColor;
                case 3: return new Color(1f, 0.3f, 0.3f, 1f); // 亮红色
                default: return textColor;
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
                    prevImg.color = new Color(0.039f, 0.102f, 0.243f, 1f);
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
                    img.color = new Color(0.0196f, 0.3725f, 0.5922f, 1f);
                }
            }

            if (configButton != null) configButton.interactable = (selectedPPTItem != null);

            // 更新PPT页数显示
            if (pptPageCountLabel != null && selectedPPTItem != null && selectedPPTItem.pptInfo != null)
            {
                pptPageCountLabel.text = $"共{selectedPPTItem.pptInfo.pageCount}页";
            }

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
                    prevImg.color = new Color(0.039f, 0.102f, 0.243f, 1f);
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

        //获取ppt页数
        public int GetPptxPageCount(string pptxPath)
        {
            using (ZipArchive zip = ZipFile.OpenRead(pptxPath))
            {
                return zip.Entries.Count(e =>
                    e.FullName.StartsWith("ppt/slides/slide") &&
                    e.FullName.EndsWith(".xml"));
            }
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        void UpdateButtonStates()
        {
            // 更新删除按钮状态
            if (selectedPPTItem == null)
            {
                deleteButton.interactable = false;
                deleteButton.GetComponent<Image>().sprite = buttonBackgroundGrey;

                configButton.interactable = false;
                configButton.GetComponent<Image>().sprite = buttonBackgroundGrey;

                playButton.interactable = false;
                playButton.GetComponent<Image>().sprite = buttonBackgroundGrey;
            }
            else
            {
                deleteButton.interactable = true;
                deleteButton.GetComponent<Image>().sprite = buttonBackgroundRed;

                configButton.interactable = true;
                configButton.GetComponent<Image>().sprite = buttonBackgroundBlue;

                if (selectedPPTItem.pptInfo.configStatus == 2)
                {
                    playButton.interactable = true;
                    playButton.GetComponent<Image>().sprite = buttonBackgroundBlue;
                }
                else
                {
                    playButton.interactable = false;
                    playButton.GetComponent<Image>().sprite = buttonBackgroundGrey;
                }
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
                newInfo.configStatus = 0; // 未配置
                newInfo.pageCount = GetPptxPageCount(newFilePath);

                // 保存到JSON
                PPTDataManager.SavePPTInfoToJson(newInfo, Path.ChangeExtension(newFileName, ".json"));

                // 等待一帧后再刷新列表，避免在渲染过程中修改UI
                yield return null;
                RefreshPPTList();

                // 上传PPT到知识库
                Debug.Log($"开始上传PPT到知识库: {newFileName}");
                yield return StartCoroutine(UploadPPTToKnowledgeBase(newInfo, newFilePath));
            }

            // 文件选择完成后再次确保Unity窗口在前台
            if (hWnd != IntPtr.Zero)
            {
                SetForegroundWindow(hWnd);
            }
        }

        /// <summary>
        /// 上传PPT到知识库（调用KnowledgeBaseManager）
        /// </summary>
        private IEnumerator UploadPPTToKnowledgeBase(PPTInfo pptInfo, string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError($"文件不存在或路径为空: {filePath}");
                yield break;
            }

            Debug.Log($"[SettingsPanelUI] 调用KnowledgeBaseManager上传PPT: {filePath}");

            // 调用KnowledgeBaseManager的上传方法
            yield return StartCoroutine(KnowledgeBaseManager.UploadFileToKnowledgeBase(filePath, null, 1));

            // 上传完成后，根据上传和解析结果更新PPTInfo的状态
            // 只有上传和解析都成功时才更新状态
            if (KnowledgeBaseManager.LastUploadSuccess)
            {
                pptInfo.is_uploaded = true;
                Debug.Log($"[SettingsPanelUI] PPT上传并解析成功，更新状态: {Path.GetFileName(filePath)}");
            }
            else
            {
                pptInfo.is_uploaded = KnowledgeBaseManager.LastUploadSuccess;
                Debug.LogWarning($"[SettingsPanelUI] PPT上传或解析失败 - 上传: {KnowledgeBaseManager.LastUploadSuccess}");
            }

            // 保存更新后的PPTInfo到JSON
            string jsonFileName = Path.ChangeExtension(Path.GetFileName(filePath), ".json");
            PPTDataManager.SavePPTInfoToJson(pptInfo, jsonFileName);
            Debug.Log($"[SettingsPanelUI] PPTInfo已保存，is_uploaded: {pptInfo.is_uploaded}");

            // 刷新列表以显示更新后的状态
            yield return null;
            RefreshPPTList();
        }

        /// <summary>
        /// 删除按钮点击
        /// </summary>
        void OnDeleteClicked()
        {
            if (selectedPPTItem == null)
            {
                return;
            }

            // 显示确认对话框
            ShowDeleteConfirmDialog();
        }

        /// <summary>
        /// 显示删除确认对话框
        /// </summary>
        void ShowDeleteConfirmDialog()
        {
            if (selectedPPTItem == null) return;

            // 创建确认对话框遮罩层
            GameObject confirmOverlay = new GameObject("DeleteConfirmOverlay");
            confirmOverlay.transform.SetParent(mainPanel.transform, false);
            RectTransform overlayRect = confirmOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayBg = confirmOverlay.AddComponent<Image>();
            overlayBg.color = new Color(0f, 0f, 0f, 0.7f);

            // 创建确认对话框面板
            GameObject confirmPanel = new GameObject("ConfirmPanel");
            confirmPanel.transform.SetParent(confirmOverlay.transform, false);
            RectTransform confirmPanelRect = confirmPanel.AddComponent<RectTransform>();
            confirmPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            confirmPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            confirmPanelRect.pivot = new Vector2(0.5f, 0.5f);
            confirmPanelRect.anchoredPosition = Vector2.zero;
            confirmPanelRect.sizeDelta = new Vector2(400, 200);

            Image confirmPanelBg = confirmPanel.AddComponent<Image>();
            confirmPanelBg.sprite = Resources.Load<Sprite>("settingsBackground");

            // 创建提示文本
            GameObject messageObj = new GameObject("Message");
            messageObj.transform.SetParent(confirmPanel.transform, false);
            RectTransform messageRect = messageObj.AddComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.4f);
            messageRect.anchorMax = new Vector2(1, 0.9f);
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, -20);

            TMP_Text messageText = messageObj.AddComponent<TextMeshProUGUI>();
            messageText.text = $"确定要删除 \"{selectedPPTItem.pptInfo.filename}\" 吗？";
            messageText.fontSize = 20;
            messageText.color = textColor;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.enableWordWrapping = true;
            FontManager.ApplyFont(messageText);

            // 创建按钮容器
            GameObject buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(confirmPanel.transform, false);
            RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
            buttonContainerRect.anchorMin = new Vector2(0, 0);
            buttonContainerRect.anchorMax = new Vector2(1, 0.4f);
            buttonContainerRect.offsetMin = new Vector2(20, 20);
            buttonContainerRect.offsetMax = new Vector2(-20, 0);

            HorizontalLayoutGroup buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;
            buttonLayout.spacing = 20;
            buttonLayout.padding = new RectOffset(0, 0, 0, 0);
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            // 创建确认按钮
            Button confirmBtn = CreateButton(buttonContainer.transform, "确认删除", new Vector2(100, 50), "buttonBackgroundRed");
            confirmBtn.onClick.AddListener(() => {
                StartCoroutine(OnDeleteConfirmedCoroutine());
                Destroy(confirmOverlay);
            });

            // 创建取消按钮
            Button cancelBtn = CreateButton(buttonContainer.transform, "取消", new Vector2(100, 50), "buttonBackgroundBlue");
            cancelBtn.onClick.AddListener(() => {
                Destroy(confirmOverlay);
            });

            // 确保对话框在最上层
            confirmOverlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 显示错误提示对话框
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        void ShowErrorDialog(string errorMessage)
        {
            // 创建错误对话框遮罩层 - 直接放在parentCanvas下以置顶显示
            GameObject errorOverlay = new GameObject("ErrorDialogOverlay");
            errorOverlay.transform.SetParent(parentCanvas.transform, false);
            RectTransform overlayRect = errorOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.localScale = Vector3.one; // 确保不被缩放

            // 设置更高的sortingOrder以确保在最上层
            Canvas overlayCanvas = errorOverlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = canvasSortingOrder + 100; // 比主面板更高

            GraphicRaycaster raycaster = errorOverlay.AddComponent<GraphicRaycaster>();

            Image overlayBg = errorOverlay.AddComponent<Image>();
            overlayBg.color = new Color(0f, 0f, 0f, 0f); // 完全透明背景

            // 创建错误对话框面板 - 使用与确认删除相同的样式
            GameObject errorPanel = new GameObject("ErrorPanel");
            errorPanel.transform.SetParent(errorOverlay.transform, false);
            RectTransform errorPanelRect = errorPanel.AddComponent<RectTransform>();
            errorPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            errorPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            errorPanelRect.pivot = new Vector2(0.5f, 0.5f);
            errorPanelRect.anchoredPosition = Vector2.zero;
            errorPanelRect.sizeDelta = new Vector2(400, 200);

            Image errorPanelBg = errorPanel.AddComponent<Image>();
            errorPanelBg.sprite = Resources.Load<Sprite>("settingsBackground");

            // 创建标题文本 - 调整位置避免超出
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(errorPanel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.6f); // 从0.5调整到0.6
            titleRect.anchorMax = new Vector2(1, 0.9f);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = new Vector2(-20, -5); // 从-10调整到-5

            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "生成演讲稿失败";
            titleText.fontSize = 20; // 从22调整到20
            titleText.color = new Color(0.85f, 0.25f, 0.25f, 1f);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;
            FontManager.ApplyFont(titleText);

            // 创建错误信息文本 - 调整位置避免超出
            GameObject messageObj = new GameObject("Message");
            messageObj.transform.SetParent(errorPanel.transform, false);
            RectTransform messageRect = messageObj.AddComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0.25f); // 从0.1调整到0.25
            messageRect.anchorMax = new Vector2(1, 0.6f); // 从0.5调整到0.6
            messageRect.offsetMin = new Vector2(20, 0);
            messageRect.offsetMax = new Vector2(-20, 0);

            TMP_Text messageText = messageObj.AddComponent<TextMeshProUGUI>();
            messageText.text = errorMessage;
            messageText.fontSize = 16; // 从18调整到16
            messageText.color = textColor;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.enableWordWrapping = true;
            FontManager.ApplyFont(messageText);

            // 创建按钮容器 - 调整位置避免超出
            GameObject buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(errorPanel.transform, false);
            RectTransform buttonContainerRect = buttonContainer.AddComponent<RectTransform>();
            buttonContainerRect.anchorMin = new Vector2(0, 0);
            buttonContainerRect.anchorMax = new Vector2(1, 0.25f); // 从0.1调整到0.25
            buttonContainerRect.offsetMin = new Vector2(20, 15); // 从20调整到15
            buttonContainerRect.offsetMax = new Vector2(-20, 5); // 添加底部偏移5

            HorizontalLayoutGroup buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;
            buttonLayout.spacing = 20;
            buttonLayout.padding = new RectOffset(0, 0, 0, 0);
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;

            // 创建确定按钮 - 调整大小
            Button okBtn = CreateButton(buttonContainer.transform, "确定", new Vector2(80, 40), "buttonBackgroundRed"); // 从100x50调整到80x40
            okBtn.onClick.AddListener(() => {
                Destroy(errorOverlay);
            });

            // 确保对话框在最上层
            errorOverlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 确认删除后执行删除操作
        /// </summary>
        IEnumerator OnDeleteConfirmedCoroutine()
        {
            if (selectedPPTItem == null)
            {
                yield break;
            }

            // 等待一帧，确保不在渲染过程中
            yield return null;

            // 获取JSON文件路径
            string jsonFileName = Path.ChangeExtension(selectedPPTItem.pptInfo.filename, ".json");
            string jsonFilePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "pptinfo", jsonFileName);

            // 删除JSON文件
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    File.Delete(jsonFilePath);
                    Debug.Log($"已删除JSON文件: {jsonFilePath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"删除JSON文件失败: {e.Message}");
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning($"JSON文件不存在: {jsonFilePath}");
            }

            // 清除选中状态
            selectedPPTItem = null;

            // 等待一帧后刷新列表
            yield return null;
            RefreshPPTList();
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

            // 使用协程来处理UI操作，避免卡死
            StartCoroutine(OnConfigClickedCoroutine());
        }

        /// <summary>
        /// 配置按钮点击协程
        /// </summary>
        IEnumerator OnConfigClickedCoroutine()
        {
            if (selectedPPTItem == null)
            {
                yield break;
            }

            // 等待一帧，确保不在渲染过程中
            yield return null;

            // 显示配置面板
            if (configOverlay != null)
            {
                // 先扩展遮罩层到整个Canvas
                ExpandConfigOverlay();

                configOverlay.SetActive(true);
                
                // 再等待一帧，确保面板激活完成
                yield return null;

                // 加载当前PPT的演讲稿
                if (configInputField != null)
                {
                    if (selectedPPTItem.pptInfo.desc != null && selectedPPTItem.pptInfo.desc.Length > 0)
                    {
                        configInputField.text = string.Join("\n", selectedPPTItem.pptInfo.desc);
                    }
                    else
                    {
                        configInputField.text = "";
                    }
                    
                    // 等待一帧，确保文本设置完成
                    yield return null;

                    RefreshConfigParagraphNumbers();
                    configNumberScrollOffsetInitialized = false;

                    // 跳转到顶部
                    configInputField.ActivateInputField();
                    configInputField.MoveTextStart(false);
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

            // 查找UISetOnOff组件并调用ToggleBubbleFeature方法
            UISetOnOff uiSetOnOff = FindObjectOfType<UISetOnOff>();
            if (uiSetOnOff != null)
            {
                Debug.Log("调用UISetOnOff.ToggleBubbleFeature开始播放PPT");
                uiSetOnOff.ToggleBubbleFeature(selectedPPTItem.pptInfo.filename);
                
                // 显示PPT控制UI
                if (pptControlUI != null)
                {
                    pptControlUI.ShowControlBar();
                    Debug.Log("[SettingsPanelUI] 显示PPT控制栏");
                }
                else
                {
                    Debug.LogWarning("[SettingsPanelUI] PPTControlUI未找到,无法显示控制栏");
                }
            }
            else
            {
                Debug.LogWarning("UISetOnOff组件未找到,无法播放PPT");
                return;
            }
            
            // 关闭UI界面
            mainPanel.SetActive(false);
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

                // 根据演讲稿内容是否为空来设置状态
                bool hasContent = !string.IsNullOrWhiteSpace(configInputField.text);
                selectedPPTItem.pptInfo.configStatus = hasContent ? 2 : 3; // 2=已配置, 3=失败

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
                if (isFullscreen)
                {
                    ToggleFullscreen();
                    // 等待一帧确保全屏退出完成
                    StartCoroutine(WaitAndCloseConfig());
                }
                else
                {
                    // 直接关闭配置面板，不保存任何更改
                    if (configOverlay != null)
                    {
                        RestoreConfigOverlay();
                        configOverlay.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// 取消配置按钮点击
        /// </summary>
        void OnCancelConfig()
        {
            // 如果处于全屏模式，先退出全屏
            if (isFullscreen)
            {
                ToggleFullscreen();
                // 等待一帧确保全屏退出完成
                StartCoroutine(WaitAndCloseConfig());
            }
            else
            {
                // 直接关闭配置面板，不保存任何更改
                if (configOverlay != null)
                {
                    RestoreConfigOverlay();
                    configOverlay.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 等待一帧后关闭配置面板
        /// </summary>
        System.Collections.IEnumerator WaitAndCloseConfig()
        {
            yield return null;
            if (configOverlay != null)
            {
                RestoreConfigOverlay();
                configOverlay.SetActive(false);
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

            // 禁用生成演讲稿按钮、确认按钮和输入框
            if (generateSpeechButton != null)
            {
                generateSpeechButton.interactable = false;
                // 修改按钮文字为"生成中..."
                TMP_Text btnText = generateSpeechButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = "生成中...";
                }
            }
            
            if (confirmConfigButton != null)
            {
                confirmConfigButton.interactable = false;
            }
            
            if (cancelConfigButton != null)
            {
                cancelConfigButton.interactable = false;
            }
            
            if (configInputField != null)
            {
                configInputField.interactable = false;

                // 备份当前的演讲稿内容
                if (selectedPPTItem != null && selectedPPTItem.pptInfo != null)
                {
                    backupSpeechContent = selectedPPTItem.pptInfo.desc;
                }

                // 清空输入框
                configInputField.text = "";
                RefreshConfigParagraphNumbers();
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

            // 直接传递filename到AutoDesc
            if (autoDesc != null)
            {
                // 设置回调事件来接收生成的演讲稿
                autoDesc.OnSpeechGenerated = OnSpeechContentGenerated;
                
                // 调用AutoDesc的生成方法，直接传递filename
                autoDesc.StartGetDescProcess(selectedPPTItem.pptInfo.filename);
            }
        }

        /// <summary>
        /// 演讲稿生成完成的回调方法
        /// </summary>
        /// <param name="generatedContent">生成的演讲稿内容</param>
        /// <param name="errorMessage">错误信息，如果生成成功则为null</param>
        private void OnSpeechContentGenerated(string[] generatedContent, string errorMessage)
        {
            // 恢复生成演讲稿按钮、确认按钮和输入框的可用状态
            if (generateSpeechButton != null)
            {
                generateSpeechButton.interactable = true;
                // 恢复按钮文字为"生成演讲稿"
                TMP_Text btnText = generateSpeechButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = "生成演讲稿";
                }
            }
            
            if (confirmConfigButton != null)
            {
                confirmConfigButton.interactable = true;
            }
            
            if (cancelConfigButton != null)
            {
                cancelConfigButton.interactable = true;
            }
            
            if (configInputField != null)
            {
                configInputField.interactable = true;
            }

            // 更新配置状态并保存到JSON
            if (selectedPPTItem != null)
            {
                // 检查生成是否成功
                if (generatedContent != null && generatedContent.Length > 0 && !string.IsNullOrEmpty(generatedContent[0]))
                {
                    // 生成成功，更新UI输入框
                    if (configInputField != null)
                    {
                        configInputField.text = string.Join("\n", generatedContent);
                        RefreshConfigParagraphNumbers();
                        configNumberScrollOffsetInitialized = false;
                    }

                    // 更新PPTInfo中的desc字段
                    selectedPPTItem.pptInfo.desc = generatedContent;
                    selectedPPTItem.pptInfo.configStatus = 2; // 已配置

                    // 保存到对应的JSON文件
                    string jsonFileName = Path.ChangeExtension(selectedPPTItem.pptInfo.filename, ".json");
                    bool saveSuccess = PPTDataManager.SavePPTInfoToJson(selectedPPTItem.pptInfo, jsonFileName);

                    if (selectedPPTItem.statusText != null)
                    {
                        selectedPPTItem.statusText.text = GetStatusString(selectedPPTItem.pptInfo.configStatus);
                        selectedPPTItem.statusText.color = GetStatusColor(selectedPPTItem.pptInfo.configStatus);
                    }
                    UpdateButtonStates();
                }
                else
                {
                    // 生成失败，恢复之前的演讲稿内容
                    Debug.LogError("PPT演讲稿生成失败");

                    // 恢复UI显示
                    if (configInputField != null && backupSpeechContent != null)
                    {
                        configInputField.text = string.Join("\n", backupSpeechContent);
                        RefreshConfigParagraphNumbers();
                        configNumberScrollOffsetInitialized = false;
                    }

                    // 恢复desc字段
                    if (backupSpeechContent != null)
                    {
                        selectedPPTItem.pptInfo.desc = backupSpeechContent;
                    }

                    // 显示错误提示弹窗
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        ShowErrorDialog(errorMessage);
                    }
                    else
                    {
                        ShowErrorDialog("演讲稿生成失败，请稍后重试");
                    }
                }
            }

            if (pptLoadingIndicator != null)
                pptLoadingIndicator.SetActive(false);
        }

        private void OnConfigInputFieldValueChanged(string newText)
        {
            StartCoroutine(RefreshConfigParagraphNumbersDelayed());
            
            bool isDeleting = newText.Length < lastConfigTextLength;
            lastConfigTextLength = newText.Length;
            
            if (isDeleting)
            {
                StartCoroutine(AutoScrollToTopIfNeeded());
            }
        }
        
        private IEnumerator AutoScrollToTopIfNeeded()
        {
            if (configInputField == null || configInputField.textComponent == null || configViewportRect == null)
            {
                yield break;
            }
            TextMeshProUGUI contentText = configInputField.textComponent as TextMeshProUGUI;
            if (contentText == null)
            {
                yield break;
            }            
            float contentHeight = contentText.preferredHeight;
            float viewportHeight = configViewportRect.rect.height;
            Debug.LogWarning("contentHeight: " + contentHeight + ", viewportHeight: " + viewportHeight);
            if (contentHeight < viewportHeight)
            {
                int caretPosition = configInputField.caretPosition;
                configInputField.MoveTextStart(false);      
                Canvas.ForceUpdateCanvases();      
                configInputField.caretPosition = caretPosition;
            }
        }
        
        private IEnumerator RefreshConfigParagraphNumbersDelayed()
        {
            yield return null;
            yield return null;
            RefreshConfigParagraphNumbers();
        }

        private void RefreshConfigParagraphNumbers()
        {
            if (configParagraphNumberText == null || configInputField == null || configInputField.textComponent == null)
            {
                return;
            }

            TextMeshProUGUI contentText = configInputField.textComponent as TextMeshProUGUI;
            if (contentText == null)
            {
                return;
            }

            string text = configInputField.text;
            if (string.IsNullOrEmpty(text))
            {
                configParagraphNumberText.text = "";
                return;
            }

            contentText.ForceMeshUpdate();

            string[] paragraphs = text.Split('\n');
            int paragraphCount = paragraphs.Length;

            if (paragraphCount == 0)
            {
                configParagraphNumberText.text = "";
                return;
            }

            TMP_TextInfo textInfo = contentText.textInfo;
            if (textInfo == null || textInfo.lineCount == 0)
            {
                configParagraphNumberText.text = "";
                return;
            }

            StringBuilder numberBuilder = new StringBuilder();
            int currentCharIndex = 0;

            for (int i = 0; i < paragraphCount; i++)
            {
                string paragraph = paragraphs[i];
                
                if (currentCharIndex < textInfo.characterCount)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[currentCharIndex];
                    int lineIndex = charInfo.lineNumber;
                    
                    if (lineIndex >= 0 && lineIndex < textInfo.lineCount)
                    {
                        TMP_LineInfo lineInfo = textInfo.lineInfo[lineIndex];
                        float lineHeight = lineInfo.lineHeight;
                        
                        numberBuilder.Append((i + 1).ToString());
                        
                        if (i < paragraphCount - 1)
                        {
                            int paragraphLength = paragraph.Length;
                            int lineCount = 0;
                            
                            for (int charIdx = currentCharIndex; charIdx < currentCharIndex + paragraphLength && charIdx < textInfo.characterCount; charIdx++)
                            {
                                if (charIdx > currentCharIndex && textInfo.characterInfo[charIdx].lineNumber != textInfo.characterInfo[charIdx - 1].lineNumber)
                                {
                                    lineCount++;
                                }
                            }
                            
                            for (int j = 0; j < lineCount; j++)
                            {
                                numberBuilder.Append("\n");
                            }
                            
                            numberBuilder.Append("\n");
                        }
                    }
                }
                else
                {
                    numberBuilder.Append((i + 1).ToString());
                    if (i < paragraphCount - 1)
                    {
                        numberBuilder.Append("\n");
                    }
                }
                
                currentCharIndex += paragraph.Length + 1;
            }

            configParagraphNumberText.text = numberBuilder.ToString();
        }

        /// <summary>
        /// 切换全屏模式
        /// </summary>
        void ToggleFullscreen()
        {
            isFullscreen = !isFullscreen;

            if (isFullscreen)
            {
                StartCoroutine(EnterFullscreen());
                // 更改按钮图标为shrink
                if (configFullscreenButton != null)
                {
                    configFullscreenButton.GetComponent<Image>().sprite = Resources.Load<Sprite>("shrink");
                }
            }
            else
            {
                StartCoroutine(ExitFullscreen());
                // 恢复按钮图标为全屏
                if (configFullscreenButton != null)
                {
                    configFullscreenButton.GetComponent<Image>().sprite = Resources.Load<Sprite>("fullscreen");
                }
            }
        }

        /// <summary>
        /// 进入全屏模式
        /// </summary>
        private IEnumerator EnterFullscreen()
        {
            if (configPanel == null) yield break;

            RectTransform panelRect = configPanel.GetComponent<RectTransform>();
            if (panelRect == null) yield break;

           
            // 保存原始状态（在第一次进入全屏时）
            if (fullscreenOriginalParent == null)
            {
                fullscreenOriginalParent = panelRect.parent;
            }
            fullscreenOriginalSiblingIndex = panelRect.GetSiblingIndex();
            fullscreenOriginalAnchorMin = panelRect.anchorMin;
            fullscreenOriginalAnchorMax = panelRect.anchorMax;
            fullscreenOriginalOffsetMin = panelRect.offsetMin;
            fullscreenOriginalOffsetMax = panelRect.offsetMax;

            configInputField.ActivateInputField();
            configInputField.MoveTextStart(false);      
            Canvas.ForceUpdateCanvases(); 
            yield return null;
            yield return null;

            // 设置为全屏 - 占满整个Canvas
            panelRect.SetParent(parentCanvas.transform, true);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.localScale = Vector3.one;

            // 将面板置于最上层
            panelRect.SetAsLastSibling();

            // 刷新行号
            StartCoroutine(RefreshConfigParagraphNumbersDelayed());
        }

        /// <summary>
        /// 退出全屏模式
        /// </summary>
        private IEnumerator ExitFullscreen()
        {
            if (configPanel == null || fullscreenOriginalParent == null) yield break;

            RectTransform panelRect = configPanel.GetComponent<RectTransform>();
            if (panelRect == null) yield break;


            configInputField.ActivateInputField();
            configInputField.MoveTextStart(false);
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
            // 恢复原始状态
            panelRect.SetParent(fullscreenOriginalParent, true);
            panelRect.anchorMin = fullscreenOriginalAnchorMin;
            panelRect.anchorMax = fullscreenOriginalAnchorMax;
            panelRect.offsetMin = fullscreenOriginalOffsetMin;
            panelRect.offsetMax = fullscreenOriginalOffsetMax;

            // 确保设置正确的位置
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localScale = Vector3.one;

            // 刷新行号
            StartCoroutine(RefreshConfigParagraphNumbersDelayed());
        }

        private void SyncConfigParagraphNumberScroll()
        {
            if (configParagraphNumberText == null || configInputField == null || configInputField.textComponent == null)
            {
                return;
            }

            TextMeshProUGUI contentText = configInputField.textComponent as TextMeshProUGUI;
            if (contentText == null)
            {
                return;
            }

            RectTransform numberRect = configParagraphNumberText.rectTransform;
            RectTransform contentRect = contentText.rectTransform;

            Vector2 viewportSize = configViewportRect != null ? configViewportRect.rect.size : Vector2.zero;
            float canvasScale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;

            if (!configNumberScrollOffsetInitialized || viewportSize != configNumberScrollLastViewportSize || !Mathf.Approximately(canvasScale, configNumberScrollLastCanvasScaleFactor))
            {
                configNumberScrollBaseAnchoredPos = numberRect.anchoredPosition;
                configContentScrollBaseAnchoredPos = contentRect.anchoredPosition;
                configNumberScrollLastViewportSize = viewportSize;
                configNumberScrollLastCanvasScaleFactor = canvasScale;
                configNumberScrollOffsetInitialized = true;
            }

            float deltaY = contentRect.anchoredPosition.y - configContentScrollBaseAnchoredPos.y;
            Vector2 numberPos = numberRect.anchoredPosition;
            numberPos.y = configNumberScrollBaseAnchoredPos.y + deltaY;
            numberRect.anchoredPosition = numberPos;
        }

        /// <summary>
        /// 扩大configOverlay的遮罩范围,使其超出mainPanel的边界
        /// </summary>
        private void ExpandConfigOverlay()
        {
            if (configOverlayRect == null || configOverlayExpanded)
                return;

            // 备份configOverlay的原始父对象和位置
            overlayOriginalParent = configOverlay.transform.parent;
            overlayOriginalSiblingIndex = configOverlay.transform.GetSiblingIndex();

            // 将configOverlay临时提升到parentCanvas,使其可以覆盖整个Canvas
            configOverlay.transform.SetParent(parentCanvas.transform, false);

            // 设置为覆盖整个Canvas
            configOverlayRect.anchorMin = Vector2.zero;
            configOverlayRect.anchorMax = Vector2.one;
            configOverlayRect.sizeDelta = Vector2.zero;
            configOverlayRect.anchoredPosition = Vector2.zero;

            configOverlayExpanded = true;

            Debug.Log("[SettingsPanel] ConfigOverlay已扩展到整个Canvas");
        }

        /// <summary>
        /// 恢复configOverlay的原始遮罩范围
        /// </summary>
        private void RestoreConfigOverlay()
        {
            if (configOverlayRect == null || !configOverlayExpanded)
                return;

            // 恢复到mainPanel下的原始位置
            configOverlay.transform.SetParent(overlayOriginalParent, false);
            configOverlay.transform.SetSiblingIndex(overlayOriginalSiblingIndex);

            // 恢复原始anchor
            configOverlayRect.anchorMin = configOverlayOriginalAnchorMin;
            configOverlayRect.anchorMax = configOverlayOriginalAnchorMax;
            configOverlayRect.offsetMin = Vector2.zero;
            configOverlayRect.offsetMax = Vector2.zero;

            configOverlayExpanded = false;

            Debug.Log("[SettingsPanel] ConfigOverlay已恢复到原始范围");
        }
    }
}

