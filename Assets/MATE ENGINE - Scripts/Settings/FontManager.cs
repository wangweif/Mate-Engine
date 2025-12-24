using UnityEngine;
using TMPro;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// 字体管理器
    /// 负责加载和应用SIMSUN字体到TextMeshPro组件
    /// </summary>
    public class FontManager : MonoBehaviour
    {
        private static FontManager _instance;
        public static FontManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FontManager>();
                    if (_instance == null)
                    {
                        GameObject fontManagerObj = new GameObject("FontManager");
                        _instance = fontManagerObj.AddComponent<FontManager>();
                        DontDestroyOnLoad(fontManagerObj);
                    }
                }
                return _instance;
            }
        }

        private TMP_FontAsset simsunFont;

        void Awake()
        {
            // 确保只有一个实例
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSIMSUNFont();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (simsunFont == null)
            {
                LoadSIMSUNFont();
            }
        }

        /// <summary>
        /// 加载SIMSUN字体
        /// </summary>
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
                Debug.Log($"FontManager: 已加载字体: {simsunFont.name}");
            }
            else
            {
                Debug.LogWarning("FontManager: 未找到 SIMSUN 字体，将使用默认字体");
            }
        }

        /// <summary>
        /// 应用字体到单个TextMeshPro组件
        /// </summary>
        /// <param name="tmpText">要应用字体的TextMeshPro组件</param>
        public void ApplyFontToTMP(TMP_Text tmpText)
        {
            if (tmpText != null && simsunFont != null)
            {
                tmpText.font = simsunFont;
            }
        }

        /// <summary>
        /// 应用字体到指定GameObject及其所有子对象的TextMeshPro组件
        /// </summary>
        /// <param name="rootObject">根对象</param>
        public void ApplyFontToAllTMP(GameObject rootObject)
        {
            if (simsunFont == null || rootObject == null) return;
            
            // 查找对象内所有 TextMeshPro 组件并应用字体
            TMP_Text[] allTMPs = rootObject.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in allTMPs)
            {
                if (tmp != null)
                {
                    tmp.font = simsunFont;
                }
            }
            Debug.Log($"FontManager: 已应用字体到 {allTMPs.Length} 个 TextMeshPro 组件");
        }

        /// <summary>
        /// 获取SIMSUN字体资源
        /// </summary>
        /// <returns>SIMSUN字体资源，如果未加载则返回null</returns>
        public TMP_FontAsset GetSIMSUNFont()
        {
            if (simsunFont == null)
            {
                LoadSIMSUNFont();
            }
            return simsunFont;
        }

        /// <summary>
        /// 检查字体是否已加载
        /// </summary>
        /// <returns>如果字体已加载返回true，否则返回false</returns>
        public bool IsFontLoaded()
        {
            return simsunFont != null;
        }

        /// <summary>
        /// 重新加载字体（用于运行时字体资源更新）
        /// </summary>
        public void ReloadFont()
        {
            simsunFont = null;
            LoadSIMSUNFont();
        }

        /// <summary>
        /// 静态方法：快速应用字体到TextMeshPro组件
        /// </summary>
        /// <param name="tmpText">要应用字体的TextMeshPro组件</param>
        public static void ApplyFont(TMP_Text tmpText)
        {
            Instance.ApplyFontToTMP(tmpText);
        }

        /// <summary>
        /// 静态方法：快速应用字体到GameObject的所有TextMeshPro组件
        /// </summary>
        /// <param name="rootObject">根对象</param>
        public static void ApplyFontToAll(GameObject rootObject)
        {
            Instance.ApplyFontToAllTMP(rootObject);
        }
    }
}
