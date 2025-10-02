using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TapEvent : UnityEvent<Vector2>
{
}

public class SimpleARGestureRecognizer : MonoBehaviour
{
    [Header("Gesture Settings")]
    [SerializeField]
    private float tapThreshold = 0.1f; // Maximum time for a tap
    
    [SerializeField]
    private float dragThreshold = 50f; // Minimum distance for a drag
    
    [Header("Events")]
    public TapEvent onTap = new TapEvent();
    
    private Vector2 touchStartPos;
    private float touchStartTime;
    private bool isTouching = false;
    
    void Update()
    {
        HandleTouch();
    }
    
    private void HandleTouch()
    {
        // Handle mouse input for editor testing
        if (Application.isEditor)
        {
            HandleMouseInput();
            return;
        }
        
        // Handle touch input for mobile
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    OnTouchStart(touch.position);
                    break;
                    
                case TouchPhase.Ended:
                    OnTouchEnd(touch.position);
                    break;
                    
                case TouchPhase.Canceled:
                    OnTouchCancel();
                    break;
            }
        }
    }
    
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            OnTouchStart(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            OnTouchEnd(Input.mousePosition);
        }
    }
    
    private void OnTouchStart(Vector2 position)
    {
        touchStartPos = position;
        touchStartTime = Time.time;
        isTouching = true;
    }
    
    private void OnTouchEnd(Vector2 position)
    {
        if (!isTouching) return;
        
        float touchDuration = Time.time - touchStartTime;
        float touchDistance = Vector2.Distance(touchStartPos, position);
        
        // Check if this is a tap gesture
        if (touchDuration <= tapThreshold && touchDistance <= dragThreshold)
        {
            onTap?.Invoke(touchStartPos);
        }
        
        isTouching = false;
    }
    
    private void OnTouchCancel()
    {
        isTouching = false;
    }
}