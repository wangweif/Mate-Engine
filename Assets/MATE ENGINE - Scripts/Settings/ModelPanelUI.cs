using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 模型面板UI管理器
    /// 负责显示模型列表并处理模型选择
    /// </summary>
    public class ModelPanelUI : MonoBehaviour
    {
        [Header("Model Panel Components")]
        public GameObject modelPanel;

        private VRMLoader vrmLoader;
        private List<ModelListItem> modelItems = new List<ModelListItem>();
        private string currentSelectedModel = "";

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

            // 获取当前选中的模型
            GetCurrentSelectedModel();
        }

        /// <summary>
        /// 创建模型面板内容
        /// </summary>
        public void CreateModelPanelContent()
        {
            if (modelPanel == null) return;

            // 清除现有内容
            foreach (Transform child in modelPanel.transform)
            {
                Destroy(child.gameObject);
            }
            modelItems.Clear();

            VerticalLayoutGroup layout = modelPanel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = modelPanel.AddComponent<VerticalLayoutGroup>();
            }
            layout.spacing = 10;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            // 创建模型列表
            CreateModelList();
        }

        /// <summary>
        /// 创建模型列表
        /// </summary>
        void CreateModelList()
        {
            List<string> modelFiles = GetVRMFilesFromRoot();
            
            foreach (string modelFile in modelFiles)
            {
                CreateModelListItem(modelFile);
            }
        }

        /// <summary>
        /// 获取根目录下的VRM文件
        /// </summary>
        List<string> GetVRMFilesFromRoot()
        {
            List<string> vrmFiles = new List<string>();
            
            string rootPath;
            if (Application.dataPath.Contains("/Assets"))
            {
                rootPath = Application.dataPath.Replace("/Assets", "");
            }
            else
            {
                rootPath = Directory.GetParent(Application.dataPath).FullName;
            }

            if (Directory.Exists(rootPath))
            {
                string[] files = Directory.GetFiles(rootPath, "*.vrm");
                foreach (string file in files)
                {
                    vrmFiles.Add(Path.GetFileName(file));
                }
            }

            return vrmFiles;
        }

        /// <summary>
        /// 创建模型列表项
        /// </summary>
        void CreateModelListItem(string modelFileName)
        {
            GameObject itemObj = new GameObject($"ModelItem_{modelFileName}");
            itemObj.transform.SetParent(modelPanel.transform, false);
            
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 50);

            // 添加背景图片组件用于高亮
            Image backgroundImage = itemObj.AddComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0f); // 透明背景

            // 添加按钮组件用于点击
            Button itemButton = itemObj.AddComponent<Button>();
            itemButton.targetGraphic = backgroundImage;
            
            // 创建水平布局
            HorizontalLayoutGroup horizontalLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = new RectOffset(15, 15, 10, 10);
            horizontalLayout.spacing = 10;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

            // 创建选中标记（√）
            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(itemObj.transform, false);
            RectTransform checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRect.sizeDelta = new Vector2(30, 30);
            
            TMP_Text checkmarkText = checkmarkObj.AddComponent<TextMeshProUGUI>();
            checkmarkText.text = "✓";
            checkmarkText.fontSize = 24;
            checkmarkText.color = new Color(0.2f, 0.8f, 0.2f, 1f); // 绿色
            checkmarkText.alignment = TextAlignmentOptions.Center;
            FontManager.ApplyFont(checkmarkText);
            
            LayoutElement checkmarkLayout = checkmarkObj.AddComponent<LayoutElement>();
            checkmarkLayout.minWidth = 30;
            checkmarkLayout.preferredWidth = 30;

            // 创建模型名称文本
            GameObject nameObj = new GameObject("ModelName");
            nameObj.transform.SetParent(itemObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            
            TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = Path.GetFileNameWithoutExtension(modelFileName);
            nameText.fontSize = 20;
            nameText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            nameText.alignment = TextAlignmentOptions.Left;
            FontManager.ApplyFont(nameText);
            
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            // 创建模型列表项数据
            ModelListItem item = new ModelListItem
            {
                fileName = modelFileName,
                itemObject = itemObj,
                backgroundImage = backgroundImage,
                checkmarkText = checkmarkText,
                nameText = nameText,
                button = itemButton
            };

            modelItems.Add(item);

            // 设置点击事件
            itemButton.onClick.AddListener(() => OnModelSelected(modelFileName));

            // 更新选中状态
            UpdateItemSelection(item);
        }

        /// <summary>
        /// 模型选中事件
        /// </summary>
        void OnModelSelected(string modelFileName)
        {
            if (vrmLoader == null)
            {
                Debug.LogWarning("VRMLoader组件未找到");
                return;
            }

            // 更新当前选中的模型
            currentSelectedModel = modelFileName;
            
            // 更新所有列表项的选中状态
            UpdateAllItemSelections();
            
            // 切换模型
            vrmLoader.SwitchModel(modelFileName);
            
            Debug.Log($"[ModelPanelUI] 选中模型: {modelFileName}");
        }

        /// <summary>
        /// 获取当前选中的模型
        /// </summary>
        void GetCurrentSelectedModel()
        {
            // 默认选中第一个模型（test1.vrm）
            currentSelectedModel = "test1.vrm";
        }

        /// <summary>
        /// 更新所有列表项的选中状态
        /// </summary>
        void UpdateAllItemSelections()
        {
            foreach (var item in modelItems)
            {
                UpdateItemSelection(item);
            }
        }

        /// <summary>
        /// 更新单个列表项的选中状态
        /// </summary>
        void UpdateItemSelection(ModelListItem item)
        {
            bool isSelected = item.fileName == currentSelectedModel;
            
            // 更新选中标记显示
            item.checkmarkText.gameObject.SetActive(isSelected);
            
            // 更新背景高亮
            if (isSelected)
            {
                item.backgroundImage.color = new Color(0.23f, 0.45f, 0.85f, 0.2f); // 淡蓝色高亮
                item.nameText.color = new Color(0.23f, 0.45f, 0.85f, 1f); // 蓝色文字
            }
            else
            {
                item.backgroundImage.color = new Color(1f, 1f, 1f, 0f); // 透明背景
                item.nameText.color = new Color(0.12f, 0.12f, 0.12f, 1f); // 默认文字颜色
            }
        }

        /// <summary>
        /// 设置VRMLoader引用
        /// </summary>
        public void SetVRMLoader(VRMLoader loader)
        {
            vrmLoader = loader;
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

        /// <summary>
        /// 刷新模型列表
        /// </summary>
        public void RefreshModelList()
        {
            CreateModelPanelContent();
        }
    }

    /// <summary>
    /// 模型列表项数据类
    /// </summary>
    [System.Serializable]
    public class ModelListItem
    {
        public string fileName;
        public GameObject itemObject;
        public Image backgroundImage;
        public TMP_Text checkmarkText;
        public TMP_Text nameText;
        public Button button;
    }
}
