using UnityEngine;

public class PinchToScale : MonoBehaviour
{
    [Header("Scaling Settings")]
    public float minScale = 0.3f;   // Minimum scale limit
    public float maxScale = 3.0f;   // Maximum scale limit
    public float sensitivity = 0.01f; // How responsive the scaling is
    public float elasticity = 0.1f;   // Optional: adds "bounce back" effect at limits

    private Vector3 initialScale;
    private float initialDistance;

    private void Start()
    {
        initialScale = transform.localScale;
    }

    private void Update()
    {
        // Only detect pinch if there are 2 touches
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // Find the position difference between the touches in this frame and the last
            Vector2 prevTouch0Pos = touch0.position - touch0.deltaPosition;
            Vector2 prevTouch1Pos = touch1.position - touch1.deltaPosition;

            float prevMagnitude = (prevTouch0Pos - prevTouch1Pos).magnitude;
            float currentMagnitude = (touch0.position - touch1.position).magnitude;

            // Difference in distances between touches
            float difference = currentMagnitude - prevMagnitude;

            // Scale object
            ScaleObject(difference * sensitivity);
        }
    }

    private void ScaleObject(float increment)
    {
        Vector3 newScale = transform.localScale + Vector3.one * increment;

        // Clamp the scale
        newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
        newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
        newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);

        // Apply elasticity at the edges
        if (transform.localScale.x <= minScale || transform.localScale.x >= maxScale)
        {
            newScale = Vector3.Lerp(transform.localScale, newScale, 1f - elasticity);
        }

        transform.localScale = newScale;
    }
}
