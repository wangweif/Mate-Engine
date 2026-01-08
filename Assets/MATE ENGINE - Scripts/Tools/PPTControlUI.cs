using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using MateEngine.PPT;
using MATE_ENGINE___Scripts.Tools;
using UnityEngine.EventSystems;

/// <summary>
/// PPT控制UI组件
/// 提供浮动控制栏用于控制PPT播放、翻页、静音等功能
/// 基于PPTService架构
/// </summary>
public class PPTControlUI : MonoBehaviour
{
    [Header("引用")]
    public Canvas parentCanvas;
    public PPTService pptService;
    public UISetOnOff uiSetOnOff;
    private VRMLoader vrmLoader;

    [Header("UI元素")]
    private GameObject controlBar;
    private GameObject topDecoration;
    private Button playPauseButton;
    private Button closeButton;
    private Button previousButton;
    private Button nextButton;
    private Button muteButton;
    private Button avatarToggleButton;
    private TMP_Text pageDisplay;
    
    // 按钮图标Image
    private Image playPauseIcon;
    private Image closeIcon;
    private Image previousIcon;
    private Image nextIcon;
    private Image muteIcon;
    private Image avatarToggleIcon;
    
    // 图标Sprite资源
    private Sprite playSprite;
    private Sprite pauseSprite;
    private Sprite closeSprite;
    private Sprite previousSprite;
    private Sprite nextSprite;
    private Sprite volumeSprite;
    private Sprite muteSprite;
    private Sprite userSprite;
    private Sprite hideUserSprite;
    
    // 数字人显示状态
    private bool isAvatarVisible = true;

    private bool isVisible = false;
    private bool isMuted = false;
    
    [Header("UI 风格配置")]
    public Color panelBackgroundColor = new Color(16f / 255f, 38f / 255f, 77f / 255f, 0.98f);
    public Color accentColor = new Color(1f, 1f, 1f, 1f);

    void Start()
    {
        // 查找Canvas
        if (parentCanvas == null)
        {
            parentCanvas = FindObjectOfType<Canvas>();
        }

        // 查找PPTService
        if (pptService == null)
        {
            pptService = PPTService.Instance;
            if (pptService == null)
            {
                pptService = FindObjectOfType<PPTService>();
            }
        }

        // 查找UISetOnOff
        if (uiSetOnOff == null)
        {
            uiSetOnOff = FindObjectOfType<UISetOnOff>();
        }
        
        // 主动注册到UISetOnOff
        if (uiSetOnOff != null)
        {
            uiSetOnOff.pptControlUI = this;
            Debug.Log("[PPTControlUI] 已主动注册到UISetOnOff");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] UISetOnOff未找到,无法注册");
        }
        
        // 查找VRMLoader
        if (vrmLoader == null)
        {
            vrmLoader = FindObjectOfType<VRMLoader>();
            if (vrmLoader != null)
            {
                Debug.Log("[PPTControlUI] 已找到VRMLoader组件");
            }
        }

        // 加载图标资源
        LoadIconSprites();
        
        // 创建UI
        CreateControlBar();

        // 订阅PPTService事件
        if (pptService != null)
        {
            pptService.OnSlideChanged += OnSlideChanged;
            pptService.OnPresentationClosed += OnPresentationClosed;
            Debug.Log("[PPTControlUI] 已订阅PPTService事件");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] PPTService未找到");
        }

        // 默认隐藏
        HideControlBar();
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (pptService != null)
        {
            pptService.OnSlideChanged -= OnSlideChanged;
            pptService.OnPresentationClosed -= OnPresentationClosed;
        }
    }

    /// <summary>
    /// 加载图标Sprite资源
    /// </summary>
    void LoadIconSprites()
    {
        // 使用Resources.Load加载资源(支持运行时)
        playSprite = Resources.Load<Sprite>("PPTIcons/播放");
        pauseSprite = Resources.Load<Sprite>("PPTIcons/暂停");
        closeSprite = Resources.Load<Sprite>("PPTIcons/关闭");
        previousSprite = Resources.Load<Sprite>("PPTIcons/上一页");
        nextSprite = Resources.Load<Sprite>("PPTIcons/下一页");
        volumeSprite = Resources.Load<Sprite>("PPTIcons/声音");
        muteSprite = Resources.Load<Sprite>("PPTIcons/静音");
        userSprite = Resources.Load<Sprite>("PPTIcons/显示-人");
        hideUserSprite = Resources.Load<Sprite>("PPTIcons/隐藏-人");
        
        // 检查加载结果
        if (playSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载播放图标");
        if (pauseSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载暂停图标");
        if (closeSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载关闭图标");
        if (previousSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载上一页图标");
        if (nextSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载下一页图标");
        if (volumeSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载声音图标");
        if (muteSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载静音图标");
        if (userSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载显示-人图标");
        if (hideUserSprite == null) Debug.LogWarning("[PPTControlUI] 未能加载隐藏-人图标");
    }

    /// <summary>
    /// 创建控制栏UI
    /// </summary>
    void CreateControlBar()
    {
        // 创建主容器
        controlBar = new GameObject("PPTControlBar");
        controlBar.transform.SetParent(parentCanvas.transform, false);

        RectTransform barRect = controlBar.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0, 50); // 稍微抬高一点，不贴边
        barRect.sizeDelta = new Vector2(520, 75); // 增加尺寸以容纳所有按钮

        // 1. 背景处理：使用深色半透明（毛玻璃感）
        Image bgImage = controlBar.AddComponent<Image>();
        // bgImage.sprite = Resources.Load<Sprite>("PPTIcons/边框");
        // bgImage.type = Image.Type.Sliced;
        bgImage.color = panelBackgroundColor;
        bgImage.raycastTarget = true;
        
        // // 2. 添加外边框 (描边) 增加精致感
        // Outline outline = controlBar.AddComponent<Outline>();
        // outline.effectColor = new Color(1, 1, 1, 0.15f);
        // outline.effectDistance = new Vector2(1, -1);

        // 2. 添加上边装饰图片(在布局组件之前创建)
        CreateTopDecoration(controlBar.transform);

        // 3. 布局微调
        HorizontalLayoutGroup layout = controlBar.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12;
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        // 创建按钮和显示元素(使用PNG图标)
        playPauseButton = CreateImageButton(controlBar.transform, "PlayPause", playSprite, OnPlayPauseClicked);
        closeButton = CreateImageButton(controlBar.transform, "Close", closeSprite, OnCloseClicked);
        previousButton = CreateImageButton(controlBar.transform, "Previous", previousSprite, OnPreviousSlideClicked);
        
        // 页码显示
        pageDisplay = CreatePageDisplay(controlBar.transform);
        
        nextButton = CreateImageButton(controlBar.transform, "Next", nextSprite, OnNextSlideClicked);
        muteButton = CreateImageButton(controlBar.transform, "Mute", volumeSprite, OnMuteClicked);
        
        // 数字人显示/隐藏按钮
        avatarToggleButton = CreateImageButton(controlBar.transform, "AvatarToggle", userSprite, OnAvatarToggleClicked);

        // 初始化显示
        UpdatePageDisplay(1, 0);
    }

    /// <summary>
    /// 创建图标按钮(使用Image)
    /// </summary>
    Button CreateImageButton(Transform parent, string name, Sprite iconSprite, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject($"Button_{name}");
        btnObj.transform.SetParent(parent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(55, 55); // 按钮大小

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(1f, 1f, 1f, 0f); // 保持背景透明

        Button btn = btnObj.AddComponent<Button>();
        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        // 设置按钮过渡颜色:悬停时微亮
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(1, 1, 1, 0.1f);
        colors.pressedColor = new Color(1, 1, 1, 0.2f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        btn.colors = colors;
        btn.transition = Selectable.Transition.ColorTint;
        
        // 添加悬停缩放脚本 (动态添加)
        btnObj.AddComponent<UIPointerAnimation>();

        // 图标Image
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(btnObj.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        
        // 根据按钮类型设置图标尺寸
        // 上一页/下一页: 16x28px, 其它: 30x28px
        if (name == "Previous" || name == "Next")
        {
            iconRect.sizeDelta = new Vector2(15, 28);
        }
        else
        {
            iconRect.sizeDelta = new Vector2(30, 28);
        }

        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.color = accentColor; // 统一图标颜色为白色
        iconImage.raycastTarget = false;

        // 保存图标Image引用
        if (name == "PlayPause")
            playPauseIcon = iconImage;
        else if (name == "Close")
            closeIcon = iconImage;
        else if (name == "Previous")
            previousIcon = iconImage;
        else if (name == "Next")
            nextIcon = iconImage;
        else if (name == "Mute")
            muteIcon = iconImage;
        else if (name == "AvatarToggle")
            avatarToggleIcon = iconImage;

        return btn;
    }

    /// <summary>
    /// 创建页码显示
    /// </summary>
    TMP_Text CreatePageDisplay(Transform parent)
    {
        GameObject displayObj = new GameObject("PageDisplay");
        displayObj.transform.SetParent(parent, false);

        RectTransform displayRect = displayObj.AddComponent<RectTransform>();
        displayRect.sizeDelta = new Vector2(80, 55);
        
        // 添加 LayoutElement 确保布局控制
        LayoutElement layoutElement = displayObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 80;
        layoutElement.preferredHeight = 55;
        layoutElement.flexibleWidth = 0;
        layoutElement.flexibleHeight = 0;

        TMP_Text displayText = displayObj.AddComponent<TextMeshProUGUI>();
        displayText.text = "1 / 4";
        displayText.fontSize = 18;
        displayText.color = Color.white;
        displayText.alignment = TextAlignmentOptions.Center;
        displayText.fontStyle = FontStyles.Normal;
        displayText.enableAutoSizing = true;
        displayText.fontSizeMin = 14;
        displayText.fontSizeMax = 18;
        FontManager.ApplyFont(displayText);

        return displayText;
    }

    /// <summary>
    /// 创建顶部装饰图片
    /// </summary>
    void CreateTopDecoration(Transform parent)
    {
        topDecoration = new GameObject("TopDecoration");
        topDecoration.transform.SetParent(parent, false);

        RectTransform decoRect = topDecoration.AddComponent<RectTransform>();
        decoRect.anchorMin = new Vector2(0f, 1f);
        decoRect.anchorMax = new Vector2(1f, 1f);
        decoRect.pivot = new Vector2(0.5f, 1f);
        decoRect.anchoredPosition = new Vector2(0, 0);
        
        // 添加LayoutElement并设置ignoreLayout,使其不受HorizontalLayoutGroup影响
        LayoutElement layoutElement = topDecoration.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        
        // 加载上边图片
        Sprite topSprite = Resources.Load<Sprite>("PPTIcons/上边");
        if (topSprite != null)
        {
            // 增加高度使其更明显
            float displayHeight = 4f;
            decoRect.sizeDelta = new Vector2(0, displayHeight);
            
            Image decoImage = topDecoration.AddComponent<Image>();
            decoImage.sprite = topSprite;
            decoImage.raycastTarget = false;
            // 使用Simple模式,按比例缩放
            decoImage.type = Image.Type.Simple;
            decoImage.preserveAspect = false; // 不保持宽高比,拉伸填充
            
            Debug.Log($"[PPTControlUI] 上边装饰图片已加载,原始尺寸: {topSprite.rect.width}x{topSprite.rect.height}, 显示高度: {displayHeight}px");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] 未能加载上边装饰图片");
        }
    }

    /// <summary>
    /// 显示控制栏
    /// </summary>
    public void ShowControlBar()
    {
        if (controlBar != null)
        {
            controlBar.SetActive(true);
            isVisible = true;
            
            // 更新页码显示
            if (pptService != null)
            {
                int current = pptService.GetCurrentSlide();
                int total = pptService.GetTotalSlides();
                UpdatePageDisplay(current, total);
            }
            
            Debug.Log("[PPTControlUI] 显示控制栏");
        }
    }

    /// <summary>
    /// 隐藏控制栏
    /// </summary>
    public void HideControlBar()
    {
        if (controlBar != null)
        {
            controlBar.SetActive(false);
            isVisible = false;
            Debug.Log("[PPTControlUI] 隐藏控制栏");
        }
    }

    /// <summary>
    /// 切换控制栏显示状态
    /// </summary>
    public void ToggleControlBar()
    {
        if (isVisible)
            HideControlBar();
        else
            ShowControlBar();
    }

    /// <summary>
    /// 播放/暂停按钮点击
    /// </summary>
    void OnPlayPauseClicked()
    {
        if (uiSetOnOff != null)
        {
            uiSetOnOff.ToggleBubbleFeature();
            Debug.Log("[PPTControlUI] 切换播放/暂停");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] UISetOnOff未找到");
        }
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    void OnCloseClicked()
    {
        Debug.Log("[PPTControlUI] 关闭按钮被点击");
        
        // 【新增】立即强制停止所有音频播放
        if (uiSetOnOff != null)
        {
            uiSetOnOff.StopPresentation(); // 停止演示协程和音频
            Debug.Log("[PPTControlUI] 已立即停止演示和音频");
        }
        
        // 1. 关闭PPT演示文稿
        if (pptService != null)
        {
            pptService.ClosePresentation();
            Debug.Log("[PPTControlUI] 已发送关闭PPT命令");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] PPTService未找到,无法关闭PPT");
        }
        
        // 2. 恢复数字人显示状态
        if (!isAvatarVisible)
        {
            isAvatarVisible = true;
            ToggleAvatarVisibility(true);
            UpdateAvatarToggleButton(true);
            Debug.Log("[PPTControlUI] 已恢复数字人显示");
        }
        
        // 3. 恢复音量状态(取消静音)
        if (isMuted)
        {
            isMuted = false;
            UpdateMuteButton(false);
            
            if (uiSetOnOff != null && uiSetOnOff.windowsTTS != null)
            {
                uiSetOnOff.windowsTTS.SetVolume(1f);
                Debug.Log("[PPTControlUI] 已恢复音量");
            }
        }
        
        // 4. 隐藏控制栏
        HideControlBar();
        Debug.Log("[PPTControlUI] 关闭操作完成");
    }

    /// <summary>
    /// 上一页按钮点击
    /// </summary>
    void OnPreviousSlideClicked()
    {
        if (pptService != null)
        {
            int current = pptService.GetCurrentSlide();
            int total = pptService.GetTotalSlides();
            
            Debug.Log($"[PPTControlUI] 上一页点击 - 当前: {current}/{total}");
            
            if (current <= 1)
            {
                // 循环到最后一页
                pptService.GoToSlide(total);
                Debug.Log($"[PPTControlUI] 循环到最后一页: {total}");
            }
            else
            {
                pptService.PreviousSlide();
                Debug.Log($"[PPTControlUI] 上一页 -> {current - 1}");
            }
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] PPTService未找到");
        }
    }

    /// <summary>
    /// 下一页按钮点击
    /// </summary>
    void OnNextSlideClicked()
    {
        if (pptService != null)
        {
            int current = pptService.GetCurrentSlide();
            int total = pptService.GetTotalSlides();
            
            Debug.Log($"[PPTControlUI] 下一页点击 - 当前: {current}/{total}");
            
            if (current >= total)
            {
                // 循环到第一页
                pptService.GoToSlide(1);
                Debug.Log("[PPTControlUI] 循环到第一页");
            }
            else
            {
                pptService.NextSlide();
                Debug.Log($"[PPTControlUI] 下一页 -> {current + 1}");
            }
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] PPTService未找到");
        }
    }

    /// <summary>
    /// 静音按钮点击
    /// </summary>
    void OnMuteClicked()
    {
        isMuted = !isMuted;
        UpdateMuteButton(isMuted);
        
        // 实现实际的静音功能
        if (uiSetOnOff != null && uiSetOnOff.windowsTTS != null)
        {
            // 通过AudioCacheManager的AudioSource控制音量
            uiSetOnOff.windowsTTS.SetVolume(isMuted ? 0f : 1f);
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] 无法控制音量,UISetOnOff或WindowsTTS未找到");
        }
    }
    
    /// <summary>
    /// 数字人显示/隐藏按钮点击
    /// </summary>
    void OnAvatarToggleClicked()
    {
        if (vrmLoader == null)
        {
            vrmLoader = FindObjectOfType<VRMLoader>();
            if (vrmLoader == null)
            {
                Debug.LogWarning("[PPTControlUI] VRMLoader未找到,无法切换数字人显示");
                return;
            }
        }
        
        isAvatarVisible = !isAvatarVisible;
        ToggleAvatarVisibility(isAvatarVisible);
        UpdateAvatarToggleButton(isAvatarVisible);
        Debug.Log($"[PPTControlUI] 数字人显示状态: {isAvatarVisible}");
    }
    
    /// <summary>
    /// 切换数字人显示/隐藏(仅控制当前加载的自定义VRM模型)
    /// </summary>
    void ToggleAvatarVisibility(bool visible)
    {
        if (vrmLoader == null) return;
        
        // 获取当前加载的自定义VRM模型
        GameObject currentModel = vrmLoader.GetCurrentModel();
        
        // 只控制当前自定义模型,不影响默认小女孩模型
        if (currentModel != null)
        {
            currentModel.SetActive(visible);
            Debug.Log($"[PPTControlUI] 已{(visible ? "显示" : "隐藏")}自定义VRM模型: {currentModel.name}");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] 当前没有加载自定义VRM模型");
        }
    }

    /// <summary>
    /// PPT页码变化事件处理
    /// </summary>
    void OnSlideChanged(int slideNum)
    {
        if (pptService != null)
        {
            int total = pptService.GetTotalSlides();
            UpdatePageDisplay(slideNum, total);
            Debug.Log($"[PPTControlUI] 页码变化事件 - 更新为: {slideNum}/{total}");
        }
        else
        {
            Debug.LogWarning("[PPTControlUI] OnSlideChanged - PPTService为null");
        }
    }

    /// <summary>
    /// PPT关闭事件处理
    /// </summary>
    void OnPresentationClosed()
    {
        HideControlBar();
        Debug.Log("[PPTControlUI] PPT关闭,隐藏控制栏");
        
        // PPT退出时恢复模型显示
        if (!isAvatarVisible)
        {
            isAvatarVisible = true;
            ToggleAvatarVisibility(true);
            UpdateAvatarToggleButton(true); // 同步更新按钮图标
            Debug.Log("[PPTControlUI] PPT退出,已恢复模型显示并更新图标");
        }
        
        // PPT退出时恢复音量到默认状态(非静音)
        if (isMuted)
        {
            isMuted = false;
            UpdateMuteButton(false);
            
            // 恢复音量
            if (uiSetOnOff != null && uiSetOnOff.windowsTTS != null)
            {
                uiSetOnOff.windowsTTS.SetVolume(1f);
                Debug.Log("[PPTControlUI] PPT退出,已恢复音量到默认状态");
            }
        }
    }

    /// <summary>
    /// 更新页码显示
    /// </summary>
    void UpdatePageDisplay(int current, int total)
    {
        if (pageDisplay != null)
        {
            if (total > 0)
            {
                pageDisplay.text = $"{current} / {total}";
            }
            else
            {
                pageDisplay.text = "- / -";
            }
        }
    }

    /// <summary>
    /// 更新播放/暂停按钮图标
    /// </summary>
    public void UpdatePlayPauseButton(bool isPlaying)
    {
        if (playPauseIcon != null)
        {
            playPauseIcon.sprite = isPlaying ? pauseSprite : playSprite;
        }
    }

    /// <summary>
    /// 更新静音按钮图标
    /// </summary>
    void UpdateMuteButton(bool isMuted)
    {
        if (muteIcon != null)
        {
            muteIcon.sprite = isMuted ? muteSprite : volumeSprite;
        }
    }
    
    /// <summary>
    /// 更新VRM显示/隐藏按钮图标
    /// </summary>
    void UpdateAvatarToggleButton(bool isVisible)
    {
        if (avatarToggleIcon != null)
        {
            // isVisible=true(显示中) → 显示用户图标
            // isVisible=false(隐藏中) → 显示隐藏图标
            avatarToggleIcon.sprite = isVisible ? userSprite : hideUserSprite;
            Debug.Log($"[PPTControlUI] 更新VRM按钮图标: {(isVisible ? "显示" : "隐藏")}");
        }
    }
}

/// <summary>
/// 简单的辅助脚本：处理悬停时的微缩放动效
/// </summary>
public class UIPointerAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    
    void Awake()
    {
        originalScale = transform.localScale;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * 1.15f; // 悬停放大 15%
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }
}
