using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// NewChatMenuPanel位置跟随器
/// 支持拖拽移动聊天面板
/// </summary>
public class NewChatMenuPanelFollower : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("面板设置")]
    [Tooltip("聊天面板内的内容容器Panel（实际移动的对象）")]
    public RectTransform contentPanel;

    [Tooltip("用于拖动的区域（Top组件）")]
    public RectTransform dragArea;

    [Tooltip("面板边缘与屏幕边缘的最小距离")]
    public float edgePadding = 20f;

    private bool isDragging = false;

    #region 拖动处理
    public void OnPointerDown(PointerEventData eventData)
    {
        if (contentPanel == null) return;

        // 检查是否点击了拖动区域
        if (dragArea != null)
        {
            RectTransform clickedRect = eventData.pointerCurrentRaycast.gameObject.GetComponent<RectTransform>();
            if (clickedRect != dragArea)
            {
                return; // 点击的不是拖动区域，不处理
            }
        }

        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || contentPanel == null) return;

        // 直接使用屏幕坐标移动
        Vector3 currentScreenPos = contentPanel.position;
        Vector3 deltaScreenPos = eventData.delta;

        currentScreenPos.x += deltaScreenPos.x;
        currentScreenPos.y += deltaScreenPos.y;

        // 限制在屏幕范围内
        currentScreenPos.x = Mathf.Clamp(currentScreenPos.x, edgePadding, Screen.width - edgePadding);
        currentScreenPos.y = Mathf.Clamp(currentScreenPos.y, edgePadding, Screen.height - edgePadding);

        contentPanel.position = currentScreenPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }
    #endregion
}
