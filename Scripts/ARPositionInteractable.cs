using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARPositionInteractable : MonoBehaviour
{
    [Header("AR Components")]
    public ARRaycastManager raycastManager;
    public Camera arCamera;
    
    [Header("Position Settings")]
    public LayerMask interactableLayer = -1;
    public float moveSpeed = 8f;
    public bool enableDragMovement = true;
    public bool enableTapToMove = true;
    public float heightOffset = 0f; // Keep object at specific height above surface
    
    [Header("Movement Constraints")]
    public bool constrainToPlanes = true;
    public float maxMoveDistance = 5f; // Maximum distance from initial placement
    
    private GameObject selectedObject;
    private bool isDragging = false;
    private Vector3 initialPosition;
    private Vector3 dragOffset;
    
    // Raycast results
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Start()
    {
        // Auto-assign components if not set
        if (raycastManager == null)
            raycastManager = FindObjectOfType<ARRaycastManager>();
            
        if (arCamera == null)
            arCamera = Camera.main;
    }

    void Update()
    {
        HandlePositionInput();
    }

    void HandlePositionInput()
    {
        if (Input.touchCount != 1)
        {
            isDragging = false;
            return;
        }

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                HandleTouchStart(touch.position);
                break;
                
            case TouchPhase.Moved:
                if (isDragging && selectedObject != null)
                    HandleDragMovement(touch.position);
                break;
                
            case TouchPhase.Ended:
                isDragging = false;
                if (selectedObject != null)
                    SnapToSurface();
                break;
        }
    }

    void HandleTouchStart(Vector2 screenPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        // Check if touching an interactable object
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, interactableLayer))
        {
            SelectObjectForMovement(hit.collider.gameObject, hit.point);
        }
        else if (enableTapToMove && selectedObject != null)
        {
            // Move selected object to tapped location
            MoveObjectToScreenPosition(screenPosition);
        }
    }

    void SelectObjectForMovement(GameObject obj, Vector3 hitPoint)
    {
        selectedObject = obj;
        
        if (enableDragMovement)
        {
            isDragging = true;
            dragOffset = selectedObject.transform.position - hitPoint;
            
            // Store initial position for constraint checking
            if (initialPosition == Vector3.zero)
                initialPosition = selectedObject.transform.position;
        }
        
        // Visual feedback
        HighlightObject(selectedObject, true);
    }

    void HandleDragMovement(Vector2 screenPosition)
    {
        if (constrainToPlanes)
        {
            // Raycast to AR planes for movement
            if (raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                Vector3 targetPosition = hits[0].pose.position + Vector3.up * heightOffset;
                MoveObjectToPosition(targetPosition);
            }
        }
        else
        {
            // Free movement based on camera distance
            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            float distanceFromCamera = Vector3.Distance(arCamera.transform.position, selectedObject.transform.position);
            Vector3 targetPosition = ray.GetPoint(distanceFromCamera) + dragOffset;
            MoveObjectToPosition(targetPosition);
        }
    }

    void MoveObjectToScreenPosition(Vector2 screenPosition)
    {
        if (raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Vector3 targetPosition = hits[0].pose.position + Vector3.up * heightOffset;
            MoveObjectToPosition(targetPosition);
        }
    }

    void MoveObjectToPosition(Vector3 targetPosition)
    {
        if (selectedObject == null) return;

        // Apply movement constraints
        if (initialPosition != Vector3.zero && maxMoveDistance > 0)
        {
            float distance = Vector3.Distance(initialPosition, targetPosition);
            if (distance > maxMoveDistance)
            {
                Vector3 direction = (targetPosition - initialPosition).normalized;
                targetPosition = initialPosition + direction * maxMoveDistance;
            }
        }

        // Smooth movement
        selectedObject.transform.position = Vector3.Lerp(
            selectedObject.transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    void SnapToSurface()
    {
        if (selectedObject == null) return;

        // Raycast downward to snap object to surface
        Vector3 rayStart = selectedObject.transform.position + Vector3.up * 0.5f;
        Ray downwardRay = new Ray(rayStart, Vector3.down);
        
        if (Physics.Raycast(downwardRay, out RaycastHit hit))
        {
            Vector3 snapPosition = hit.point + Vector3.up * heightOffset;
            selectedObject.transform.position = snapPosition;
        }
    }

    void HighlightObject(GameObject obj, bool highlight)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (highlight)
                renderer.material.color = Color.cyan;
            else
                renderer.material.color = Color.white;
        }
    }

    // Public methods for external control
    public void SetSelectedObject(GameObject obj)
    {
        if (selectedObject != null)
            HighlightObject(selectedObject, false);
            
        selectedObject = obj;
        if (selectedObject != null)
        {
            HighlightObject(selectedObject, true);
            initialPosition = selectedObject.transform.position;
        }
    }

    public void MoveSelectedObject(Vector3 direction, float distance)
    {
        if (selectedObject != null)
        {
            Vector3 targetPosition = selectedObject.transform.position + direction.normalized * distance;
            MoveObjectToPosition(targetPosition);
        }
    }

    public void MoveUp(float distance = 0.1f)
    {
        MoveSelectedObject(Vector3.up, distance);
    }

    public void MoveDown(float distance = 0.1f)
    {
        MoveSelectedObject(Vector3.down, distance);
    }

    public void MoveForward(float distance = 0.1f)
    {
        MoveSelectedObject(arCamera.transform.forward, distance);
    }

    public void MoveBackward(float distance = 0.1f)
    {
        MoveSelectedObject(-arCamera.transform.forward, distance);
    }

    public void MoveLeft(float distance = 0.1f)
    {
        MoveSelectedObject(-arCamera.transform.right, distance);
    }

    public void MoveRight(float distance = 0.1f)
    {
        MoveSelectedObject(arCamera.transform.right, distance);
    }

    public void ResetToInitialPosition()
    {
        if (selectedObject != null && initialPosition != Vector3.zero)
        {
            selectedObject.transform.position = initialPosition;
        }
    }

    public GameObject GetSelectedObject()
    {
        return selectedObject;
    }

    public void ClearSelection()
    {
        if (selectedObject != null)
        {
            HighlightObject(selectedObject, false);
            selectedObject = null;
        }
    }
}