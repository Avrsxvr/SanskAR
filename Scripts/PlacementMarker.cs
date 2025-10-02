using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlacementMarker : MonoBehaviour
{
    [Header("Placement Settings")]
    public GameObject placementMarkerPrefab;
    
    private GameObject placementMarker;
    private ARRaycastManager raycastManager;
    private Camera arCamera;
    private Pose placementPose;
    private bool placementPoseIsValid = false;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Start()
    {
        // This script should be attached to XR Origin
        // Get the ARRaycastManager component from XR Origin
        raycastManager = GetComponent<ARRaycastManager>();
        
        if (raycastManager == null)
        {
            Debug.LogError("ARRaycastManager component not found! Please attach this script to the XR Origin GameObject that has ARRaycastManager.");
            enabled = false;
            return;
        }

        // Find the camera in XR Origin hierarchy
        arCamera = GetComponentInChildren<Camera>();
        
        if (arCamera == null)
        {
            Debug.LogError("Camera not found in XR Origin children! Make sure XR Origin has Camera Offset > Main Camera structure.");
            enabled = false;
            return;
        }

        // Validate placement marker prefab
        if (placementMarkerPrefab == null)
        {
            Debug.LogError("Placement Marker Prefab is not assigned!");
            enabled = false;
            return;
        }

        // Verify ARPlaneManager exists (for plane detection)
        var planeManager = GetComponent<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogWarning("ARPlaneManager not found on XR Origin! Plane detection may not work. Please add ARPlaneManager component to XR Origin.");
        }

        // Instantiate the placement marker
        placementMarker = Instantiate(placementMarkerPrefab);
        placementMarker.SetActive(false);
        
        Debug.Log($"PlacementMarker initialized successfully with camera: {arCamera.name}");
    }

    void Update()
    {
        if (arCamera == null || raycastManager == null) return;
        
        UpdatePlacementPose();
        UpdatePlacementMarker();
    }

    void UpdatePlacementPose()
    {
        // Cast a ray from the center of the screen
        var screenCenter = arCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));

        // Perform the raycast against detected planes
        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            placementPoseIsValid = hits.Count > 0;

            if (placementPoseIsValid)
            {
                // Get the hit pose (position and plane alignment)
                placementPose = hits[0].pose;
                
                // Apply -90 degree rotation on X-axis to make the marker lie flat on the ground
                // and set Y rotation to 0 as requested
                Vector3 eulerAngles = placementPose.rotation.eulerAngles;
                placementPose.rotation = Quaternion.Euler(-90f, 0f, eulerAngles.z);
                
                // Alternative: If you want the marker to always face the same direction
                // placementPose.rotation = Quaternion.Euler(-90f, 0f, 0f);
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

    // Public method to get current placement pose (useful for placing objects)
    public Pose GetPlacementPose()
    {
        return placementPose;
    }

    // Public method to check if placement is valid
    public bool IsPlacementValid()
    {
        return placementPoseIsValid;
    }

    // Optional: Method to place an object at current placement pose
    public GameObject PlaceObject(GameObject prefab)
    {
        if (placementPoseIsValid && prefab != null)
        {
            return Instantiate(prefab, placementPose.position, placementPose.rotation);
        }
        return null;
    }

    // Optional: Draw debug information in Scene view
    void OnDrawGizmos()
    {
        if (placementPoseIsValid && Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(placementPose.position, Vector3.one * 0.1f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(placementPose.position, placementPose.rotation * Vector3.forward * 0.2f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawRay(placementPose.position, placementPose.rotation * Vector3.right * 0.1f);
        }
    }
}