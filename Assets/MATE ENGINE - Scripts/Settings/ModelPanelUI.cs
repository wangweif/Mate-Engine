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

        private const string SelectedModelPrefsKey = "MATE_ENGINE_SELECTED_VRM";
        private const string TtsVoicePrefsKey = "MATE_ENGINE_TTS_VOICE";

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

            //更新模型列表状态
            UpdateAllItemSelections();
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
            layout.spacing = 2;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            // 创建模型列表
            CreateModelList();
        }

        /// <summary>
        /// 创建模型列表
        /// </summary>
        void CreateModelList()
        {
            List<string> modelFiles = GetVRMFilesFromModels();
            
            foreach (string modelFile in modelFiles)
            {
                CreateModelListItem(modelFile);
            }
        }

        /// <summary>
        /// 获取Models目录下的VRM文件
        /// </summary>
        List<string> GetVRMFilesFromModels()
        {
            List<string> vrmFiles = new List<string>();

            string modelsPath;
            if (Application.dataPath.Contains("/Assets"))
            {
                modelsPath = Path.Combine(Application.dataPath, "StreamingAssets/Models");
            }
            else
            {
                modelsPath = Path.Combine(Application.streamingAssetsPath,"Models");
            }

            if (Directory.Exists(modelsPath))
            {
                // 递归查找所有子目录中的 .vrm
                string[] files = Directory.GetFiles(modelsPath, "*.vrm", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    vrmFiles.Add(file); // 如果你只想要文件名，用 Path.GetFileName(file)
                }
            }

            return vrmFiles;
        }

        /// <summary>
        /// 获取模型图片路径
        /// </summary>
        string GetModelImagePath(string modelFilePath)
        {
            string directory = Path.GetDirectoryName(modelFilePath);
            string modelNameWithoutExt = Path.GetFileNameWithoutExtension(modelFilePath);
            
            // 支持的图片格式
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };
            
            foreach (string ext in imageExtensions)
            {
                string imagePath = Path.Combine(directory, modelNameWithoutExt + ext);
                if (File.Exists(imagePath))
                {
                    return imagePath;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 加载图片为Texture2D
        /// </summary>
        Texture2D LoadImageTexture(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;

            byte[] imageData = File.ReadAllBytes(imagePath);
            Texture2D texture = new Texture2D(2, 2);
            
            if (texture.LoadImage(imageData))
            {
                return texture;
            }
            
            Destroy(texture);
            return null;
        }

        /// <summary>
        /// 创建模型列表项
        /// </summary>
        void CreateModelListItem(string modelFileName)
        {
            GameObject itemObj = new GameObject($"ModelItem_{modelFileName}");
            itemObj.transform.SetParent(modelPanel.transform, false);
            
            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(0, 80);

            // 添加背景图片组件用于高亮
            Image backgroundImage = itemObj.AddComponent<Image>();
            backgroundImage.color = new Color(0.15f, 0.16f, 0.20f, 0f); // 透明背景（暗黑风格）

            // 添加按钮组件用于点击
            Button itemButton = itemObj.AddComponent<Button>();
            itemButton.targetGraphic = backgroundImage;
            
            // 创建水平布局
            HorizontalLayoutGroup horizontalLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = new RectOffset(15, 15, 10, 10);
            horizontalLayout.spacing = 15;
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

            // 创建模型图片
            GameObject imageObj = new GameObject("ModelImage");
            imageObj.transform.SetParent(itemObj.transform, false);
            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.sizeDelta = new Vector2(60, 60);
            
            Image modelImageComponent = imageObj.AddComponent<Image>();
            modelImageComponent.color = Color.white;
            
            // 尝试加载模型图片
            string imagePath = GetModelImagePath(modelFileName);
            Texture2D imageTexture = LoadImageTexture(imagePath);
            
            if (imageTexture != null)
            {
                Sprite imageSprite = Sprite.Create(imageTexture, new Rect(0, 0, imageTexture.width, imageTexture.height), new Vector2(0.5f, 0.5f));
                modelImageComponent.sprite = imageSprite;
                modelImageComponent.preserveAspect = true;
            }
            else
            {
                // 如果没有找到图片，显示默认颜色或隐藏
                modelImageComponent.color = new Color(0.8f, 0.8f, 0.8f, 0.3f);
            }
            
            LayoutElement imageLayout = imageObj.AddComponent<LayoutElement>();
            imageLayout.minWidth = 60;
            imageLayout.preferredWidth = 60;
            imageLayout.minHeight = 60;
            imageLayout.preferredHeight = 60;

            // 创建模型名称文本
            GameObject nameObj = new GameObject("ModelName");
            nameObj.transform.SetParent(itemObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            
            TMP_Text nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = Path.GetFileNameWithoutExtension(modelFileName);
            nameText.fontSize = 20;
            nameText.color = new Color(0.85f, 0.88f, 0.95f, 1f); // 改为浅色文字（暗黑风格）
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
                button = itemButton,
                modelImage = modelImageComponent
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
            currentSelectedModel = Path.GetFileName(modelFileName);
            PlayerPrefs.SetString(SelectedModelPrefsKey, currentSelectedModel);
            PlayerPrefs.Save();
            
            // 更新所有列表项的选中状态
            UpdateAllItemSelections();
            
            // 切换模型
            vrmLoader.SwitchModel(modelFileName);

            // 根据模型名称设置对应的TTS语音
            string voice = GetVoiceForModel(currentSelectedModel);
            PlayerPrefs.SetString(TtsVoicePrefsKey, voice);
            PlayerPrefs.Save();
                
            Debug.Log($"[ModelPanelUI] 选中模型: {modelFileName}");
        }

        /// <summary>
        /// 根据模型名称获取对应的语音
        /// </summary>
        string GetVoiceForModel(string modelName)
        {
            // 移除文件扩展名，如果有的话
            string baseName = Path.GetFileNameWithoutExtension(modelName);

            switch (baseName.ToLower())
            {
                case "male01":
                    return "aisjiuxu";
                case "female01":
                    return "x4_xiaoyan";
                case "xiaozhi":
                    return "x4_yezi";
                default:
                    Debug.LogWarning($"[ModelPanelUI] 未知模型 {baseName}，使用默认语音");
                    return "x4_yezi"; // 默认语音
            }
        }

        /// <summary>
        /// 获取当前选中的模型
        /// </summary>
        void GetCurrentSelectedModel()
        {
            currentSelectedModel = PlayerPrefs.GetString(SelectedModelPrefsKey, "xiaozhi");
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
            bool isSelected = Path.GetFileName(item.fileName) == currentSelectedModel;
            
            // 更新选中标记显示
            item.checkmarkText.gameObject.SetActive(isSelected);
            
            // 更新背景高亮
            if (isSelected)
            {
                item.backgroundImage.color = new Color(0.25f, 0.47f, 0.87f, 0.3f); // 蓝色高亮（暗黑风格）
                item.nameText.color = new Color(0.40f, 0.65f, 1.0f, 1f); // 亮蓝色文字
            }
            else
            {
                item.backgroundImage.color = new Color(0.15f, 0.16f, 0.20f, 0f); // 透明背景
                item.nameText.color = new Color(0.85f, 0.88f, 0.95f, 1f); // 浅色文字（暗黑风格）
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
        public Image modelImage;
    }
}
