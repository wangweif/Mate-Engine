using UnityEngine;
using UnityEngine.UI;
using System;
using LLMUnity;

namespace LLMUnitySamples
{
    struct BubbleUI
    {
        public Sprite sprite;
        public Font font;
        public int fontSize;
        public Color fontColor;
        public Color bubbleColor;
        public float bottomPosition;
        public float leftPosition;
        public float textPadding;
        public float bubbleOffset;
        public float bubbleWidth;
        public float bubbleHeight;
    }

    public class RectTransformResizeHandler : MonoBehaviour
    {
        EmptyCallback callback;

        public void SetCallBack(EmptyCallback callback)
        {
            this.callback = callback;
        }

        void OnRectTransformDimensionsChange()
        {
            callback?.Invoke();
        }
    }

    class Bubble
    {
        protected GameObject bubbleObject;
        protected GameObject imageObject;
        public BubbleUI bubbleUI;

        public Bubble(Transform parent, BubbleUI ui, string name, string message)
        {
            bubbleUI = ui;
            bubbleObject = CreateTextObject(parent, name, message, bubbleUI.bubbleWidth == -1, bubbleUI.bubbleHeight == -1);
            imageObject = CreateImageObject(bubbleObject.transform, "Image");
            SetBubblePosition(bubbleObject.GetComponent<RectTransform>(), imageObject.GetComponent<RectTransform>(), bubbleUI);
            SetSortingOrder(bubbleObject, imageObject);
        }

        public void SyncParentRectTransform(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        protected GameObject CreateTextObject(Transform parent, string name, string message, bool horizontalStretch = true, bool verticalStretch = false)
        {
            // Create a child GameObject for the text
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Canvas));
            textObject.transform.SetParent(parent);
            Text textContent = textObject.GetComponent<Text>();

            if (verticalStretch || horizontalStretch)
            {
                ContentSizeFitter contentSizeFitter = textObject.AddComponent<ContentSizeFitter>();
                if (verticalStretch) contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                if (horizontalStretch) contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            // Add text and font
            textContent.text = message;
            if (bubbleUI.font != null)
                textContent.font = bubbleUI.font;
            textContent.fontSize = bubbleUI.fontSize;
            textContent.color = bubbleUI.fontColor;
            return textObject;
        }

        protected GameObject CreateImageObject(Transform parent, string name)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Canvas));
            imageObject.transform.SetParent(parent);
            RectTransform imageRectTransform = imageObject.GetComponent<RectTransform>();
            Image bubbleImage = imageObject.GetComponent<Image>();

            bubbleImage.type = Image.Type.Sliced;
            bubbleImage.sprite = bubbleUI.sprite;
            bubbleImage.color = bubbleUI.bubbleColor;
            return imageObject;
        }

        /*void SetBubblePosition(RectTransform bubbleRectTransform, RectTransform imageRectTransform, BubbleUI bubbleUI)
        {
            // Set the position of the new bubble at the bottom
            bubbleRectTransform.pivot = new Vector2(bubbleUI.leftPosition, bubbleUI.bottomPosition);
            bubbleRectTransform.anchorMin = new Vector2(bubbleUI.leftPosition, bubbleUI.bottomPosition);
            bubbleRectTransform.anchorMax = new Vector2(bubbleUI.leftPosition, bubbleUI.bottomPosition);
            bubbleRectTransform.localScale = Vector3.one;
            Vector2 anchoredPosition = new Vector2(bubbleUI.bubbleOffset + bubbleUI.textPadding, bubbleUI.bubbleOffset + bubbleUI.textPadding);
            if (bubbleUI.leftPosition == 1) anchoredPosition.x *= -1;
            if (bubbleUI.bottomPosition == 1) anchoredPosition.y *= -1;
            bubbleRectTransform.anchoredPosition = anchoredPosition;

            float width = bubbleUI.bubbleWidth == -1 ? bubbleRectTransform.sizeDelta.x : bubbleUI.bubbleWidth;
            float height = bubbleUI.bubbleHeight == -1 ? bubbleRectTransform.sizeDelta.y : bubbleUI.bubbleHeight;
            bubbleRectTransform.sizeDelta = new Vector2(width - 2 * bubbleUI.textPadding, height - 2 * bubbleUI.textPadding);
            SyncParentRectTransform(imageRectTransform);
            imageRectTransform.offsetMin = new Vector2(-bubbleUI.textPadding, -bubbleUI.textPadding);
            imageRectTransform.offsetMax = new Vector2(bubbleUI.textPadding, bubbleUI.textPadding);
        }*/
        void SetBubblePosition(RectTransform bubbleRectTransform, RectTransform imageRectTransform, BubbleUI bubbleUI)
        {
            // 简化：只设置基本位置，具体布局在UpdateBubblePositions中处理
            bubbleRectTransform.pivot = new Vector2(bubbleUI.leftPosition, bubbleUI.bottomPosition);
            bubbleRectTransform.anchorMin = new Vector2(bubbleUI.leftPosition, bubbleUI.bottomPosition);
            bubbleRectTransform.anchorMax = new Vector2(bubbleUI.leftPosition, bubbleUI.bottomPosition);
            bubbleRectTransform.localScale = Vector3.one;

            Vector2 anchoredPosition = new Vector2(0, bubbleUI.bubbleOffset + bubbleUI.textPadding);
            bubbleRectTransform.anchoredPosition = anchoredPosition;

            float width = bubbleUI.bubbleWidth == -1 ? bubbleRectTransform.sizeDelta.x : bubbleUI.bubbleWidth;
            float height = bubbleUI.bubbleHeight == -1 ? bubbleRectTransform.sizeDelta.y : bubbleUI.bubbleHeight;
            bubbleRectTransform.sizeDelta = new Vector2(width - 2 * bubbleUI.textPadding, height - 2 * bubbleUI.textPadding);

            SyncParentRectTransform(imageRectTransform);
            imageRectTransform.offsetMin = new Vector2(-bubbleUI.textPadding, -bubbleUI.textPadding);
            imageRectTransform.offsetMax = new Vector2(bubbleUI.textPadding, bubbleUI.textPadding);
        }

        void SetSortingOrder(GameObject bubbleObject, GameObject imageObject)
        {
            // Set the sorting order to make bubbleObject render behind textObject
            Canvas bubbleCanvas = bubbleObject.GetComponent<Canvas>();
            bubbleCanvas.overrideSorting = true;
            bubbleCanvas.sortingOrder = 2;
            Canvas imageCanvas = imageObject.GetComponent<Canvas>();
            imageCanvas.overrideSorting = true;
            imageCanvas.sortingOrder = 1;
        }

        public void OnResize(EmptyCallback callback)
        {
            RectTransformResizeHandler resizeHandler = bubbleObject.AddComponent<RectTransformResizeHandler>();
            resizeHandler.SetCallBack(callback);
        }

        public RectTransform GetRectTransform()
        {
            return bubbleObject.GetComponent<RectTransform>();
        }

        public RectTransform GetOuterRectTransform()
        {
            return imageObject.GetComponent<RectTransform>();
        }

        public Vector2 GetSize()
        {
            return bubbleObject.GetComponent<RectTransform>().sizeDelta + imageObject.GetComponent<RectTransform>().sizeDelta;
        }

        public string GetText()
        {
            return bubbleObject.GetComponent<Text>().text;
        }

        public void SetText(string text)
        {
            bubbleObject.GetComponent<Text>().text = text;
        }

        public void Show()
        {
            if (bubbleObject != null)
            {
                bubbleObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (bubbleObject != null)
            {
                bubbleObject.SetActive(false);
            }
        }

        public bool IsVisible()
        {
            return bubbleObject != null && bubbleObject.activeSelf;
        }

        public void Destroy()
        {
            UnityEngine.Object.Destroy(bubbleObject);
        }
    }

    class InputBubble : Bubble
    {
        private static Sprite defaultButtonSprite;
        protected GameObject inputFieldObject;
        protected InputField inputField;
        protected GameObject placeholderObject;
        protected GameObject voiceInputButton;
        protected GameObject keyboardButton;
        protected GameObject holdToSpeakButton;
        protected Button voiceButtonComponent;
        protected Button keyboardButtonComponent;
        protected Button holdToSpeakButtonComponent;
        protected bool isVoiceMode = false;

        public InputBubble(Transform parent, BubbleUI ui, string name, string message, int lineHeight = 4) :
            base(parent, ui, name, emptyLines(message, lineHeight))
        {
            Text textObjext = bubbleObject.GetComponent<Text>();
            RectTransform bubbleRectTransform = bubbleObject.GetComponent<RectTransform>();
            bubbleObject.GetComponent<ContentSizeFitter>().enabled = false;
            placeholderObject = CreatePlaceholderObject(bubbleObject.transform, bubbleRectTransform, textObjext.text);
            inputFieldObject = CreateInputFieldObject(bubbleObject.transform, textObjext, placeholderObject.GetComponent<Text>());
            inputField = inputFieldObject.GetComponent<InputField>();

            // <<< Hier Orders fixen NUR für InputBubble >>>
            Canvas textCanvas = bubbleObject.GetComponent<Canvas>();
            if (textCanvas != null)
            {
                textCanvas.sortingOrder = 2;
                // 确保Canvas有GraphicRaycaster以接收UI事件
                if (bubbleObject.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                {
                    bubbleObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
            }

            Canvas imgCanvas = imageObject.GetComponent<Canvas>();
            if (imgCanvas != null)
            {
                imgCanvas.sortingOrder = 2;
            }

            // 创建语音输入按钮和按住说话按钮
            CreateVoiceInputButtons(bubbleRectTransform);
        }


        static string emptyLines(string message, int lineHeight)
        {
            string messageLines = message;
            for (int i = 0; i < lineHeight - 1; i++)
                messageLines += "\n";
            return messageLines;
        }

        GameObject CreatePlaceholderObject(Transform parent, RectTransform textRectTransform, string message)
        {
            // Create a child GameObject for the placeholder text
            GameObject placeholderObject = CreateTextObject(parent, "Placeholder", message, false, false);
            RectTransform placeholderRectTransform = placeholderObject.GetComponent<RectTransform>();
            placeholderRectTransform.sizeDelta = textRectTransform.sizeDelta;
            placeholderRectTransform.anchoredPosition = textRectTransform.anchoredPosition;
            placeholderRectTransform.localScale = Vector3.one;
            SyncParentRectTransform(placeholderRectTransform);
            return placeholderObject;
        }

        GameObject CreateInputFieldObject(Transform parent, Text textObject, Text placeholderTextObject)
        {
            GameObject inputFieldObject = new GameObject("InputField", typeof(RectTransform), typeof(InputField), typeof(Canvas));
            inputFieldObject.transform.SetParent(parent);
            inputField = inputFieldObject.GetComponent<InputField>();
            inputField.textComponent = textObject;
            inputField.placeholder = placeholderTextObject;
            inputField.interactable = true;
            inputField.lineType = InputField.LineType.MultiLineSubmit;
            inputField.shouldHideMobileInput = false;
            inputField.shouldActivateOnSelect = true;
            RectTransform inputFieldRect = inputFieldObject.GetComponent<RectTransform>();
            inputFieldRect.localScale = Vector3.one;
            
            // 设置输入框的RectTransform，为右侧按钮留出空间（约50像素）
            inputFieldRect.anchorMin = Vector2.zero;
            inputFieldRect.anchorMax = new Vector2(1f, 1f);
            inputFieldRect.pivot = new Vector2(0.5f, 0.5f);
            inputFieldRect.offsetMin = new Vector2(0f, 0f);
            inputFieldRect.offsetMax = new Vector2(-50f, 0f); // 右侧留出50像素给按钮
            
            // 确保输入框的Canvas不会遮挡按钮（降低层级）
            Canvas inputFieldCanvas = inputFieldObject.GetComponent<Canvas>();
            if (inputFieldCanvas != null)
            {
                inputFieldCanvas.sortingOrder = 1; // 低于按钮的层级
            }
            
            return inputFieldObject;
        }

        public void FixCaretSorting()
        {
            GameObject caret = GameObject.Find($"{inputField.name} Input Caret");
            Canvas bubbleCanvas = caret.GetComponent<Canvas>();
            if (bubbleCanvas == null)
            {
                bubbleCanvas = caret.AddComponent<Canvas>();
                bubbleCanvas.overrideSorting = true;
                bubbleCanvas.sortingOrder = 3;
            }
        }

        public void AddSubmitListener(UnityEngine.Events.UnityAction<string> onInputFieldSubmit)
        {
            if (inputField != null)
            {
                inputField.onSubmit.AddListener(onInputFieldSubmit);
            }
        }

        public void AddValueChangedListener(UnityEngine.Events.UnityAction<string> onValueChanged)
        {
            if (inputField != null)
            {
                inputField.onValueChanged.AddListener(onValueChanged);
            }
        }

        public new string GetText()
        {
            if (inputField == null || !inputFieldObject.activeSelf) return "";
            return inputField.text;
        }

        public new void SetText(string text)
        {
            if (inputField == null) return;
            // 如果处于语音模式，先切换回文本输入模式
            if (isVoiceMode)
            {
                SetVoiceMode(false);
            }
            if (inputFieldObject.activeSelf)
            {
                inputField.text = text;
                MoveTextEnd();
            }
        }

        public void SetPlaceHolderText(string text)
        {
            placeholderObject.GetComponent<Text>().text = text;
        }

        public bool inputFocused()
        {
            if (inputField == null || !inputFieldObject.activeSelf) return false;
            return inputField.isFocused;
        }

        public void MoveTextEnd()
        {
            if (inputField == null || !inputFieldObject.activeSelf) return;
            inputField.MoveTextEnd(true);
        }

        public void setInteractable(bool interactable)
        {
            if (inputField == null) return;
            inputField.interactable = interactable;
        }

        public void SetSelectionColorAlpha(float alpha)
        {
            Color color = inputField.selectionColor;
            color.a = alpha;
            inputField.selectionColor = color;
        }

        public void ActivateInputField()
        {
            if (inputField == null || !inputFieldObject.activeSelf) return;
            inputField.ActivateInputField();
            FixCaretSorting();
        }

        public void ReActivateInputField()
        {
            if (inputField == null || !inputFieldObject.activeSelf) return;
            inputField.DeactivateInputField();
            inputField.Select();
            inputField.ActivateInputField();
        }

        void CreateVoiceInputButtons(RectTransform bubbleRectTransform)
        {
            // 创建语音输入按钮（麦克风图标）- 移除Canvas，使用父Canvas
            voiceInputButton = new GameObject("VoiceInputButton", typeof(RectTransform), typeof(Image), typeof(Button));
            voiceInputButton.transform.SetParent(bubbleObject.transform, false);
            RectTransform voiceButtonRect = voiceInputButton.GetComponent<RectTransform>();
            if (voiceButtonRect != null)
            {
                voiceButtonRect.anchorMin = new Vector2(1f, 0.5f);
                voiceButtonRect.anchorMax = new Vector2(1f, 0.5f);
                voiceButtonRect.pivot = new Vector2(1f, 0.5f);
                voiceButtonRect.sizeDelta = new Vector2(110f, 110f);
                voiceButtonRect.anchoredPosition = new Vector2(140f, 0f); // 移到输入框右侧
                voiceButtonRect.localScale = Vector3.one;
            }
            
            Image voiceButtonImage = voiceInputButton.GetComponent<Image>();
            if (voiceButtonImage != null)
            {
                Sprite micSprite = Resources.Load<Sprite>("mic");
                if (micSprite != null)
                {
                    voiceButtonImage.sprite = micSprite;
                    voiceButtonImage.color = Color.white;  // 图标本身的透明背景
                }
                voiceButtonImage.raycastTarget = true; // 确保可以接收射线检测
                voiceButtonImage.type = UnityEngine.UI.Image.Type.Simple;
            }
            
            voiceButtonComponent = voiceInputButton.GetComponent<Button>();
            if (voiceButtonComponent != null)
            {
                voiceButtonComponent.interactable = true;
                // 确保按钮的targetGraphic设置正确
                if (voiceButtonComponent.targetGraphic == null)
                {
                    voiceButtonComponent.targetGraphic = voiceButtonImage;
                }
                // 设置按钮的transition类型
                voiceButtonComponent.transition = Selectable.Transition.ColorTint;
            }
            
            // 确保按钮在正确的层级（通过设置SiblingIndex，放在最后以确保在最上层）
            voiceInputButton.transform.SetAsLastSibling();

            // 创建键盘按钮（初始隐藏）- 移除Canvas，使用父Canvas
            keyboardButton = new GameObject("KeyboardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            keyboardButton.transform.SetParent(bubbleObject.transform, false);
            RectTransform keyboardButtonRect = keyboardButton.GetComponent<RectTransform>();
            if (keyboardButtonRect != null)
            {
                keyboardButtonRect.anchorMin = new Vector2(1f, 0.5f);
                keyboardButtonRect.anchorMax = new Vector2(1f, 0.5f);
                keyboardButtonRect.pivot = new Vector2(1f, 0.5f);
                keyboardButtonRect.sizeDelta = new Vector2(110f, 110f);
                keyboardButtonRect.anchoredPosition = new Vector2(140f, 0f);
                keyboardButtonRect.localScale = Vector3.one;
            }
            
            Image keyboardButtonImage = keyboardButton.GetComponent<Image>();
            if (keyboardButtonImage != null)
            {
                Sprite keyboardSprite = Resources.Load<Sprite>("keyboard");
                if (keyboardSprite != null)
                {
                    keyboardButtonImage.sprite = keyboardSprite;
                    keyboardButtonImage.color = Color.white;
                }
                keyboardButtonImage.raycastTarget = true;
                keyboardButtonImage.type = UnityEngine.UI.Image.Type.Simple;
            }
            
            keyboardButtonComponent = keyboardButton.GetComponent<Button>();
            if (keyboardButtonComponent != null)
            {
                keyboardButtonComponent.interactable = true;
            }
            
            keyboardButton.SetActive(false);

            // 创建按住说话按钮（初始隐藏）- 移除Canvas，使用父Canvas
            holdToSpeakButton = new GameObject("HoldToSpeakButton", typeof(RectTransform), typeof(Image), typeof(Button));
            holdToSpeakButton.transform.SetParent(bubbleObject.transform, false);
            RectTransform holdButtonRect = holdToSpeakButton.GetComponent<RectTransform>();
            if (holdButtonRect != null)
            {
                holdButtonRect.anchorMin = new Vector2(0f, 0.5f);
                holdButtonRect.anchorMax = new Vector2(0f, 0.5f);
                holdButtonRect.pivot = new Vector2(0f, 0.5f);
                holdButtonRect.sizeDelta = new Vector2 (bubbleObject.GetComponent<RectTransform>().sizeDelta.x, 80f);
                holdButtonRect.anchoredPosition = new Vector2(0f, 0f);
                holdButtonRect.localScale = Vector3.one;
            }
            
            Image holdButtonImage = holdToSpeakButton.GetComponent<Image>();
            if (holdButtonImage != null)
            {
                holdButtonImage.color = new Color(0.2f, 0.6f, 0.9f, 1f);
                holdButtonImage.raycastTarget = true;
            }
            
            // 创建文本子对象
            GameObject holdButtonTextObj = new GameObject("HoldButtonText", typeof(RectTransform), typeof(Text));
            holdButtonTextObj.transform.SetParent(holdToSpeakButton.transform, false);
            RectTransform holdButtonTextRect = holdButtonTextObj.GetComponent<RectTransform>();
            if (holdButtonTextRect != null)
            {
                holdButtonTextRect.anchorMin = Vector2.zero;
                holdButtonTextRect.anchorMax = Vector2.one;
                holdButtonTextRect.sizeDelta = Vector2.zero;
                holdButtonTextRect.anchoredPosition = Vector2.zero;
            }
            
            Text holdButtonText = holdButtonTextObj.GetComponent<Text>();
            if (holdButtonText != null)
            {
                holdButtonText.text = "按住说话";
                holdButtonText.fontSize = 24;
                holdButtonText.color = Color.white;
                holdButtonText.alignment = TextAnchor.MiddleCenter;
                holdButtonText.raycastTarget = false; // 文本不拦截点击
                if (bubbleUI.font != null) holdButtonText.font = bubbleUI.font;
            }
            
            holdToSpeakButtonComponent = holdToSpeakButton.GetComponent<Button>();
            if (holdToSpeakButtonComponent != null)
            {
                holdToSpeakButtonComponent.interactable = true;
            }
            
            holdToSpeakButton.SetActive(false);
        }

        public void SetVoiceMode(bool voiceMode)
        {
            isVoiceMode = voiceMode;
            if (voiceMode)
            {
                if (voiceInputButton != null) voiceInputButton.SetActive(false);
                if (keyboardButton != null) keyboardButton.SetActive(true);
                if (holdToSpeakButton != null) holdToSpeakButton.SetActive(true);
                if (inputFieldObject != null) inputFieldObject.SetActive(false);
            }
            else
            {
                if (voiceInputButton != null) voiceInputButton.SetActive(true);
                if (keyboardButton != null) keyboardButton.SetActive(false);
                if (holdToSpeakButton != null) holdToSpeakButton.SetActive(false);
                if (inputFieldObject != null) inputFieldObject.SetActive(true);
            }
        }

        public Button GetVoiceInputButton()
        {
            return voiceButtonComponent;
        }

        public Button GetKeyboardButton()
        {
            return keyboardButtonComponent;
        }

        public Button GetHoldToSpeakButton()
        {
            return holdToSpeakButtonComponent;
        }

        public bool IsVoiceMode()
        {
            return isVoiceMode;
        }

        public void UpdateHoldButtonCancelState(bool isCancelling)
        {
            if (holdToSpeakButton == null) return;

            Image holdButtonImage = holdToSpeakButton.GetComponent<Image>();
            if (holdButtonImage != null)
            {
                holdButtonImage.color = isCancelling
                    ? new Color(0.8f, 0.2f, 0.2f, 1f)   // 取消状态：红色
                    : new Color(18f/255f, 150f/255f, 219f/255f, 1f);  // 正常状态：蓝色
            }

            Text holdButtonText = holdToSpeakButton.GetComponentInChildren<Text>();
            if (holdButtonText != null)
            {
                holdButtonText.text = isCancelling ? "松开取消" : "按住说话";
            }
        }
        public void UpdateHoldButtonState(bool isRecording)
        {
            if (holdToSpeakButton == null) return;

            // 更新按钮颜色
            Image holdButtonImage = holdToSpeakButton.GetComponent<Image>();
            if (holdButtonImage != null)
            {
                holdButtonImage.color = isRecording 
                    ? new Color(0.2f, 0.9f, 0.2f, 1f)  // 录制中：绿色
                    : new Color(18f/255f, 150f/255f, 219f/255f, 1f);  // 未录制：蓝色
            }

            // 更新按钮文本
            Text holdButtonText = holdToSpeakButton.GetComponentInChildren<Text>();
            if (holdButtonText != null)
            {
                holdButtonText.text = isRecording ? "正在录音..." : "按住说话";
            }
        }
    }

}
