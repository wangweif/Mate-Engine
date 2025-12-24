using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 模型面板UI管理器
    /// 负责处理模型相关的UI操作和显示
    /// </summary>
    public class ModelPanelUI : MonoBehaviour
    {
        [Header("Model Panel Components")]
        public Button changeModelButton;
        public TMP_Text currentModelText;
        public Button resetModelButton;
        public GameObject modelPanel;

        private VRMLoader vrmLoader;

        void Start()
        {
            InitializeComponents();
        }

        void InitializeComponents()
        {
            // 查找VRMLoader组件
            if (vrmLoader == null)
                vrmLoader = FindFirstObjectByType<VRMLoader>();

            // 确保FontManager已初始化
            FontManager.Instance.GetSIMSUNFont();

            // 设置按钮事件
            SetupUI();
        }

        void SetupUI()
        {
            // 设置模型面板按钮
            if (changeModelButton != null)
                changeModelButton.onClick.AddListener(OnChangeModel);

            if (resetModelButton != null)
                resetModelButton.onClick.AddListener(OnResetModel);

            UpdateModelInfo();
        }

        /// <summary>
        /// 创建模型面板内容
        /// </summary>
        public void CreateModelPanelContent()
        {
            if (modelPanel == null) return;

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
            currentModelText.fontSize = 20; // 调大字体 (16 -> 20)
            currentModelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            currentModelText.alignment = TextAlignmentOptions.Left;
            FontManager.ApplyFont(currentModelText);

            // 更改模型按钮
            changeModelButton = CreateButton(modelPanel.transform, "更改模型", new Vector2(200, 40));

            // 重置模型按钮
            resetModelButton = CreateButton(modelPanel.transform, "重置为默认模型", new Vector2(200, 40));

            // 重新设置按钮事件
            SetupUI();
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
            // 标签文字：深色
            labelText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            labelText.alignment = TextAlignmentOptions.Left;
            FontManager.ApplyFont(labelText);
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
            textComp.fontSize = 22; // 调大字体 (18 -> 22)
            textComp.color = Color.white;
            textComp.alignment = TextAlignmentOptions.Center;
            FontManager.ApplyFont(textComp);

            return btn;
        }

        /// <summary>
        /// 更改模型按钮点击事件
        /// </summary>
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

        /// <summary>
        /// 重置模型按钮点击事件
        /// </summary>
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

        /// <summary>
        /// 更新模型信息显示
        /// </summary>
        public void UpdateModelInfo()
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

        /// <summary>
        /// 设置VRMLoader引用
        /// </summary>
        public void SetVRMLoader(VRMLoader loader)
        {
            vrmLoader = loader;
            UpdateModelInfo();
        }

        /// <summary>
        /// 获取当前模型面板GameObject
        /// </summary>
        public GameObject GetModelPanel()
        {
            return modelPanel;
        }

        /// <summary>
        /// 设置模型面板GameObject
        /// </summary>
        public void SetModelPanel(GameObject panel)
        {
            modelPanel = panel;
        }
    }
}
