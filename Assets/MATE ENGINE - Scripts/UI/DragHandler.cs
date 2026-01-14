using UnityEngine;
using UnityEngine.EventSystems;

public class DragHandler : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerUpHandler
{
    [Header("Drag Settings")]
    [Range(0f, 100f)] public float dragSmooth = 0f;
    
    public bool IsDragging => _isDragging;
    private bool _isDragging = false;
    
    private Vector2 _grabOffset;
    private Kirurobo.UniWindowController _uniwinc;
    
    private Vector2 _dragTarget;
    private Vector2 _dragVel;
    private const float MaxSmoothTime = 0.35f;
    
    void Start()
    {
        _uniwinc = GameObject.FindAnyObjectByType<Kirurobo.UniWindowController>();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_uniwinc == null) return;
        
        // Calculate grab offset between window position and cursor position
        _grabOffset = _uniwinc.windowPosition - _uniwinc.cursorPosition;
        
        _isDragging = true;
        _dragVel = Vector2.zero;
        _dragTarget = _uniwinc.windowPosition;
    }
    
    public void OnEndDrag(PointerEventData eventData) 
    { 
        EndDragging(); 
    }
    
    public void OnPointerUp(PointerEventData eventData) 
    { 
        EndDragging(); 
    }
    
    private void EndDragging()
    {
        _isDragging = false;
        _dragVel = Vector2.zero;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _uniwinc == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        
        // Calculate new window position based on cursor position and grab offset
        Vector2 newWindowPosition = _uniwinc.cursorPosition + _grabOffset;
        _dragTarget = newWindowPosition;
    }
    
    void Update()
    {
        if (!_isDragging || _uniwinc == null) return;
        
        float t = Mathf.Clamp01(dragSmooth * 0.01f) * MaxSmoothTime;
        if (t <= 0f)
        {
            _uniwinc.windowPosition = _dragTarget;
        }
        else
        {
            _uniwinc.windowPosition = Vector2.SmoothDamp(_uniwinc.windowPosition, _dragTarget, ref _dragVel, t);
        }
    }
}
