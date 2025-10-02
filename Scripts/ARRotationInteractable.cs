using UnityEngine;

public class ARRotationInteractable : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationRateDrag = 100f;   // Speed for one-finger drag
    public float rotationRateTwist = 2f;    // Speed multiplier for two-finger twist

    private bool isDragging = false;
    private Vector2 lastTouchPosition;

    void Update()
    {
        if (Input.touchCount == 1) // Single finger drag -> Rotate on Y
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                isDragging = true;
                lastTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                Vector2 delta = touch.position - lastTouchPosition;
                float rotationY = delta.x * rotationRateDrag * Time.deltaTime;
                transform.Rotate(0f, -rotationY, 0f, Space.World); // Rotate around Y-axis
                lastTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }
        }
        else if (Input.touchCount == 2) // Two finger twist -> Rotate
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // Find the angle difference between touches
            Vector2 prevDir = (touch0.position - touch0.deltaPosition) - (touch1.position - touch1.deltaPosition);
            Vector2 currDir = touch0.position - touch1.position;

            float angle = Vector2.SignedAngle(prevDir, currDir);
            transform.Rotate(0f, -angle * rotationRateTwist, 0f, Space.World);
        }
    }
}