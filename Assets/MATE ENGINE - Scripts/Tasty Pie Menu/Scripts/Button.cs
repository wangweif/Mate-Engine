using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MATE_ENGINE___Scripts.Tools;

namespace Xamin
{
    [RequireComponent(typeof(Image))]
    public class Button : MonoBehaviour
    {
        [Tooltip("Your actions, that will be executed when the buttons is pressed")]
        public UnityEvent action;
        [Tooltip("The icon of this button")]
        public Sprite image;
        [Tooltip("If this button can be pressed or not. False = grayed out button")]
        public bool unlocked;
        [Tooltip("Can be used to reference the button via code.")]
        public string id;

        public Color customColor;
        public bool useCustomColor;

        [Header("Button Hide Conditions")]
        [Tooltip("Button wird ausgeblendet, wenn einer dieser Animator-Bool-Parameter true ist (z.B. IsSitting, IsWindowsSit)")]
        public string[] hideIfAnimatorBool;
        [Tooltip("Button wird ausgeblendet, wenn einer dieser States im Base Layer aktiv ist (z.B. Sit, WindowsSit)")]
        public string[] hideIfStateName;

        [Header("Show Only If Animator Bool (true)")]
        public string[] showOnlyIfAnimatorBool;
        public string[] showOnlyIfStateName;


        private UnityEngine.UI.Image imageComponent;
        private bool _isimageComponentNotNull;

        void Start()
        {
            imageComponent = GetComponent<UnityEngine.UI.Image>();
            if (image)
                imageComponent.sprite = image;
            _isimageComponentNotNull = imageComponent != null;
        }

        public Color currentColor
        {
            get { return imageComponent.color; }
        }

        public void SetColor(Color c)
        {
            if (_isimageComponentNotNull)
                imageComponent.color = c;
        }

        public void ExecuteAction()
        {
            // 如果是设置按钮，切换设置界面显示/隐藏，不执行原来的事件
            if (id == "settings")
            {
                ToggleSettingsPanel();
                return; // 直接返回，不执行原来的事件
            }
            
            // 其他按钮执行自定义事件
            action.Invoke();
        }

        private void ToggleSettingsPanel()
        {
            // 查找设置面板
            SettingsPanelUI settingsPanel = FindFirstObjectByType<SettingsPanelUI>();
            
            if (settingsPanel != null)
            {
                // 切换显示/隐藏状态
                settingsPanel.TogglePanel();
                Debug.Log($"设置面板已{(settingsPanel.IsPanelOpen() ? "显示" : "隐藏")}");
            }
            else
            {
                // 如果找不到，尝试创建并显示
                GameObject panelObj = new GameObject("SettingsPanelUI");
                settingsPanel = panelObj.AddComponent<SettingsPanelUI>();
                if (settingsPanel != null)
                {
                    settingsPanel.OpenPanel();
                    Debug.Log("设置面板已创建并显示");
                }
                else
                {
                    Debug.LogWarning("无法创建设置面板");
                }
            }
        }
    }
}
