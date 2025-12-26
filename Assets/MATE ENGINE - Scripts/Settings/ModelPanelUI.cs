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
        public ScrollRect scrollRect; // 滚动视图组件

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

            // 设置或获取ScrollRect组件
            if (scrollRect == null)
            {
                scrollRect = modelPanel.GetComponentInParent<ScrollRect>();
            }

            // 如果没有ScrollRect，创建滚动视图结构
            if (scrollRect == null)
            {
                SetupScrollView();
            }

            VerticalLayoutGroup layout = modelPanel.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = modelPanel.AddComponent<VerticalLayoutGroup>();
            }
            layout.spacing = 2;
            layout.padding = new RectOffset(8, 8, 20, 8);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            
            // 添加ContentSizeFitter以自动调整内容大小
            ContentSizeFitter fitter = modelPanel.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = modelPanel.AddComponent<ContentSizeFitter>();
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // 创建模型列表
            CreateModelList();
        }

        /// <summary>
        /// 设置滚动视图
        /// </summary>
        void SetupScrollView()
        {
            Transform parent = modelPanel.transform.parent;
            if (parent == null) return;

            // 创建ScrollRect容器
            GameObject scrollViewObj = new GameObject("ScrollView");
            scrollViewObj.transform.SetParent(parent, false);
            scrollViewObj.transform.SetSiblingIndex(modelPanel.transform.GetSiblingIndex());
            
            RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
            scrollViewRect.anchorMin = new Vector2(0, 0);
            scrollViewRect.anchorMax = new Vector2(1, 1);
            scrollViewRect.offsetMin = Vector2.zero;
            scrollViewRect.offsetMax = Vector2.zero;

            // 添加ScrollRect组件
            scrollRect = scrollViewObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            // 创建Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollViewObj.transform, false);
            
            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0, 0);
            viewportRect.anchorMax = new Vector2(1, 1);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            
            // Image viewportMask = viewportObj.AddComponent<Image>();
            // viewportMask.color = Color.clear;
            
            // Mask mask = viewportObj.AddComponent<Mask>();
            // mask.showMaskGraphic = false;

            scrollRect.viewport = viewportRect;

            // 将modelPanel移到Viewport下
            modelPanel.transform.SetParent(viewportObj.transform, false);
            RectTransform contentRect = modelPanel.GetComponent<RectTransform>();
            if (contentRect == null)
            {
                contentRect = modelPanel.AddComponent<RectTransform>();
            }
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;

            scrollRect.content = contentRect;

            // 创建滚动条
            CreateScrollbar(scrollViewObj);
        }

        /// <summary>
        /// 创建滚动条
        /// </summary>
        void CreateScrollbar(GameObject scrollViewObj)
        {
            // 创建滚动条对象
            GameObject scrollbarObj = new GameObject("Scrollbar");
            scrollbarObj.transform.SetParent(scrollViewObj.transform, false);
            
            RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(12, 0);
            scrollbarRect.anchoredPosition = new Vector2(0, 0);

            Image scrollbarBg = scrollbarObj.AddComponent<Image>();
            scrollbarBg.color = new Color(0.1f, 0.1f, 0.12f, 0.8f); // 暗黑风格背景

            Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // 创建滑动区域
            GameObject slidingAreaObj = new GameObject("Sliding Area");
            slidingAreaObj.transform.SetParent(scrollbarObj.transform, false);
            
            RectTransform slidingAreaRect = slidingAreaObj.AddComponent<RectTransform>();
            slidingAreaRect.anchorMin = Vector2.zero;
            slidingAreaRect.anchorMax = Vector2.one;
            slidingAreaRect.offsetMin = new Vector2(2, 2);
            slidingAreaRect.offsetMax = new Vector2(-2, -2);

            // 创建滑块
            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(slidingAreaObj.transform, false);
            
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(0.4f, 0.45f, 0.55f, 0.8f); // 暗黑风格滑块

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            // 设置滚动条过渡效果
            ColorBlock colors = scrollbar.colors;
            colors.normalColor = new Color(0.4f, 0.45f, 0.55f, 0.8f);
            colors.highlightedColor = new Color(0.5f, 0.55f, 0.65f, 1f);
            colors.pressedColor = new Color(0.3f, 0.35f, 0.45f, 1f);
            colors.selectedColor = new Color(0.5f, 0.55f, 0.65f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.35f, 0.5f);
            scrollbar.colors = colors;

            // 连接滚动条到ScrollRect
            if (scrollRect != null)
            {
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }
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

            // 创建单选框（Radio Button）
            GameObject radioObj = new GameObject("RadioButton");
            radioObj.transform.SetParent(itemObj.transform, false);
            RectTransform radioRect = radioObj.AddComponent<RectTransform>();
            radioRect.sizeDelta = new Vector2(24, 24);
            
            // 单选框外圈
            Image radioOuterCircle = radioObj.AddComponent<Image>();
            radioOuterCircle.color = new Color(0.5f, 0.55f, 0.65f, 1f); // 暗黑风格边框
            radioOuterCircle.sprite = CreateCircleSprite();
            radioOuterCircle.type = Image.Type.Sliced;
            
            LayoutElement radioLayout = radioObj.AddComponent<LayoutElement>();
            radioLayout.minWidth = 24;
            radioLayout.preferredWidth = 24;
            radioLayout.minHeight = 24;
            radioLayout.preferredHeight = 24;

            // 创建单选框内圈（选中标记）
            GameObject radioInnerObj = new GameObject("RadioInner");
            radioInnerObj.transform.SetParent(radioObj.transform, false);
            RectTransform radioInnerRect = radioInnerObj.AddComponent<RectTransform>();
            radioInnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            radioInnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            radioInnerRect.pivot = new Vector2(0.5f, 0.5f);
            radioInnerRect.sizeDelta = new Vector2(12, 12);
            radioInnerRect.anchoredPosition = Vector2.zero;
            
            Image radioInnerCircle = radioInnerObj.AddComponent<Image>();
            radioInnerCircle.color = new Color(0.3f, 0.9f, 0.3f, 1f); // 绿色选中标记
            radioInnerCircle.sprite = CreateCircleSprite();
            radioInnerCircle.type = Image.Type.Sliced;

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
                radioInnerCircle = radioInnerCircle,
                radioOuterCircle = radioOuterCircle,
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
        /// 创建圆形Sprite（用于单选框）
        /// </summary>
        Sprite CreateCircleSprite()
        {
            // 创建一个简单的圆形纹理
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    
                    // 创建圆形边框
                    if (distance <= radius && distance >= radius - 2)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else if (distance < radius - 2)
                    {
                        // 内部填充（用于内圈）
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
            
            // 更新单选框内圈显示（选中时显示，未选中时隐藏）
            item.radioInnerCircle.gameObject.SetActive(isSelected);
            
            // 更新单选框外圈颜色
            if (isSelected)
            {
                item.radioOuterCircle.color = new Color(0.3f, 0.9f, 0.3f, 1f); // 绿色边框
            }
            else
            {
                item.radioOuterCircle.color = new Color(0.5f, 0.55f, 0.65f, 1f); // 灰色边框
            }
            
            // 更新背景高亮
            if (isSelected)
            {
                item.backgroundImage.color = new Color(0.25f, 0.47f, 0.87f, 0.3f); // 蓝色高亮（暗黑风格）
                item.nameText.color = new Color(0.3f, 0.9f, 0.3f, 1f); // 亮绿色文字            
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
        public Image radioInnerCircle; // 单选框内圈
        public Image radioOuterCircle; // 单选框外圈
        public TMP_Text nameText;
        public Button button;
        public Image modelImage;
    }
}
