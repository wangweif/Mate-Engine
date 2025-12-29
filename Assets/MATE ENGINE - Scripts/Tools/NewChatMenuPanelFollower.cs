using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// NewChatMenuPanel 可拖拽功能
/// 只保留拖拽，删除了跟随逻辑
/// </summary>
public class NewChatMenuPanelFollower : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("核心引用")]
    public RectTransform contentPanel; // 移动的面板
    public RectTransform parentCanvasRect; // 父级容器

    [Header("拖拽设置")]
    public RectTransform dragArea;
    public float screenMargin = 20f;

    private bool isDragging = false;
    private Vector2 pointerOffset; // 鼠标点击位置相对于面板中心的偏移

    private void Start()
    {
        // 如果没有手动指定，自动获取父级
        if (parentCanvasRect == null && contentPanel != null)
            parentCanvasRect = contentPanel.parent as RectTransform;
    }

    private Vector2 ClampAnchoredPos(Vector2 pos)
    {
        if (parentCanvasRect == null || contentPanel == null)
            return pos;

        // 获取父容器的尺寸
        Vector2 parentSize = parentCanvasRect.rect.size;
        // 获取面板的尺寸
        Vector2 panelSize = contentPanel.rect.size;

        // 计算可移动的边界范围
        // 左边界：面板左边缘不能超过父容器左边缘 + 边距
        float minX = -parentSize.x / 2f + panelSize.x / 2f + screenMargin;
        // 右边界：面板右边缘不能超过父容器右边缘 - 边距
        float maxX = parentSize.x / 2f - panelSize.x / 2f - screenMargin;
        // 下边界：面板下边缘不能超过父容器下边缘 + 边距
        float minY = -parentSize.y / 2f + panelSize.y / 2f + screenMargin;
        // 上边界：面板上边缘不能超过父容器上边缘 - 边距
        float maxY = parentSize.y / 2f - panelSize.y / 2f - screenMargin;

        // 限制位置在边界范围内
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        return pos;
    }

    #region 拖拽处理
    public void OnPointerDown(PointerEventData eventData)
    {
        if (dragArea != null && eventData.pointerCurrentRaycast.gameObject != dragArea.gameObject) return;

        isDragging = true;

        // 计算鼠标点击位置与面板中心的偏移量
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvasRect,
            eventData.pressPosition,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            pointerOffset = contentPanel.anchoredPosition - localPoint;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || contentPanel == null || parentCanvasRect == null) return;

        // 将当前鼠标屏幕位置转换为父容器的局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            // 直接设置面板位置 = 鼠标位置 + 初始偏移
            Vector2 newPos = localPoint + pointerOffset;
            contentPanel.anchoredPosition = ClampAnchoredPos(newPos);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }
    #endregion
}