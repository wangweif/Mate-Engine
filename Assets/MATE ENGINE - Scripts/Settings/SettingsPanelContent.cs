using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 设置面板内容创建器
    /// 负责创建设置页面的UI内容并管理更新日志
    /// </summary>
    public class SettingsPanelContent : MonoBehaviour
    {
        [Header("Settings Panel Components")]
        public GameObject settingsPanel;

        [Header("Changelog Components")]
        public ScrollRect changelogScrollRect;
        public TMP_Text changelogText;
        public TMP_Dropdown versionDropdown;

        // 版本信息列表
        private List<VersionInfo> versionInfos = new List<VersionInfo>();

        /// <summary>
        /// 设置设置面板
        /// </summary>
        public void SetSettingsPanel(GameObject panel)
        {
            settingsPanel = panel;
        }

        /// <summary>
        /// 创建设置面板内容
        /// </summary>
        /// <param name="createLabelFunc">创建标签的函数</param>
        /// <param name="createButtonFunc">创建按钮的函数</param>
        public void CreateSettingsPanelContent(
            System.Func<Transform, string, float, GameObject> createLabelFunc,
            System.Func<Transform, string, Vector2, Button> createButtonFunc)
        {
            if (settingsPanel == null) return;

            VerticalLayoutGroup layout = settingsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;

            // 更新日志标题
            GameObject changelogLabel = createLabelFunc(settingsPanel.transform, "更新日志：", 16);

            // 创建版本选择下拉框
            GameObject dropdownObj = new GameObject("VersionDropdown");
            dropdownObj.transform.SetParent(settingsPanel.transform, false);
            RectTransform dropdownRect = dropdownObj.GetComponent<RectTransform>();
            if (dropdownRect == null)
            {
                dropdownRect = dropdownObj.AddComponent<RectTransform>();
            }
            dropdownRect.sizeDelta = new Vector2(0, 40);

            // 添加背景图片
            Image dropdownBg = dropdownObj.AddComponent<Image>();
            dropdownBg.color = new Color(1f, 1f, 1f, 1f);

            // 添加下拉框组件
            versionDropdown = dropdownObj.AddComponent<TMP_Dropdown>();
            versionDropdown.targetGraphic = dropdownBg;

            // 创建标签（显示当前选中的版本）
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 5);
            labelRect.offsetMax = new Vector2(-25, -5);
            TMP_Text labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "选择版本...";
            labelText.fontSize = 20;
            labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            FontManager.ApplyFont(labelText);
            versionDropdown.captionText = labelText;

            // 创建模板（下拉列表）
            GameObject templateObj = new GameObject("Template");
            templateObj.transform.SetParent(dropdownObj.transform, false);
            RectTransform templateRect = templateObj.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0, 150);
            Image templateBg = templateObj.AddComponent<Image>();
            templateBg.color = new Color(1f, 1f, 1f, 1f);
            ScrollRect templateScroll = templateObj.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            versionDropdown.template = templateRect;
            templateObj.SetActive(false);

            // 创建视口
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(templateObj.transform, false);
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;
            Image viewportMask = viewportObj.AddComponent<Image>();
            viewportMask.color = new Color(1f, 1f, 1f, 1f);
            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            templateScroll.viewport = viewportRect;

            // 创建内容
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup contentLayout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = false;
            ContentSizeFitter contentFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            templateScroll.content = contentRect;

            // 创建选项项模板（必须作为Content的子对象）
            GameObject itemObj = new GameObject("Item");
            itemObj.transform.SetParent(contentObj.transform, false);
            // 注意：Item模板应该保持激活状态，Unity会使用它作为模板来创建选项
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 30);
            Toggle itemToggle = itemObj.AddComponent<Toggle>();
            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.color = new Color(0.96f, 0.96f, 0.98f, 1f);
            itemToggle.targetGraphic = itemBg;
            // 注意：不需要设置 ToggleGroup，TMP_Dropdown 会自动管理选中状态

            // 创建选项标签
            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(itemObj.transform, false);
            RectTransform itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(60, 0);
            itemLabelRect.offsetMax = new Vector2(0, 0);
            TMP_Text itemLabelText = itemLabelObj.AddComponent<TextMeshProUGUI>();
            itemLabelText.text = "选项";
            itemLabelText.fontSize = 20;
            itemLabelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            itemLabelText.alignment = TextAlignmentOptions.Left;
            FontManager.ApplyFont(itemLabelText);
            versionDropdown.itemText = itemLabelText;

            // 设置下拉框属性
            versionDropdown.options = new List<TMP_Dropdown.OptionData>();

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
            GameObject changelogViewport = new GameObject("Viewport");
            changelogViewport.transform.SetParent(scrollObj.transform, false);
            RectTransform changelogViewportRect = changelogViewport.GetComponent<RectTransform>();
            if (changelogViewportRect == null)
            {
                changelogViewportRect = changelogViewport.AddComponent<RectTransform>();
            }
            changelogViewportRect.anchorMin = Vector2.zero;
            changelogViewportRect.anchorMax = Vector2.one;
            changelogViewportRect.offsetMin = Vector2.zero;
            changelogViewportRect.offsetMax = Vector2.zero;

            Image changelogViewportMask = changelogViewport.AddComponent<Image>();
            changelogViewportMask.color = new Color(1f, 1f, 1f, 1f);
            Mask changelogMask = changelogViewport.AddComponent<Mask>();
            changelogMask.showMaskGraphic = false;

            changelogScrollRect.viewport = changelogViewportRect;

            // 创建内容
            GameObject changelogContent = new GameObject("Content");
            changelogContent.transform.SetParent(changelogViewport.transform, false);
            RectTransform changelogContentRect = changelogContent.GetComponent<RectTransform>();
            if (changelogContentRect == null)
            {
                changelogContentRect = changelogContent.AddComponent<RectTransform>();
            }
            changelogContentRect.anchorMin = new Vector2(0, 1);
            changelogContentRect.anchorMax = new Vector2(1, 1);
            changelogContentRect.pivot = new Vector2(0.5f, 1);
            changelogContentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup changelogContentLayout = changelogContent.AddComponent<VerticalLayoutGroup>();
            changelogContentLayout.childForceExpandWidth = true;
            changelogContentLayout.childControlHeight = false;
            changelogContentLayout.padding = new RectOffset(60, 10, 10, 10);

            changelogScrollRect.content = changelogContentRect;

            // 更新日志文本
            GameObject changelogTextObj = new GameObject("ChangelogText");
            changelogTextObj.transform.SetParent(changelogContent.transform, false);
            RectTransform changelogTextRect = changelogTextObj.GetComponent<RectTransform>();
            if (changelogTextRect == null)
            {
                changelogTextRect = changelogTextObj.AddComponent<RectTransform>();
            }
            changelogTextRect.sizeDelta = new Vector2(0, 0);
            changelogText = changelogTextObj.AddComponent<TextMeshProUGUI>();
            changelogText.text = "加载中...";
            changelogText.fontSize = 20; // 调大字体 (16 -> 20)
            changelogText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            changelogText.alignment = TextAlignmentOptions.TopLeft;
            FontManager.ApplyFont(changelogText);

            ContentSizeFitter fitter = changelogTextObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 加载更新日志
            LoadChangelog();
        }

        /// <summary>
        /// 加载更新日志
        /// </summary>
        void LoadChangelog()
        {
            if (changelogText == null) return;

            try
            {
                // 从VersionInfoManager获取所有版本信息
                versionInfos = VersionInfoManager.GetAllVersionInfo();
                
                if (versionInfos == null || versionInfos.Count == 0)
                {
                    changelogText.text = "未找到版本信息";
                    if (versionDropdown != null)
                    {
                        versionDropdown.ClearOptions();
                    }
                    return;
                }

                // 设置下拉框选项
                if (versionDropdown != null)
                {
                    versionDropdown.ClearOptions();
                    List<string> versionOptions = new List<string>();
                    foreach (var versionInfo in versionInfos)
                    {
                        versionOptions.Add(versionInfo.version);
                    }
                    versionDropdown.AddOptions(versionOptions);
                    versionDropdown.value = 0; // 默认选择第一个（最新版本）
                    
                    // 添加值改变事件监听
                    versionDropdown.onValueChanged.RemoveAllListeners();
                    versionDropdown.onValueChanged.AddListener(OnVersionDropdownChanged);
                    
                    // 显示第一个版本的更新日志
                    UpdateChangelogText(0);
                }
                else
                {
                    // 如果没有下拉框，显示所有版本信息
                    DisplayAllVersions();
                }
            }
            catch (System.Exception e)
            {
                changelogText.text = $"加载更新日志失败：{e.Message}";
                Debug.LogError($"加载更新日志失败: {e}");
            }
        }

        /// <summary>
        /// 版本下拉框值改变事件处理
        /// </summary>
        void OnVersionDropdownChanged(int index)
        {
            if (index >= 0 && index < versionInfos.Count)
            {
                UpdateChangelogText(index);
            }
        }

        /// <summary>
        /// 更新更新日志文本内容
        /// </summary>
        void UpdateChangelogText(int versionIndex)
        {
            if (changelogText == null || versionIndex < 0 || versionIndex >= versionInfos.Count)
                return;

            changelogText.text = versionInfos[versionIndex].description;
            
            // 滚动到顶部
            if (changelogScrollRect != null)
            {
                changelogScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// 显示所有版本信息（当没有下拉框时使用）
        /// </summary>
        void DisplayAllVersions()
        {
            if (versionInfos == null || versionInfos.Count == 0)
            {
                changelogText.text = "未找到版本信息";
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var versionInfo in versionInfos)
            {
                sb.AppendLine(versionInfo.description);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
            changelogText.text = sb.ToString();
        }
    }
}
