using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectPlacementManager : MonoBehaviour
{
    [Header("Placement Settings")]
    public GameObject placementMarkerPrefab;
    
    [Header("Food Models")]
    public GameObject[] foodPrefabs; // Array of different food models to place
    
    [Header("Animation Settings")]
    public float scaleUpDuration = 0.5f;
    public AnimationCurve scaleUpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    // Private variables for placement marker functionality
    private GameObject placementMarker;
    private ARRaycastManager raycastManager;
    private ARPlaneManager planeManager;
    private Camera arCamera;
    private Pose placementPose;
    private bool placementPoseIsValid = false;
    
    // Private variables for object placement
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
    private GameObject currentPlacedObject; // Store only one placed object at a time
    
    void Start()
    {
        InitializeComponents();
        InitializePlacementMarker();
        HidePlaneVisuals();
    }
    
    void InitializeComponents()
    {
        // Get AR components from XR Origin
        raycastManager = GetComponent<ARRaycastManager>();
        planeManager = GetComponent<ARPlaneManager>();
        arCamera = GetComponentInChildren<Camera>();
        
        // Validation
        if (raycastManager == null)
        {
            Debug.LogError("ARRaycastManager component not found! Please attach this script to the XR Origin GameObject.");
            enabled = false;
            return;
        }
        
        if (arCamera == null)
        {
            Debug.LogError("Camera not found in XR Origin children!");
            enabled = false;
            return;
        }
        
        if (placementMarkerPrefab == null)
        {
            Debug.LogError("Placement Marker Prefab is not assigned!");
            enabled = false;
            return;
        }
        
        if (planeManager == null)
        {
            Debug.LogWarning("ARPlaneManager not found! Plane detection may not work properly.");
        }
        
        Debug.Log("ARObjectPlacementManager initialized successfully");
    }
    
    void InitializePlacementMarker()
    {
        // Create placement marker
        placementMarker = Instantiate(placementMarkerPrefab);
        placementMarker.SetActive(false);
    }
    
    void HidePlaneVisuals()
    {
        // Hide plane visual representations while keeping detection active
        if (planeManager != null)
        {
            foreach (var plane in planeManager.trackables)
            {
                HidePlane(plane);
            }
            
            // Subscribe to plane events to hide new planes
            planeManager.planesChanged += OnPlanesChanged;
        }
    }
    
    void OnPlanesChanged(ARPlanesChangedEventArgs eventArgs)
    {
        // Hide newly added planes
        foreach (var plane in eventArgs.added)
        {
            if (plane != null)
                HidePlane(plane);
        }
        
        foreach (var plane in eventArgs.updated)
        {
            if (plane != null)
                HidePlane(plane);
        }
    }
    
    void HidePlane(ARPlane plane)
    {
        var meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
        
        var lineRenderer = plane.GetComponent<LineRenderer>();
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }
    
    void Update()
    {
        if (arCamera == null || raycastManager == null) return;
        
        UpdatePlacementPose();
        UpdatePlacementMarker();
    }
    
    void UpdatePlacementPose()
    {
        // Cast ray from screen center
        var screenCenter = arCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));
        
        // Perform raycast against detected planes
        if (raycastManager.Raycast(screenCenter, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            placementPoseIsValid = raycastHits.Count > 0;
            
            if (placementPoseIsValid)
            {
                // Get the hit pose
                placementPose = raycastHits[0].pose;
                
                // Adjust rotation to make marker lie flat on the surface
                Vector3 eulerAngles = placementPose.rotation.eulerAngles;
                placementPose.rotation = Quaternion.Euler(-90f, 0f, eulerAngles.z);
            }
        }
        else
        {
            placementPoseIsValid = false;
        }
    }
    
    void UpdatePlacementMarker()
    {
        if (placementMarker == null) return;
        
        if (placementPoseIsValid)
        {
            placementMarker.SetActive(true);
            placementMarker.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
        }
        else
        {
            placementMarker.SetActive(false);
        }
    }
    
    // Public methods for placing different food items - call these from UI buttons
    public void PlaceFood(int foodIndex)
    {
        if (!placementPoseIsValid)
        {
            Debug.LogWarning("Cannot place food: No valid placement surface detected");
            return;
        }
        
        if (foodIndex < 0 || foodIndex >= foodPrefabs.Length)
        {
            Debug.LogWarning("Invalid food index: " + foodIndex);
            return;
        }
        
        if (foodPrefabs[foodIndex] == null)
        {
            Debug.LogWarning("Food prefab at index " + foodIndex + " is null");
            return;
        }
        
        // Remove previous object if it exists (only one object at a time)
        RemoveCurrentFood();
        
        // Create food object at placement pose
        Pose foodPose = placementPose;
        
        // Adjust food rotation to stand upright on the surface
        foodPose.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up), Vector3.up);
        
        GameObject newFood = Instantiate(foodPrefabs[foodIndex], foodPose.position, foodPose.rotation);
        
        // Configure for AR interactions - NO RESTRICTIONS
        SetupFoodForInteractions(newFood);
        
        // Store as current placed object
        currentPlacedObject = newFood;
        
        // Start spawn animation
        StartCoroutine(SpawnAnimation(newFood));
        
        Debug.Log($"Placed food item: {foodPrefabs[foodIndex].name} at position: {foodPose.position}");
    }
    
    void RemoveCurrentFood()
    {
        if (currentPlacedObject != null)
        {
            Destroy(currentPlacedObject);
            currentPlacedObject = null;
            Debug.Log("Previous food item removed");
        }
    }
    
    void SetupFoodForInteractions(GameObject food)
    {
        // Ensure the food has a Rigidbody
        Rigidbody rb = food.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = food.AddComponent<Rigidbody>();
        }
        
        // Configure for AR interactions - completely unrestricted
        rb.mass = 1f;
        rb.linearDamping = 0f;       // No damping
        rb.angularDamping = 0f;      // No angular damping  
        rb.useGravity = false;       // No gravity
        rb.isKinematic = true;       // Kinematic for AR interactions
        rb.constraints = RigidbodyConstraints.None; // No constraints at all
        
        // Ensure collider exists
        Collider col = food.GetComponent<Collider>();
        if (col == null)
        {
            col = food.GetComponentInChildren<Collider>();
            if (col == null)
            {
                col = food.AddComponent<BoxCollider>();
            }
        }
        
        // Make sure collider is solid (not trigger)
        col.isTrigger = false;
    }
    
    IEnumerator SpawnAnimation(GameObject obj)
    {
        Vector3 originalScale = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < scaleUpDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / scaleUpDuration;
            float scaleValue = scaleUpCurve.Evaluate(normalizedTime);
            
            obj.transform.localScale = originalScale * scaleValue;
            
            yield return null;
        }
        
        // Ensure final scale is correct
        obj.transform.localScale = originalScale;
    }
    
    // Convenience methods for UI buttons (for specific food items)
    public void PlaceFood1() { PlaceFood(0); }
    public void PlaceFood2() { PlaceFood(1); }
    public void PlaceFood3() { PlaceFood(2); }
    public void PlaceFood4() { PlaceFood(3); }
    public void PlaceFood5() { PlaceFood(4); }
    public void PlaceFood6() { PlaceFood(5); }
    
    // Utility methods
    public void ClearAllFood()
    {
        RemoveCurrentFood();
        Debug.Log("Current food item cleared");
    }
    
    public void RemoveLastFood()
    {
        RemoveCurrentFood();
    }
    
    // Public getters for external scripts
    public bool IsPlacementValid()
    {
        return placementPoseIsValid;
    }
    
    public Pose GetPlacementPose()
    {
        return placementPose;
    }
    
    public int GetPlacedFoodCount()
    {
        return currentPlacedObject != null ? 1 : 0;
    }
    
    public bool HasPlacedFood()
    {
        return currentPlacedObject != null;
    }
    
    public GameObject GetCurrentPlacedObject()
    {
        return currentPlacedObject;
    }
    
    // Cleanup
    void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
        
        // Clean up placed object
        RemoveCurrentFood();
        
        // Clean up placement marker
        if (placementMarker != null)
        {
            Destroy(placementMarker);
        }
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (placementPoseIsValid && Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(placementPose.position, Vector3.one * 0.1f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(placementPose.position, placementPose.rotation * Vector3.forward * 0.2f);
        }
    }
}