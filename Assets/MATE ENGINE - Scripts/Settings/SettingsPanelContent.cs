using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 设置面板内容创建器
    /// 负责创建设置页面的UI内容
    /// </summary>
    public static class SettingsPanelContent
    {
        /// <summary>
        /// 创建设置面板内容
        /// </summary>
        /// <param name="settingsPanel">设置面板的GameObject</param>
        /// <param name="changelogScrollRect">更新日志滚动视图的引用</param>
        /// <param name="changelogText">更新日志文本的引用</param>
        /// <param name="createLabelFunc">创建标签的函数</param>
        /// <param name="createButtonFunc">创建按钮的函数</param>
        public static void CreateSettingsPanelContent(
            GameObject settingsPanel,
            out ScrollRect changelogScrollRect,
            out TMP_Text changelogText,
            System.Func<Transform, string, float, GameObject> createLabelFunc,
            System.Func<Transform, string, Vector2, Button> createButtonFunc)
        {
            // 初始化输出参数
            changelogScrollRect = null;
            changelogText = null;

            VerticalLayoutGroup layout = settingsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            // 更新日志标题
            GameObject changelogLabel = createLabelFunc(settingsPanel.transform, "更新日志：", 16);

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
            changelogText.fontSize = 20; // 调大字体 (16 -> 20)
            changelogText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            changelogText.alignment = TextAlignmentOptions.TopLeft;
            FontManager.ApplyFont(changelogText);

            ContentSizeFitter fitter = changelogTextObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
