using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Runtime.InteropServices;

[RequireComponent(typeof(RectTransform))]
public class NewChatMenuPanelFollower : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("核心引用")]
    public RectTransform contentPanel;
    public RectTransform parentCanvasRect;
    public RectTransform dragArea;

    [Header("边界设置")]
    [Tooltip("面板边缘距离屏幕边缘的最小像素距离")]
    public float screenMargin = 20f;

    private bool isDragging = false;
    private Vector2 pointerOffset;
    private Canvas canvas;
    private Camera uiCamera;

    // Windows API 用于获取显示器尺寸和窗口位置
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    #endif

    /// <summary>
    /// 获取真实显示器的分辨率
    /// </summary>
    private void GetRealScreenResolution(out int screenWidth, out int screenHeight)
    {
        screenWidth = Screen.width;
        screenHeight = Screen.height;

        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            IntPtr desktopHandle = GetDesktopWindow();
            RECT rect;
            if (GetWindowRect(desktopHandle, out rect))
            {
                screenWidth = rect.Right - rect.Left;
                screenHeight = rect.Bottom - rect.Top;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"获取显示器分辨率失败，使用 Screen.width/height: {e.Message}");
        }
        #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        // macOS 使用 Display.main.systemWidth
        if (Display.main != null)
        {
            screenWidth = (int)Display.main.systemWidth;
            screenHeight = (int)Display.main.systemHeight;
        }
        #else
        // 其他平台尝试使用 Display
        if (Display.main != null)
        {
            screenWidth = (int)Display.main.systemWidth;
            screenHeight = (int)Display.main.systemHeight;
        }
        #endif
    }

    /// <summary>
    /// 获取游戏窗口在真实显示器上的实际位置
    /// </summary>
    private void GetGameWindowPosition(out int windowX, out int windowY, out int windowWidth, out int windowHeight)
    {
        windowX = 0;
        windowY = 0;
        windowWidth = Screen.width;
        windowHeight = Screen.height;

        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            IntPtr gameWindow = GetActiveWindow();
            RECT rect;
            if (GetWindowRect(gameWindow, out rect))
            {
                windowX = rect.Left;
                windowY = rect.Top;
                windowWidth = rect.Right - rect.Left;
                windowHeight = rect.Bottom - rect.Top;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"获取游戏窗口位置失败: {e.Message}");
        }
        #endif
    }

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        if (contentPanel == null) contentPanel = GetComponent<RectTransform>();

        // 自动查找父级
        if (parentCanvasRect == null && contentPanel.parent != null)
            parentCanvasRect = contentPanel.parent as RectTransform;

        UpdateCameraCache();
    }

    private void Update()
    {
        // 每帧检查并修正面板位置（确保面板始终在屏幕内）
        if (contentPanel != null && parentCanvasRect != null && !isDragging)
        {
            ApplyPositionWithBounds(contentPanel.anchoredPosition);
        }
    }

    private void UpdateCameraCache()
    {
        // 缓存相机引用，Screen Space - Overlay 模式下相机应为 null
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                uiCamera = null;
            else
                uiCamera = canvas.worldCamera;
        }
    }

    /// <summary>
    /// 既然要限制在屏幕内，最稳健的方法是：
    /// 1. 先把位置设过去
    /// 2. 检查屏幕像素溢出（相对于真实显示器）
    /// 3. 如果溢出，修正位置
    /// </summary>
    private void ApplyPositionWithBounds(Vector2 desiredLocalPos)
    {
        if (contentPanel == null || parentCanvasRect == null) return;

        // --- 步骤 1: 先应用父容器限制 (Local Clamp) ---
        Vector2 clampedLocalPos = ClampToParent(desiredLocalPos);
        contentPanel.anchoredPosition = clampedLocalPos;

        // --- 步骤 2: 获取游戏窗口在真实显示器上的实际位置 ---
        GetGameWindowPosition(out int windowX, out int windowY, out int windowWidth, out int windowHeight);
        GetRealScreenResolution(out int displayWidth, out int displayHeight);

        // --- 步骤 3: 获取面板在游戏窗口坐标系中的位置 ---
        Vector3[] corners = new Vector3[4];
        contentPanel.GetWorldCorners(corners);

        Vector2 minGameWindow = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maxGameWindow = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            minGameWindow.x = Mathf.Min(minGameWindow.x, screenPoint.x);
            minGameWindow.y = Mathf.Min(minGameWindow.y, screenPoint.y);
            maxGameWindow.x = Mathf.Max(maxGameWindow.x, screenPoint.x);
            maxGameWindow.y = Mathf.Max(maxGameWindow.y, screenPoint.y);
        }

        // --- 步骤 4: 转换为真实显示器坐标 ---
        // 注意：Windows API 中 Y 轴是从上往下的，而 Unity 是从下往上的
        // 需要转换 Y 坐标
        float minDisplayX = minGameWindow.x + windowX;
        float maxDisplayX = maxGameWindow.x + windowX;
        float minDisplayY = (displayHeight - windowY - windowHeight) + minGameWindow.y;  // 转换 Y 坐标
        float maxDisplayY = (displayHeight - windowY - windowHeight) + maxGameWindow.y;  // 转换 Y 坐标

        // --- 步骤 5: 检测是否超出显示器边界 ---
        float shiftX = 0;
        float shiftY = 0;

        // 左边缘
        if (minDisplayX < screenMargin)
        {
            shiftX = screenMargin - minDisplayX;
        }
        // 右边缘
        else if (maxDisplayX > displayWidth - screenMargin)
        {
            shiftX = (displayWidth - screenMargin) - maxDisplayX;
        }

        // 下边缘
        if (minDisplayY < screenMargin)
        {
            shiftY = screenMargin - minDisplayY;
        }
        // 上边缘
        else if (maxDisplayY > displayHeight - screenMargin)
        {
            shiftY = (displayHeight - screenMargin) - maxDisplayY;
        }

        // --- 步骤 6: 应用修正 ---
        if (shiftX != 0 || shiftY != 0)
        {
            Vector2 currentCenterScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, contentPanel.position);
            Vector2 correctedCenterScreen = currentCenterScreen + new Vector2(shiftX, shiftY);

            Vector2 finalLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvasRect,
                correctedCenterScreen,
                uiCamera,
                out finalLocalPos
            );

            contentPanel.anchoredPosition = finalLocalPos;
        }
    }

    private Vector2 ClampToParent(Vector2 targetPos)
    {
        Rect pRect = parentCanvasRect.rect;
        
        // 计算缩放后的尺寸
        float width = contentPanel.rect.width * contentPanel.localScale.x;
        float height = contentPanel.rect.height * contentPanel.localScale.y;

        // Pivot 偏移
        float minX = pRect.xMin + contentPanel.pivot.x * width;
        float maxX = pRect.xMax - (1 - contentPanel.pivot.x) * width;
        float minY = pRect.yMin + contentPanel.pivot.y * height;
        float maxY = pRect.yMax - (1 - contentPanel.pivot.y) * height;

        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

        return targetPos;
    }

    #region 拖拽事件
    public void OnPointerDown(PointerEventData eventData)
    {
        if (dragArea != null)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(dragArea, eventData.position, eventData.pressEventCamera))
                return;
        }

        isDragging = true;
        UpdateCameraCache();

        // 计算初始抓取点的偏移
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localMousePos);
        pointerOffset = contentPanel.anchoredPosition - localMousePos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || contentPanel == null) return;

        // 计算目标局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPointerPos))
        {
            Vector2 desiredPos = localPointerPos + pointerOffset;
            ApplyPositionWithBounds(desiredPos);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }
    #endregion
}