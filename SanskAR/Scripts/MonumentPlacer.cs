using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MonumentPlacer : MonoBehaviour
{
    [Header("Placement Settings")]
    public GameObject placementMarkerPrefab;
    
    [Header("Monument Models")]
    public GameObject[] monumentPrefabs; // Array of different monument models to place
    
    [Header("3D Monument Placement Settings")]
    [Tooltip("Distance from placement marker along world forward direction (Z-axis)")]
    public float forwardDistance = 4f;
    [Tooltip("Distance from placement marker along world right direction (X-axis)")]
    public float rightDistance = 0f;
    [Tooltip("Vertical distance from placement marker (Y-axis - positive = up, negative = down)")]
    public float upDistance = 0f;
    
    [Header("Legacy Support (Deprecated - Use 3D distances above)")]
    [Tooltip("Legacy: Use forwardDistance instead")]
    public float viewingDistance = 4f;
    [Tooltip("Legacy: Use upDistance instead")]
    public float yOffset = 0f;
    
    [Header("Monument Scale & Orientation")]
    [Tooltip("Scale factor for monuments (adjust based on real-world size)")]
    public float monumentScaleFactor = 0.1f;
    [Tooltip("Should monument always face the camera?")]
    public bool faceCamera = true;
    [Tooltip("Custom rotation offset (applied after camera facing if enabled)")]
    public Vector3 rotationOffset = Vector3.zero;
    
    [Header("Animation Settings")]
    public float scaleUpDuration = 1f;
    public AnimationCurve scaleUpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    // Private variables for placement marker functionality
    private GameObject placementMarker;
    private ARRaycastManager raycastManager;
    private ARPlaneManager planeManager;
    private Camera arCamera;
    private Pose placementPose;
    private bool placementPoseIsValid = false;
    
    // Private variables for monument placement
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
    private GameObject currentPlacedMonument;
    
    // CRITICAL: Store the locked position when monument is placed
    private Vector3 lockedMonumentPosition;
    private bool monumentPositionLocked = false;
    private Pose lockedPlacementPose; // Store the placement pose when monument was placed
    
    // Store original prefab transforms for preservation
    private Dictionary<GameObject, TransformData> originalPrefabTransforms = new Dictionary<GameObject, TransformData>();
    
    [System.Serializable]
    private struct TransformData
    {
        public Vector3 localScale;
        public Quaternion localRotation;
        
        public TransformData(Vector3 scale, Quaternion rotation)
        {
            localScale = scale;
            localRotation = rotation;
        }
    }
    
    void Start()
    {
        InitializeComponents();
        InitializePlacementMarker();
        HidePlaneVisuals();
        CachePrefabTransforms();
        SyncLegacyValues();
    }
    
    void SyncLegacyValues()
    {
        // Sync legacy values with new 3D system
        if (forwardDistance == 4f && viewingDistance != 4f)
        {
            forwardDistance = viewingDistance;
        }
        if (upDistance == 0f && yOffset != 0f)
        {
            upDistance = yOffset;
        }
    }
    
    void CachePrefabTransforms()
    {
        // Store original transform data from prefabs
        foreach (GameObject prefab in monumentPrefabs)
        {
            if (prefab != null)
            {
                originalPrefabTransforms[prefab] = new TransformData(
                    prefab.transform.localScale,
                    prefab.transform.localRotation
                );
            }
        }
    }
    
    void InitializeComponents()
    {
        // Get AR components from XR Origin
        raycastManager = GetComponent<ARRaycastManager>();
        planeManager = GetComponent<ARPlaneManager>();
        
        // Get camera from XR Origin structure (Main Camera child)
        arCamera = GetComponentInChildren<Camera>();
        
        // Alternative ways to find the camera in XR Origin
        if (arCamera == null)
        {
            // Try finding by name (common XR Origin camera names)
            Transform cameraTransform = transform.Find("Main Camera") ?? 
                                       transform.Find("Camera Offset/Main Camera") ??
                                       transform.Find("CameraOffset/Main Camera");
            
            if (cameraTransform != null)
            {
                arCamera = cameraTransform.GetComponent<Camera>();
            }
        }
        
        // Last resort: find any camera in the scene tagged as MainCamera
        if (arCamera == null)
        {
            arCamera = Camera.main;
        }
        
        // Validation
        if (raycastManager == null)
        {
            Debug.LogError("ARRaycastManager component not found! Please attach this script to the XR Origin GameObject.");
            enabled = false;
            return;
        }
        
        if (arCamera == null)
        {
            Debug.LogError("Camera not found! Make sure Main Camera is a child of XR Origin!");
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
        
        Debug.Log($"MonumentPlacer initialized successfully with camera: {arCamera.name}");
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
        
        // Only update placement pose and marker when no monument is placed
        if (!monumentPositionLocked)
        {
            UpdatePlacementPose();
            UpdatePlacementMarker();
        }
        else
        {
            // Hide placement marker when monument is locked in position
            if (placementMarker != null)
                placementMarker.SetActive(false);
        }
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
                // Get the hit pose - this is where monuments will be placed
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
    
    Vector3 CalculateMonumentPosition(GameObject selectedPrefab = null)
    {
        // If monument position is locked, return the locked position
        if (monumentPositionLocked)
        {
            return lockedMonumentPosition;
        }
        
        if (!placementPoseIsValid) 
            return placementPose.position;
        
        // Start from placement marker position (this is on the detected plane)
        Vector3 markerPosition = placementPose.position;
        
        // Use world-space directions for camera-independent positioning
        Vector3 worldForward = Vector3.forward;  // World Z-axis
        Vector3 worldRight = Vector3.right;      // World X-axis
        Vector3 worldUp = Vector3.up;           // World Y-axis
        
        // Calculate fixed offset using world coordinates (completely independent of camera)
        Vector3 offset = Vector3.zero;
        offset += worldForward * forwardDistance;  // Move along world Z-axis
        offset += worldRight * rightDistance;     // Move along world X-axis  
        offset += worldUp * upDistance;          // Move along world Y-axis
        
        // IMPORTANT: Adjust for prefab's pivot point and negative Y position
        if (selectedPrefab != null)
        {
            // Get the prefab's original position (before any modifications)
            Vector3 prefabLocalPos = selectedPrefab.transform.localPosition;
            
            // If prefab has negative Y position, it means its pivot is above the base
            // We need to compensate so the monument sits properly on the ground
            if (prefabLocalPos.y < 0)
            {
                // Compensate for negative Y by moving the monument up
                // This ensures the bottom of the monument touches the ground plane
                offset += worldUp * Mathf.Abs(prefabLocalPos.y);
                Debug.Log($"Adjusting for prefab negative Y position: {prefabLocalPos.y}, adding offset: {Mathf.Abs(prefabLocalPos.y)}");
            }
            
            // Also check bounds to ensure proper ground placement
            Renderer prefabRenderer = selectedPrefab.GetComponent<Renderer>();
            if (prefabRenderer == null)
                prefabRenderer = selectedPrefab.GetComponentInChildren<Renderer>();
                
            if (prefabRenderer != null)
            {
                Bounds prefabBounds = prefabRenderer.bounds;
                Vector3 boundsMin = prefabBounds.min;
                Vector3 boundsCenter = prefabBounds.center;
                
                // Calculate how much the bounds extend below the prefab's pivot
                float pivotToBoundsBottom = boundsCenter.y - boundsMin.y;
                
                // If the bounds extend below the pivot (negative relative position)
                if (boundsMin.y < selectedPrefab.transform.position.y)
                {
                    // Additional adjustment based on actual mesh bounds
                    float boundsAdjustment = pivotToBoundsBottom;
                    offset += worldUp * boundsAdjustment * monumentScaleFactor;
                    Debug.Log($"Bounds-based adjustment: {boundsAdjustment * monumentScaleFactor} (scaled)");
                }
            }
        }
        
        // Final monument position
        Vector3 monumentPosition = markerPosition + offset;
        
        return monumentPosition;
    }
    
    Quaternion CalculateMonumentRotation(GameObject selectedPrefab, Vector3 monumentPosition)
    {
        if (selectedPrefab == null) return Quaternion.identity;
        
        // Get preserved rotation from prefab
        Quaternion preservedRotation = originalPrefabTransforms.ContainsKey(selectedPrefab) ? 
            originalPrefabTransforms[selectedPrefab].localRotation : 
            selectedPrefab.transform.localRotation;
        
        if (faceCamera)
        {
            // Orient monument to face the camera (but position is still locked)
            Vector3 directionToCamera = (arCamera.transform.position - monumentPosition).normalized;
            directionToCamera.y = 0; // Keep it horizontal
            Quaternion cameraFacingRotation = Quaternion.LookRotation(-directionToCamera);
            
            // Combine preserved rotation with camera-facing rotation
            Quaternion combinedRotation = cameraFacingRotation * preservedRotation;
            
            // Apply custom rotation offset
            if (rotationOffset != Vector3.zero)
            {
                combinedRotation = combinedRotation * Quaternion.Euler(rotationOffset);
            }
            
            return combinedRotation;
        }
        else
        {
            // Use preserved rotation with custom offset
            Quaternion finalRotation = preservedRotation;
            if (rotationOffset != Vector3.zero)
            {
                finalRotation = finalRotation * Quaternion.Euler(rotationOffset);
            }
            return finalRotation;
        }
    }
    
    // Public methods for placing different monuments - call these from UI buttons
    public void PlaceMonument(int monumentIndex)
    {
        if (!placementPoseIsValid && !monumentPositionLocked)
        {
            Debug.LogWarning("Cannot place monument - no valid placement position detected. Point device at a surface.");
            return;
        }
        
        if (monumentIndex < 0 || monumentIndex >= monumentPrefabs.Length)
        {
            Debug.LogWarning("Invalid monument index: " + monumentIndex);
            return;
        }
        
        if (monumentPrefabs[monumentIndex] == null)
        {
            Debug.LogWarning("Monument prefab at index " + monumentIndex + " is null");
            return;
        }
        
        // Remove previous monument if it exists
        RemoveCurrentMonument();
        
        // Get selected prefab and preserve its original properties
        GameObject selectedPrefab = monumentPrefabs[monumentIndex];
        
        // LOCK THE POSITION: Calculate and store the monument position when first placing
        if (!monumentPositionLocked)
        {
            lockedMonumentPosition = CalculateMonumentPosition(selectedPrefab); // Pass prefab for Y adjustment
            lockedPlacementPose = placementPose;
            monumentPositionLocked = true;
            Debug.Log($"Monument position LOCKED at: {lockedMonumentPosition}");
        }
        
        // Use the locked position
        Vector3 monumentPosition = lockedMonumentPosition;
        
        // Calculate monument rotation
        Quaternion monumentRotation = CalculateMonumentRotation(selectedPrefab, monumentPosition);
        
        // Create monument at the LOCKED position
        GameObject newMonument = Instantiate(selectedPrefab, monumentPosition, monumentRotation);
        
        // Apply scale factor for monuments and preserve original proportions
        Vector3 preservedScale = originalPrefabTransforms.ContainsKey(selectedPrefab) ? 
            originalPrefabTransforms[selectedPrefab].localScale : 
            selectedPrefab.transform.localScale;
        Vector3 finalScale = preservedScale * monumentScaleFactor;
        newMonument.transform.localScale = finalScale;
        
        // Configure for AR viewing
        SetupMonumentForViewing(newMonument);
        
        // Store as current placed monument
        currentPlacedMonument = newMonument;
        
        // Start spawn animation
        StartCoroutine(SpawnAnimationWithPreservedScale(newMonument, finalScale));
        
        Debug.Log($"Placed monument: {selectedPrefab.name} at LOCKED position: {monumentPosition} " +
                  $"(Original Marker: {lockedPlacementPose.position}, World Offset: X={rightDistance}, Y={upDistance}, Z={forwardDistance}) " +
                  $"Distance from camera: {Vector3.Distance(arCamera.transform.position, monumentPosition):F2}m " +
                  $"Scale: {monumentScaleFactor}, Face Camera: {faceCamera}");
    }
    
    void RemoveCurrentMonument()
    {
        if (currentPlacedMonument != null)
        {
            Destroy(currentPlacedMonument);
            currentPlacedMonument = null;
            Debug.Log("Previous monument removed");
        }
    }
    
    // IMPORTANT: Unlock position when clearing monuments
    public void UnlockMonumentPosition()
    {
        monumentPositionLocked = false;
        Debug.Log("Monument position UNLOCKED - ready for new placement");
    }
    
    void SetupMonumentForViewing(GameObject monument)
    {
        // Add Rigidbody for potential physics interactions
        Rigidbody rb = monument.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = monument.AddComponent<Rigidbody>();
        }
        
        // Configure for AR viewing - stable placement
        rb.mass = 1000f; // Heavy mass for stability
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        rb.useGravity = false; // Monuments don't fall
        rb.isKinematic = true; // Keep it stable
        rb.constraints = RigidbodyConstraints.FreezeAll; // Prevent any movement
        
        // Ensure collider exists for potential interactions
        Collider col = monument.GetComponent<Collider>();
        if (col == null)
        {
            col = monument.GetComponentInChildren<Collider>();
            if (col == null)
            {
                // Add a box collider as fallback
                col = monument.AddComponent<BoxCollider>();
            }
        }
        
        col.isTrigger = false; // Solid collider
        
        // Optional: Add a tag for identification
        monument.tag = "Monument";
    }
    
    IEnumerator SpawnAnimationWithPreservedScale(GameObject obj, Vector3 targetScale)
    {
        // Start from zero scale and animate to the target scale
        obj.transform.localScale = Vector3.zero;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < scaleUpDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / scaleUpDuration;
            float scaleValue = scaleUpCurve.Evaluate(normalizedTime);
            
            obj.transform.localScale = targetScale * scaleValue;
            
            yield return null;
        }
        
        // Ensure final scale is exactly the target scale
        obj.transform.localScale = targetScale;
        
        Debug.Log($"Monument spawn animation completed. Final scale: {obj.transform.localScale}");
    }
    
    // Convenience methods for UI buttons (for specific monuments)
    public void PlaceMonument1() { PlaceMonument(0); }
    public void PlaceMonument2() { PlaceMonument(1); }
    public void PlaceMonument3() { PlaceMonument(2); }
    public void PlaceMonument4() { PlaceMonument(3); }
    public void PlaceMonument5() { PlaceMonument(4); }
    public void PlaceMonument6() { PlaceMonument(5); }
    
    // Updated utility methods
    public void ClearAllMonuments()
    {
        RemoveCurrentMonument();
        UnlockMonumentPosition(); // IMPORTANT: Unlock for new placement
        Debug.Log("Current monument cleared and position unlocked");
    }
    
    public void RemoveLastMonument()
    {
        RemoveCurrentMonument();
        UnlockMonumentPosition(); // IMPORTANT: Unlock for new placement
    }
    
    // 3D Distance adjustment methods - these will only affect NEW placements
    public void SetForwardDistance(float distance)
    {
        forwardDistance = distance;
        viewingDistance = distance; // Keep legacy value in sync
        Debug.Log($"Monument forward distance (World Z) set to: {forwardDistance}m - affects NEW placements only");
        
        // Only reposition if monument is not locked OR if user explicitly wants to unlock and reposition
        if (!monumentPositionLocked)
        {
            RepositionCurrentMonument();
        }
        else
        {
            Debug.Log("Current monument position is LOCKED. Clear monument to use new distance.");
        }
    }
    
    public void SetRightDistance(float distance)
    {
        rightDistance = distance;
        Debug.Log($"Monument right distance (World X) set to: {rightDistance}m - affects NEW placements only");
        
        if (!monumentPositionLocked)
        {
            RepositionCurrentMonument();
        }
        else
        {
            Debug.Log("Current monument position is LOCKED. Clear monument to use new distance.");
        }
    }
    
    public void SetUpDistance(float distance)
    {
        upDistance = distance;
        yOffset = distance; // Keep legacy value in sync
        Debug.Log($"Monument up distance (World Y) set to: {upDistance}m - affects NEW placements only");
        
        if (!monumentPositionLocked)
        {
            RepositionCurrentMonument();
        }
        else
        {
            Debug.Log("Current monument position is LOCKED. Clear monument to use new distance.");
        }
    }
    
    // Set all 3D distances at once
    public void Set3DDistance(Vector3 distances)
    {
        forwardDistance = distances.z;  // Z = forward in world space
        rightDistance = distances.x;    // X = right in world space
        upDistance = distances.y;       // Y = up in world space
        
        // Sync legacy values
        viewingDistance = forwardDistance;
        yOffset = upDistance;
        
        Debug.Log($"Monument 3D distances set to: World X (Right)={rightDistance}, World Y (Up)={upDistance}, World Z (Forward)={forwardDistance}");
        
        if (!monumentPositionLocked)
        {
            RepositionCurrentMonument();
        }
        else
        {
            Debug.Log("Current monument position is LOCKED. Clear monument to use new distances.");
        }
    }
    
    public void Set3DDistance(float forward, float right, float up)
    {
        Set3DDistance(new Vector3(right, up, forward));
    }
    
    // Legacy methods (for backward compatibility)
    public void SetViewingDistance(float distance)
    {
        SetForwardDistance(distance);
    }
    
    public void SetYOffset(float offset)
    {
        SetUpDistance(offset);
    }
    
    void RepositionCurrentMonument()
    {
        // Only reposition if monument is not locked
        if (currentPlacedMonument != null && placementPoseIsValid && !monumentPositionLocked)
        {
            // Find which prefab this monument came from for Y adjustment
            GameObject originalPrefab = null;
            for (int i = 0; i < monumentPrefabs.Length; i++)
            {
                if (monumentPrefabs[i] != null && 
                    currentPlacedMonument.name.Contains(monumentPrefabs[i].name))
                {
                    originalPrefab = monumentPrefabs[i];
                    break;
                }
            }
            
            Vector3 newPosition = CalculateMonumentPosition(originalPrefab); // Pass prefab for Y adjustment
            StartCoroutine(SmoothPositionTransition(currentPlacedMonument, newPosition));
            
            // Update rotation if face camera is enabled
            if (faceCamera && originalPrefab != null)
            {
                Quaternion newRotation = CalculateMonumentRotation(originalPrefab, newPosition);
                StartCoroutine(SmoothRotationTransition(currentPlacedMonument, newRotation));
            }
        }
    }
    
    // Method to force unlock and reposition (for advanced users)
    public void ForceRepositionCurrentMonument()
    {
        if (currentPlacedMonument != null)
        {
            // Find which prefab this monument came from
            GameObject originalPrefab = null;
            for (int i = 0; i < monumentPrefabs.Length; i++)
            {
                if (monumentPrefabs[i] != null && 
                    currentPlacedMonument.name.Contains(monumentPrefabs[i].name))
                {
                    originalPrefab = monumentPrefabs[i];
                    break;
                }
            }
            
            UnlockMonumentPosition();
            lockedMonumentPosition = CalculateMonumentPosition(originalPrefab); // Pass prefab for Y adjustment
            monumentPositionLocked = true;
            
            StartCoroutine(SmoothPositionTransition(currentPlacedMonument, lockedMonumentPosition));
            Debug.Log($"Monument position force-updated and re-locked at: {lockedMonumentPosition}");
        }
    }
    
    // Method to adjust monument scale at runtime
    public void SetMonumentScale(float scaleFactor)
    {
        monumentScaleFactor = Mathf.Max(0.01f, scaleFactor);
        Debug.Log($"Monument scale factor set to: {monumentScaleFactor}");
        
        // Apply to current monument if exists
        if (currentPlacedMonument != null)
        {
            ApplyScaleToCurrentMonument();
        }
    }
    
    // Toggle camera facing - this can work even with locked position
    public void SetFaceCamera(bool shouldFaceCamera)
    {
        faceCamera = shouldFaceCamera;
        Debug.Log($"Monument face camera set to: {faceCamera}");
        
        if (currentPlacedMonument != null)
        {
            // Update rotation based on new setting (position stays locked)
            for (int i = 0; i < monumentPrefabs.Length; i++)
            {
                if (monumentPrefabs[i] != null && 
                    currentPlacedMonument.name.Contains(monumentPrefabs[i].name))
                {
                    Vector3 monumentPos = currentPlacedMonument.transform.position;
                    Quaternion newRotation = CalculateMonumentRotation(monumentPrefabs[i], monumentPos);
                    StartCoroutine(SmoothRotationTransition(currentPlacedMonument, newRotation));
                    break;
                }
            }
        }
    }
    
    // Set custom rotation offset
    public void SetRotationOffset(Vector3 offset)
    {
        rotationOffset = offset;
        Debug.Log($"Monument rotation offset set to: {rotationOffset}");
        
        if (currentPlacedMonument != null)
        {
            // Update rotation with new offset (position stays locked)
            SetFaceCamera(faceCamera); // Trigger rotation recalculation
        }
    }
    
    void ApplyScaleToCurrentMonument()
    {
        if (currentPlacedMonument == null) return;
        
        // Find which prefab this monument came from
        for (int i = 0; i < monumentPrefabs.Length; i++)
        {
            if (monumentPrefabs[i] != null && 
                currentPlacedMonument.name.Contains(monumentPrefabs[i].name))
            {
                Vector3 originalScale = originalPrefabTransforms.ContainsKey(monumentPrefabs[i]) ?
                    originalPrefabTransforms[monumentPrefabs[i]].localScale :
                    monumentPrefabs[i].transform.localScale;
                Vector3 newScale = originalScale * monumentScaleFactor;
                
                StartCoroutine(SmoothScaleTransition(currentPlacedMonument, newScale));
                break;
            }
        }
    }
    
    IEnumerator SmoothPositionTransition(GameObject obj, Vector3 targetPosition)
    {
        Vector3 startPosition = obj.transform.position;
        float duration = 1f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            obj.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }
        
        obj.transform.position = targetPosition;
    }
    
    IEnumerator SmoothRotationTransition(GameObject obj, Quaternion targetRotation)
    {
        Quaternion startRotation = obj.transform.rotation;
        float duration = 1f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            obj.transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            yield return null;
        }
        
        obj.transform.rotation = targetRotation;
    }
    
    IEnumerator SmoothScaleTransition(GameObject obj, Vector3 targetScale)
    {
        Vector3 startScale = obj.transform.localScale;
        float duration = 0.5f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            obj.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        
        obj.transform.localScale = targetScale;
    }
    
    // Public getters for external scripts
    public bool IsPlacementValid()
    {
        return placementPoseIsValid || monumentPositionLocked;
    }
    
    public Pose GetPlacementPose()
    {
        return monumentPositionLocked ? lockedPlacementPose : placementPose;
    }
    
    public Vector3 GetMonumentPlacementPosition()
    {
        return CalculateMonumentPosition(); // Will return locked position if locked, no prefab needed for getter
    }
    
    public bool IsMonumentPositionLocked()
    {
        return monumentPositionLocked;
    }
    
    public Vector3 GetLockedMonumentPosition()
    {
        return lockedMonumentPosition;
    }
    
    // 3D distance getters
    public float GetForwardDistance()
    {
        return forwardDistance;
    }
    
    public float GetRightDistance()
    {
        return rightDistance;
    }
    
    public float GetUpDistance()
    {
        return upDistance;
    }
    
    public Vector3 Get3DDistance()
    {
        return new Vector3(rightDistance, upDistance, forwardDistance);
    }
    
    // Legacy getters (for backward compatibility)
    public float GetViewingDistance()
    {
        return forwardDistance;
    }
    
    public float GetYOffset()
    {
        return upDistance;
    }
    
    public bool HasPlacedMonument()
    {
        return currentPlacedMonument != null;
    }
    
    public GameObject GetCurrentPlacedMonument()
    {
        return currentPlacedMonument;
    }
    
    public float GetScaleFactor()
    {
        return monumentScaleFactor;
    }
    
    public bool GetFaceCamera()
    {
        return faceCamera;
    }
    
    public Vector3 GetRotationOffset()
    {
        return rotationOffset;
    }
    
    // Get preserved transform data for external use
    public Vector3 GetPreservedScale(int monumentIndex)
    {
        if (monumentIndex >= 0 && monumentIndex < monumentPrefabs.Length && monumentPrefabs[monumentIndex] != null)
        {
            Vector3 originalScale = originalPrefabTransforms.ContainsKey(monumentPrefabs[monumentIndex]) ?
                originalPrefabTransforms[monumentPrefabs[monumentIndex]].localScale :
                monumentPrefabs[monumentIndex].transform.localScale;
            return originalScale * monumentScaleFactor;
        }
        return Vector3.one * monumentScaleFactor;
    }
    
    public Quaternion GetPreservedRotation(int monumentIndex)
    {
        if (monumentIndex >= 0 && monumentIndex < monumentPrefabs.Length && monumentPrefabs[monumentIndex] != null)
        {
            return originalPrefabTransforms.ContainsKey(monumentPrefabs[monumentIndex]) ?
                originalPrefabTransforms[monumentPrefabs[monumentIndex]].localRotation :
                monumentPrefabs[monumentIndex].transform.localRotation;
        }
        return Quaternion.identity;
    }
    
    // Cleanup
    void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
        
        // Clean up placed monument
        RemoveCurrentMonument();
        
        // Clean up placement marker
        if (placementMarker != null)
        {
            Destroy(placementMarker);
        }
        
        // Clear cached transforms
        originalPrefabTransforms.Clear();
    }
    
    // Debug visualization - only available in Unity Editor
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (Application.isPlaying && arCamera != null)
        {
            // Draw camera position
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(arCamera.transform.position, 0.2f);
            
            // Draw placement marker position if valid
            if (placementPoseIsValid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(placementPose.position, Vector3.one * 0.1f);
                
                // Draw calculated monument position
                Vector3 monumentPos = CalculateMonumentPosition();
                Gizmos.color = monumentPositionLocked ? Color.red : Color.orange;
                Gizmos.DrawWireCube(monumentPos, Vector3.one * 0.5f);
                
                // Draw world-space offset vectors
                Vector3 markerPos = monumentPositionLocked ? lockedPlacementPose.position : placementPose.position;
                
                // Forward offset (cyan) - World Z axis
                Vector3 forwardOffset = markerPos + (Vector3.forward * forwardDistance);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(markerPos, forwardOffset);
                Gizmos.DrawWireCube(forwardOffset, Vector3.one * 0.2f);
                
                // Right offset (magenta) - World X axis
                Vector3 rightOffset = forwardOffset + (Vector3.right * rightDistance);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(forwardOffset, rightOffset);
                Gizmos.DrawWireCube(rightOffset, Vector3.one * 0.15f);
                
                // Up offset (yellow) - World Y axis
                Vector3 upOffset = rightOffset + (Vector3.up * upDistance);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(rightOffset, upOffset);
                
                // Final monument position should match upOffset
                Gizmos.color = monumentPositionLocked ? Color.red : Color.orange;
                Gizmos.DrawLine(markerPos, monumentPos);
                
                // Draw line from camera to monument
                Gizmos.color = Color.white;
                Gizmos.DrawLine(arCamera.transform.position, monumentPos);
                
                // Show locked position differently
                if (monumentPositionLocked)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(lockedMonumentPosition, 0.3f);
                    UnityEditor.Handles.Label(lockedMonumentPosition + Vector3.up * 1.5f, "LOCKED POSITION", 
                        new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red } });
                }
                
                // Draw distance measurements
                float markerDistance = Vector3.Distance(arCamera.transform.position, markerPos);
                float monumentDistance = Vector3.Distance(arCamera.transform.position, monumentPos);
                
                UnityEditor.Handles.Label(markerPos + Vector3.up * 0.5f, $"Marker: {markerDistance:F1}m");
                UnityEditor.Handles.Label(monumentPos + Vector3.up, 
                    $"Monument: {monumentDistance:F1}m\nWorld: X={rightDistance:F1} Y={upDistance:F1} Z={forwardDistance:F1}\n{(monumentPositionLocked ? "LOCKED" : "FREE")}");
            }
        }
    }
#endif
}