using Kirurobo;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsHandlerPPT : MonoBehaviour
{
    [Header("SettingsMenuCanvas Reference")]
    public Canvas settingsMenuCanvas;  // SettingsMenuCanvas的引用

    [Header("PPT UI Components")]
    public GameObject pptUIContainer;  // PPT功能的UI容器
    public Toggle enablePPTToggle;     // PPT开关按钮
    public TextMeshProUGUI displayText;  // PPT状态文字
    public TextMeshProUGUI titleLabel;    // PPT功能标题（可选）

    [Header("Display Settings")]
    public string displayMessage = "PPT控制功能";     // 显示文字内容
    public string titleText = "PPT远程控制";         // 标题文字
    public Color textColor = Color.white;             // 启用状态文字颜色
    public Color disabledTextColor = Color.gray;      // 禁用状态文字颜色
    public Color titleColor = Color.cyan;             // 标题颜色

    void Start()
    {
        // 如果没有手动设置SettingsMenuCanvas引用，自动查找
        if (settingsMenuCanvas == null)
        {
            settingsMenuCanvas = GameObject.Find("SettingsMenuCanvas")?.GetComponent<Canvas>();
        }

        // 创建或查找PPT UI组件
        CreateOrUpdatePPTUI();

        enablePPTToggle?.onValueChanged.AddListener(OnEnablePPTChanged);
        LoadSettings();
        ApplySettings();

        // 确保UI组件在游戏开始时正确显示
        EnsureUIComponentsVisible();
    }

    // 创建或更新PPT UI组件
    private void CreateOrUpdatePPTUI()
    {
        if (settingsMenuCanvas == null)
        {
            Debug.LogWarning("SettingsMenuCanvas未找到！");
            return;
        }

        // 如果没有指定UI容器，尝试查找或创建
        if (pptUIContainer == null)
        {
            // 尝试查找现有的PPT UI容器
            pptUIContainer = settingsMenuCanvas.transform.Find("PPTUIContainer")?.gameObject;

            if (pptUIContainer == null)
            {
                // 如果找不到，创建新的UI容器
                CreatePPTUIContainer();
            }
        }
    }

    // 创建PPT UI容器和组件
    private void CreatePPTUIContainer()
    {
        // 创建父容器
        pptUIContainer = new GameObject("PPTUIContainer");
        pptUIContainer.transform.SetParent(settingsMenuCanvas.transform, false);

        // 添加垂直布局组件
        VerticalLayoutGroup layoutGroup = pptUIContainer.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.spacing = 10f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);

        // 添加Content Size Fitter组件
        ContentSizeFitter sizeFitter = pptUIContainer.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 创建标题文本
        CreateTitleLabel();

        // 创建状态文字
        CreateDisplayText();

        // 创建开关按钮
        CreateToggle();

        Debug.Log("PPT UI组件已创建在SettingsMenuCanvas上");
    }

    // 创建标题
    private void CreateTitleLabel()
    {
        GameObject titleObj = new GameObject("PPTTitleLabel");
        titleObj.transform.SetParent(pptUIContainer.transform, false);

        titleLabel = titleObj.AddComponent<TextMeshProUGUI>();
        titleLabel.text = titleText;
        titleLabel.fontSize = 16;
        titleLabel.color = titleColor;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.alignment = TextAlignmentOptions.Center;
    }

    // 创建状态文字
    private void CreateDisplayText()
    {
        GameObject textObj = new GameObject("PPTStatusText");
        textObj.transform.SetParent(pptUIContainer.transform, false);

        displayText = textObj.AddComponent<TextMeshProUGUI>();
        displayText.fontSize = 14;
        displayText.color = textColor;
        displayText.alignment = TextAlignmentOptions.Center;
    }

    // 创建开关按钮
    private void CreateToggle()
    {
        GameObject toggleObj = new GameObject("PPTToggle");
        toggleObj.transform.SetParent(pptUIContainer.transform, false);

        // 添加Toggle组件
        enablePPTToggle = toggleObj.AddComponent<Toggle>();

        // 创建背景
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(toggleObj.transform, false);
        Image background = backgroundObj.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        enablePPTToggle.targetGraphic = background;

        // 创建勾选标记
        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(toggleObj.transform, false);
        Image checkmark = checkmarkObj.AddComponent<Image>();
        checkmark.color = Color.green;
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        enablePPTToggle.graphic = checkmark;

        // 创建标签
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleObj.transform, false);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "启用PPT控制";
        label.fontSize = 12;
        label.color = Color.white;
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(30, 0);
        labelRect.offsetMax = Vector2.zero;
        enablePPTToggle.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 30);
    }

    private void OnEnablePPTChanged(bool value)
    {
        SaveLoadHandler.Instance.data.enablePPT = value;
        ApplySettings();
        Save();
    }

    public void LoadSettings()
    {
        enablePPTToggle?.SetIsOnWithoutNotify(SaveLoadHandler.Instance.data.enablePPT);
    }

    public void ApplySettings()
    {
        // 应用设置到游戏逻辑
        var data = SaveLoadHandler.Instance.data;

        // 更新文字显示
        UpdateDisplayText(data.enablePPT);

        // 在这里可以添加PPT功能的具体逻辑
        // 例如：FindFirstObjectByType<PPTController>().enabled = data.enablePPT;
    }

    public void ResetToDefaults()
    {
        enablePPTToggle?.SetIsOnWithoutNotify(true); // 默认值
        SaveLoadHandler.Instance.data.enablePPT = true;
        UpdateDisplayText(true);
    }

    private void UpdateDisplayText(bool pptEnabled)
    {
        if (displayText != null)
        {
            if (pptEnabled)
            {
                displayText.text = displayMessage;
                displayText.color = textColor;
                displayText.gameObject.SetActive(true);
            }
            else
            {
                displayText.text = "PPT功能已禁用";
                displayText.color = disabledTextColor;
                displayText.gameObject.SetActive(true);
            }
        }

        // 确保按钮始终显示
        if (enablePPTToggle != null)
        {
            enablePPTToggle.gameObject.SetActive(true);
        }

        // PPT UI容器已经在上面处理了
    }

    // 确保UI组件在设置菜单中可见
    private void EnsureUIComponentsVisible()
    {
        // 确保PPT UI容器可见
        if (pptUIContainer != null)
        {
            pptUIContainer.SetActive(true);

            // 设置容器在画布上的位置（右上角）
            RectTransform containerRect = pptUIContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = new Vector2(1, 1); // 右上角锚点
                containerRect.anchorMax = new Vector2(1, 1);
                containerRect.pivot = new Vector2(1, 1);    // 右上角轴心
                containerRect.anchoredPosition = new Vector2(-50, -50); // 距离右边和顶部50像素
            }
        }

        // 确保文字显示
        if (displayText != null)
        {
            displayText.gameObject.SetActive(true);
        }

        // 确保按钮显示
        if (enablePPTToggle != null)
        {
            enablePPTToggle.gameObject.SetActive(true);
        }

        // 确保标题显示
        if (titleLabel != null)
        {
            titleLabel.gameObject.SetActive(true);
        }
    }

    // 公共方法，用于从外部更新显示文字
    public void SetDisplayMessage(string message)
    {
        displayMessage = message;
        // 即使在禁用状态下也会更新文字，但会根据当前状态显示
        UpdateDisplayText(SaveLoadHandler.Instance.data.enablePPT);
    }

    // 公共方法，用于更新文字颜色
    public void SetTextColor(Color color)
    {
        textColor = color;
        if (displayText != null && SaveLoadHandler.Instance.data.enablePPT)
        {
            displayText.color = textColor;
        }
    }

    // 公共方法，用于更新禁用状态的文字颜色
    public void SetDisabledTextColor(Color color)
    {
        disabledTextColor = color;
        if (displayText != null && !SaveLoadHandler.Instance.data.enablePPT)
        {
            displayText.color = disabledTextColor;
        }
    }

    // 公共方法，强制刷新UI显示
    public void RefreshUI()
    {
        UpdateDisplayText(SaveLoadHandler.Instance.data.enablePPT);
        EnsureUIComponentsVisible();
    }

    private void Save()
    {
        SaveLoadHandler.Instance.SaveToDisk();
        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
    }
}