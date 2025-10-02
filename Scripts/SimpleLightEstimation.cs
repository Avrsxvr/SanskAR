using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(Light))]
public class SimpleLightEstimation : MonoBehaviour
{
    [SerializeField] private ARCameraManager cameraManager;
    [SerializeField] private float intensityMultiplier = 1.0f;
    [SerializeField] private float maxIntensity = 3.0f;
    [SerializeField] private float minIntensity = 0.1f;
    
    private Light sceneLight;
    private float defaultIntensity;
    private Color defaultColor;
    
    void Awake()
    {
        sceneLight = GetComponent<Light>();
        
        // Store default values
        defaultIntensity = sceneLight.intensity;
        defaultColor = sceneLight.color;
        
        // Validate camera manager assignment
        if (cameraManager == null)
        {
            Debug.LogError("ARCameraManager not assigned to SimpleLightEstimation script!");
            return;
        }
        
        // Subscribe to frame events
        cameraManager.frameReceived += OnFrameReceived;
    }
    
    void OnFrameReceived(ARCameraFrameEventArgs args)
    {
        // Check if light estimation data exists
        if (args.lightEstimation == null)
            return;
            
        // Update brightness if available
        if (args.lightEstimation.averageBrightness.HasValue)
        {
            float brightness = args.lightEstimation.averageBrightness.Value * intensityMultiplier;
            sceneLight.intensity = Mathf.Clamp(brightness, minIntensity, maxIntensity);
        }
        
        // Update color correction if available
        if (args.lightEstimation.colorCorrection.HasValue)
        {
            sceneLight.color = args.lightEstimation.colorCorrection.Value;
        }
    }
    
    void OnDestroy()
    {
        // Clean up event subscription to prevent memory leaks
        if (cameraManager != null)
        {
            cameraManager.frameReceived -= OnFrameReceived;
        }
    }
    
    void OnDisable()
    {
        // Reset to default values when disabled
        if (sceneLight != null)
        {
            sceneLight.intensity = defaultIntensity;
            sceneLight.color = defaultColor;
        }
    }
}