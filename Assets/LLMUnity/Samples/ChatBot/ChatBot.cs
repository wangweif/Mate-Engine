using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

namespace LLMUnitySamples
{
    public class ChatBot : MonoBehaviour
    {
        [Header("Containers")]
        public Transform chatContainer;         
        public Transform inputContainer;        

        [Header("Colors & Font")]
        public Color playerColor = new Color32(81, 164, 81, 255);
        public Color aiColor = new Color32(29, 29, 73, 255);
        public Color fontColor = Color.white;
        public Font font;
        public int fontSize = 16;

        [Header("Bubble Layout")]
        public int bubbleWidth = 400;
        public float textPadding = 10f;
        public float bubbleSpacing = 10f;
        public float bottomPadding = 10f;       
        public Sprite sprite;
        public Sprite roundedSprite16;
        public Sprite roundedSprite32;
        public Sprite roundedSprite64;

        // 新增：左右边距控制
        [Header("Bubble Margins")]
        public float leftMargin = 20f;    // AI气泡左边距
        public float rightMargin = 20f;   // 玩家气泡右边距
        public float sideIndent = 80f;   // 侧边缩进量，让气泡不要贴边

        [Header("LLM")]
        public LLMCharacter llmCharacter;

        [Header("Input Settings")]
        public string inputPlaceholder = "Message me";

        [Header("Streaming Audio")]
        public AudioSource streamAudioSource;

        [Header("Bubble Materials")]
        public Material playerMaterial;         
        public Material aiMaterial;             
        [Header("Text Materials")]
        public Material playerTextMaterial;      
        public Material aiTextMaterial;        
        [Header("Scroll")]
        public ScrollRect scrollRect;           
        public bool autoScrollOnNewMessage = true;     
        public bool respectUserScroll = true;            

        [Header("History")]
        [Min(0)] public int maxMessages = 100;           
        public bool trimOnlyWhenAtBottom = true;       
        public bool enableOffscreenTrim = false;        

        [Header("Font Colors (per side)")]
        public Color playerFontColor = Color.white;
        public Color aiFontColor = Color.white;

        [Header("Rounded Sprite Radius")]
        [Range(0, 64)]
        public int cornerRadius = 16; 
        private bool layoutDirty;

        private InputBubble inputBubble;
        private List<Bubble> chatBubbles = new List<Bubble>();
        private bool blockInput = true;
        private BubbleUI playerUI, aiUI;
        private bool warmUpDone = false;
        private int lastBubbleOutsideFOV = -1;

        private Animator avatarAnimator;
        private Animator lastAvatarAnimator;
        private static readonly int isTalkingHash = Animator.StringToHash("isTalking");

        public SmartWindowsTTS smartWindowsTTS;

        void Start()
        {
            avatarAnimator = GetComponent<Animator>();

            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cornerRadius <= 16) sprite = roundedSprite16;
            else if (cornerRadius <= 32) sprite = roundedSprite32;
            else sprite = roundedSprite64;

            playerUI = new BubbleUI
            {
                sprite = sprite,
                font = font,
                fontSize = fontSize,
                fontColor = playerFontColor,
                bubbleColor = playerColor,
                bottomPosition = 0,
                leftPosition = 1,
                textPadding = textPadding,
                bubbleOffset = bubbleSpacing,
                bubbleWidth = bubbleWidth,
                bubbleHeight = -1
            };

            aiUI = new BubbleUI
            {
                sprite = sprite,
                font = font,
                fontSize = fontSize,
                fontColor = aiFontColor,
                bubbleColor = aiColor,
                bottomPosition = 0,
                leftPosition = 0,
                textPadding = textPadding,
                bubbleOffset = bubbleSpacing,
                bubbleWidth = bubbleWidth,
                bubbleHeight = -1
            };

            Transform inputParent = inputContainer != null ? inputContainer : chatContainer;

            inputBubble = new InputBubble(inputParent, playerUI, "InputBubble", "Loading...", 4);
            inputBubble.AddSubmitListener(onInputFieldSubmit);
            inputBubble.AddValueChangedListener(onValueChanged);
            inputBubble.setInteractable(false);

            ShowLoadedMessages();
            _ = llmCharacter.Warmup(WarmUpCallback);
            FindAvatarSmart();
        }

        void FindAvatarSmart()
        {
            Animator found = null;
            var loader = FindFirstObjectByType<VRMLoader>();
            if (loader != null)
            {
                var current = loader.GetCurrentModel();
                if (current != null) found = current.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a && a.gameObject.activeInHierarchy);
            }
            if (found == null)
            {
                var modelParent = GameObject.Find("Model");
                if (modelParent != null) found = modelParent.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a && a.gameObject.activeInHierarchy);
            }
            if (found == null)
            {
                var all = GameObject.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                found = all.FirstOrDefault(a => a && a.isActiveAndEnabled);
            }
            if (found != avatarAnimator)
            {
                avatarAnimator = found;
                lastAvatarAnimator = avatarAnimator;
            }
        }

        void RefreshAvatarIfChanged()
        {
            if (avatarAnimator == null || lastAvatarAnimator == null || avatarAnimator != lastAvatarAnimator)
            {
                FindAvatarSmart();
            }
        }


        private void MarkLayoutDirty()
        {
            layoutDirty = true;
        }

        void OnDisable()
        {
            if (streamAudioSource != null && streamAudioSource.isPlaying)
            {
                streamAudioSource.Stop();
                streamAudioSource.volume = 1f; 
            }
            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);
        }

        Bubble AddBubble(string message, bool isPlayerMessage)
        {
            Bubble bubble = new Bubble(chatContainer, isPlayerMessage ? playerUI : aiUI, isPlayerMessage ? "PlayerBubble" : "AIBubble", message);
            chatBubbles.Add(bubble);
            bubble.OnResize(MarkLayoutDirty);

            var image = bubble.GetRectTransform().GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.material = isPlayerMessage ? playerMaterial : aiMaterial;
            }
            var text = bubble.GetRectTransform().GetComponentInChildren<Text>(true);
            if (text != null)
            {
                Material m = isPlayerMessage ? playerTextMaterial : aiTextMaterial;
                if (m != null)
                {
                    text.material = m;
                }
            }

            if (autoScrollOnNewMessage && (!respectUserScroll || IsAtBottom()))
            {
                StartCoroutine(ScrollToBottomNextFrame());
            }

            TrimHistoryIfNeeded();

            return bubble;
        }

        void TrimHistoryIfNeeded()
        {
            if (maxMessages <= 0) return;

            if (chatBubbles.Count > maxMessages)
            {
                if (!trimOnlyWhenAtBottom || IsAtBottom())
                {
                    int removeCount = chatBubbles.Count - maxMessages;
                    for (int i = 0; i < removeCount; i++)
                    {
                        chatBubbles[i].Destroy();
                    }
                    chatBubbles.RemoveRange(0, removeCount);
                    UpdateBubblePositions();
                }
            }
        }

        bool IsAtBottom(float tolerance = 0.01f)
        {
            if (scrollRect == null) return true; 
            return scrollRect.verticalNormalizedPosition <= tolerance;
        }

        void ShowLoadedMessages()
        {
            int start = 1;
            int total = llmCharacter.chat.Count;
            if (maxMessages > 0)
                start = Mathf.Max(1, total - maxMessages);

            for (int i = start; i < total; i++)
            {
                AddBubble(llmCharacter.chat[i].content, i % 2 == 1);
            }
            StartCoroutine(ScrollToBottomNextFrame());
        }

        void onInputFieldSubmit(string newText)
        {
            inputBubble.ActivateInputField();
            if (blockInput || newText.Trim() == "" || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                StartCoroutine(BlockInteraction());
                return;
            }
            blockInput = true;

            string message = inputBubble.GetText().Replace("\v", "\n");

            AddBubble(message, true);
            Bubble aiBubble = AddBubble("...", false);

            if (streamAudioSource != null)
                streamAudioSource.Play();
            if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, true);

            Task chatTask = llmCharacter.Chat(
                message,
                (partial) => { aiBubble.SetText(partial); layoutDirty = true; },
                () =>
                {
                    if (avatarAnimator != null) avatarAnimator.SetBool(isTalkingHash, false);

                    aiBubble.SetText(aiBubble.GetText());
                    layoutDirty = true;

                    if (streamAudioSource != null && streamAudioSource.isPlaying)
                        StartCoroutine(FadeOutStreamAudio());

                    //调用tts
                    Debug.Log("开始调用tts");
                    smartWindowsTTS.SpeakAsync(aiBubble.GetText(), (success) => {
                        if (!success)
                        {
                            Debug.LogWarning($"⚠ 语音播放失败: {aiBubble.GetText()}");
                        }
                    });
                    Debug.Log("完成调用tts");

                    AllowInput();
                }
            );
            inputBubble.SetText("");
        }

        private IEnumerator FadeOutStreamAudio(float duration = 0.5f)
        {
            float startVolume = streamAudioSource.volume;

            while (streamAudioSource.volume > 0f)
            {
                streamAudioSource.volume -= startVolume * Time.deltaTime / duration;
                yield return null;
            }

            streamAudioSource.Stop();
            streamAudioSource.volume = startVolume; 
        }

        public void WarmUpCallback()
        {
            warmUpDone = true;
            inputBubble.SetPlaceHolderText(inputPlaceholder);
            AllowInput();
        }

        public void AllowInput()
        {
            blockInput = false;
            inputBubble.ReActivateInputField();
        }

        public void CancelRequests()
        {
            llmCharacter.CancelRequests();
            AllowInput();
        }

        IEnumerator<string> BlockInteraction()
        {
            inputBubble.setInteractable(false);
            yield return null;
            inputBubble.setInteractable(true);
            inputBubble.MoveTextEnd();
        }

        void onValueChanged(string newText)
        {
            if (Input.GetKey(KeyCode.Return))
            {
                if (inputBubble.GetText().Trim() == "")
                    inputBubble.SetText("");
            }
        }

        /*public void UpdateBubblePositions()
        {
            float y = bottomPadding;
            for (int i = chatBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform childRect = bubble.GetRectTransform();

                childRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);

                childRect.anchoredPosition = new Vector2(childRect.anchoredPosition.x, y);

                if (enableOffscreenTrim)
                {
                    float containerHeight = chatContainer.GetComponent<RectTransform>().rect.height;
                    if (y > containerHeight && lastBubbleOutsideFOV == -1)
                    {
                        lastBubbleOutsideFOV = i;
                    }
                }

                y += bubble.GetSize().y + bubbleSpacing;
            }
            var contentRect = chatContainer.GetComponent<RectTransform>();
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + bottomPadding);
        }*/
        public void UpdateBubblePositions()
        {
            float y = bottomPadding;
            float containerWidth = chatContainer.GetComponent<RectTransform>().rect.width;

            for (int i = chatBubbles.Count - 1; i >= 0; i--)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform childRect = bubble.GetRectTransform();

                // 设置气泡宽度
                childRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);

                // 获取气泡类型
                bool isPlayerBubble = bubble.bubbleUI.leftPosition == 1;

                // 微信风格错开布局
                float xPosition;
                if (isPlayerBubble)
                {
                    // 玩家气泡：右侧，从容器右边界向左偏移
                    xPosition = containerWidth - bubbleWidth - rightMargin - sideIndent;
                }
                else
                {
                    // AI气泡：左侧，从容器左边界向右偏移
                    xPosition = leftMargin + sideIndent;
                }

                childRect.anchoredPosition = new Vector2(xPosition, y);

                if (enableOffscreenTrim)
                {
                    float containerHeight = chatContainer.GetComponent<RectTransform>().rect.height;
                    if (y > containerHeight && lastBubbleOutsideFOV == -1)
                    {
                        lastBubbleOutsideFOV = i;
                    }
                }

                y += bubble.GetSize().y + bubbleSpacing;
            }
            var contentRect = chatContainer.GetComponent<RectTransform>();
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + bottomPadding);
            DebugBubblePositions();
        }
        public void DebugBubblePositions()
        {
            float containerWidth = chatContainer.GetComponent<RectTransform>().rect.width;
            Debug.Log($"容器宽度: {containerWidth}");

            for (int i = 0; i < chatBubbles.Count; i++)
            {
                Bubble bubble = chatBubbles[i];
                RectTransform rect = bubble.GetRectTransform();
                bool isPlayer = bubble.bubbleUI.leftPosition == 1;
                Debug.Log($"{(isPlayer ? "玩家" : "AI")}气泡 - 位置: {rect.anchoredPosition}, 锚点: {rect.anchorMin}");
            }
        }
        public void UpdateAllBulesWidth()
        {
            foreach(Bubble bubble in chatBubbles)
            {
                 RectTransform bubbleRect = bubble.GetRectTransform();
                if (bubbleRect != null)
                {
                    bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);
                }
            }
            MarkLayoutDirty();
        }

        void Update()
        {
            RefreshAvatarIfChanged();

            if (!inputBubble.inputFocused() && warmUpDone)
            {
                inputBubble.ActivateInputField();
                StartCoroutine(BlockInteraction());
            }

            if (enableOffscreenTrim && lastBubbleOutsideFOV != -1)
            {
                for (int i = 0; i <= lastBubbleOutsideFOV; i++)
                {
                    chatBubbles[i].Destroy();
                }
                chatBubbles.RemoveRange(0, lastBubbleOutsideFOV + 1);
                lastBubbleOutsideFOV = -1;
                UpdateBubblePositions();
            }
        }

        public void ExitGame()
        {
            Debug.Log("Exit button clicked");
            Application.Quit();
        }

        IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f; 
        }

        bool onValidateWarning = true;
        void OnValidate()
        {
            if (cornerRadius <= 16) sprite = roundedSprite16;
            else if (cornerRadius <= 32) sprite = roundedSprite32;
            else sprite = roundedSprite64;

            if (onValidateWarning && llmCharacter != null && !llmCharacter.remote && llmCharacter.llm != null && llmCharacter.llm.model == "")
            {
                Debug.LogWarning($"Please select a model in the {llmCharacter.llm.gameObject.name} GameObject!");
                onValidateWarning = false;
            }
        }

        void LateUpdate()
        {
            if (!layoutDirty) return;
            layoutDirty = false;

            UpdateBubblePositions();
            if (autoScrollOnNewMessage && (!respectUserScroll || IsAtBottom()))
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
            }
        }

    }
}