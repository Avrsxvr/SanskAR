using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class UIScreenNavigator : MonoBehaviour
{
    [Header("Animation Settings")]
    public float transitionDuration = 0.5f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Back Navigation")]
    public bool enableAndroidBackButton = true;
    public bool enableBackButtonHistory = true;
    public int maxHistorySize = 10;
    
    [Header("Screen Settings")]
    public GameObject defaultScreen; // Screen to show first (but will start invisible)
    public GameObject homeScreen; // NEW: Home screen to show at start
    public bool preloadScreens = true; // Keep screens loaded but invisible
    public bool startWithAllInvisible = true; // NEW: Start with all canvases invisible
    public bool showHomeScreenAtStart = true; // NEW: Show home screen immediately at start
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    private GameObject currentScreen;
    private Stack<GameObject> screenHistory = new Stack<GameObject>();
    private bool isTransitioning = false;
    private bool hasStartedNavigation = false; // Track if any navigation has started
    
    // Events for screen changes
    public System.Action<GameObject, GameObject> OnScreenChanged;
    public System.Action OnBackToDefault;

    private void Start()
    {
        InitializeNavigator();
    }

    private void Update()
    {
        // Handle Android back button - supports both old and new Input System
        bool backPressed = false;
        
        #if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            backPressed = true;
        }
        #endif
        
        #if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            backPressed = true;
        }
        #endif
        
        if (enableAndroidBackButton && backPressed)
        {
            HandleBackNavigation();
        }
    }

    private void InitializeNavigator()
    {
        if (startWithAllInvisible)
        {
            // Make ALL canvases invisible at start
            MakeAllCanvasesInvisible();
            
            // Determine which screen to use as starting screen
            GameObject startingScreen = homeScreen != null ? homeScreen : defaultScreen;
            
            if (startingScreen != null)
            {
                currentScreen = startingScreen;
                
                // Show home screen immediately if enabled
                if (showHomeScreenAtStart && homeScreen != null)
                {
                    ActivateScreen(homeScreen);
                    hasStartedNavigation = true;
                    if (showDebugLogs)
                        Debug.Log($"Home screen shown at start: {homeScreen.name}");
                }
                else if (showDebugLogs)
                {
                    Debug.Log($"Starting screen set to: {currentScreen.name} (but kept invisible until first navigation)");
                }
            }
            
            if (!showHomeScreenAtStart && showDebugLogs)
                Debug.Log("All canvases initialized as invisible. Use NavigateTo() to show first screen.");
        }
        else
        {
            // Original behavior - find first active screen
            currentScreen = GetActiveScreen();
            
            if (currentScreen == null && defaultScreen != null)
            {
                currentScreen = defaultScreen;
                ActivateScreen(currentScreen);
            }
            
            if (preloadScreens)
            {
                PreloadAllScreens();
            }
        }
    }

    private void MakeAllCanvasesInvisible()
    {
        foreach (Transform child in transform)
        {
            GameObject screen = child.gameObject;
            // Ensure screen is active so components work, but make it invisible
            screen.SetActive(true);
            CanvasGroup cg = GetOrAddCanvasGroup(screen);
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            
            if (showDebugLogs)
                Debug.Log($"Made canvas invisible: {screen.name}");
        }
    }

    private void PreloadAllScreens()
    {
        foreach (Transform child in transform)
        {
            GameObject screen = child.gameObject;
            if (screen != currentScreen)
            {
                // Activate to ensure components are initialized, then make invisible
                screen.SetActive(true);
                CanvasGroup cg = GetOrAddCanvasGroup(screen);
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }
    }

    // Main navigation method - can be called from buttons
    public void NavigateTo(GameObject nextScreen)
    {
        NavigateTo(nextScreen, true);
    }

    // Overloaded method with history control
    public void NavigateTo(GameObject nextScreen, bool addToHistory)
    {
        if (isTransitioning || nextScreen == null)
        {
            if (showDebugLogs && isTransitioning)
                Debug.Log("Transition already in progress, ignoring request");
            return;
        }

        // Handle first navigation when starting with all invisible
        if (!hasStartedNavigation && startWithAllInvisible)
        {
            hasStartedNavigation = true;
            if (showDebugLogs)
                Debug.Log($"First navigation started to: {nextScreen.name}");
            
            // For first navigation, just show the screen without transition
            ActivateScreen(nextScreen);
            currentScreen = nextScreen;
            OnScreenChanged?.Invoke(null, currentScreen);
            return;
        }

        if (currentScreen == null)
        {
            ActivateScreen(nextScreen);
            currentScreen = nextScreen;
            if (showDebugLogs)
                Debug.Log($"No current screen, directly activated: {nextScreen.name}");
            return;
        }

        if (currentScreen == nextScreen)
        {
            if (showDebugLogs)
                Debug.Log("Already on target screen, ignoring navigation request");
            return;
        }

        // Add current screen to history before navigating
        if (addToHistory && enableBackButtonHistory)
        {
            AddToHistory(currentScreen);
        }

        if (showDebugLogs)
            Debug.Log($"Navigating from {currentScreen.name} to {nextScreen.name}");

        StartCoroutine(TransitionScreens(currentScreen, nextScreen));
    }

    // Back navigation - can be called from UI back buttons
    public void NavigateBack()
    {
        HandleBackNavigation();
    }

    private void HandleBackNavigation()
    {
        if (isTransitioning) return;

        // If no navigation has started yet, ignore back button
        if (!hasStartedNavigation && startWithAllInvisible)
        {
            if (showDebugLogs)
                Debug.Log("Back pressed but no navigation started yet, ignoring");
            return;
        }

        if (screenHistory.Count > 0)
        {
            GameObject previousScreen = screenHistory.Pop();
            
            // Validate that the previous screen still exists
            if (previousScreen != null)
            {
                if (showDebugLogs)
                    Debug.Log($"Navigating back to: {previousScreen.name}");
                
                // Navigate without adding to history (to avoid circular references)
                NavigateTo(previousScreen, false);
            }
            else
            {
                // If screen was destroyed, try the next one in history
                HandleBackNavigation();
            }
        }
        else
        {
            // No history, go to default screen or handle as needed
            HandleBackToDefault();
        }
    }

    private void HandleBackToDefault()
    {
        if (defaultScreen != null && currentScreen != defaultScreen)
        {
            if (showDebugLogs)
                Debug.Log("No history available, returning to default screen");
            
            NavigateTo(defaultScreen, false);
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("Back pressed but no default screen set or already on default");
            
            // Could quit application, show exit dialog, etc.
            OnBackToDefault?.Invoke();
        }
    }

    private void AddToHistory(GameObject screen)
    {
        // Avoid adding the same screen multiple times in a row
        if (screenHistory.Count > 0 && screenHistory.Peek() == screen)
            return;

        screenHistory.Push(screen);

        // Limit history size
        if (screenHistory.Count > maxHistorySize)
        {
            var tempStack = new Stack<GameObject>();
            for (int i = 0; i < maxHistorySize; i++)
            {
                if (screenHistory.Count > 0)
                    tempStack.Push(screenHistory.Pop());
            }
            screenHistory.Clear();
            while (tempStack.Count > 0)
            {
                screenHistory.Push(tempStack.Pop());
            }
        }

        if (showDebugLogs)
            Debug.Log($"Added to history: {screen.name} (History size: {screenHistory.Count})");
    }

    private IEnumerator TransitionScreens(GameObject fromScreen, GameObject toScreen)
    {
        isTransitioning = true;

        // Ensure target screen is active
        if (!toScreen.activeSelf)
        {
            toScreen.SetActive(true);
        }

        CanvasGroup fromCG = GetOrAddCanvasGroup(fromScreen);
        CanvasGroup toCG = GetOrAddCanvasGroup(toScreen);

        // Prepare transition
        fromCG.alpha = 1f;
        fromCG.interactable = false;
        fromCG.blocksRaycasts = false;
        
        toCG.alpha = 0f;
        toCG.interactable = false;
        toCG.blocksRaycasts = true; // Enable raycasts for the new screen

        float elapsedTime = 0f;
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / transitionDuration;
            float curveValue = transitionCurve.Evaluate(normalizedTime);

            fromCG.alpha = Mathf.Lerp(1f, 0f, curveValue);
            toCG.alpha = Mathf.Lerp(0f, 1f, curveValue);

            yield return null;
        }

        // Finalize transition
        fromCG.alpha = 0f;
        toCG.alpha = 1f;
        toCG.interactable = true;

        // Handle screen deactivation
        if (!preloadScreens)
        {
            fromScreen.SetActive(false);
        }
        else
        {
            fromCG.blocksRaycasts = false;
        }

        GameObject previousScreen = currentScreen;
        currentScreen = toScreen;
        isTransitioning = false;

        // Trigger event
        OnScreenChanged?.Invoke(previousScreen, currentScreen);

        if (showDebugLogs)
            Debug.Log($"Transition completed. Current screen: {currentScreen.name}");
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject screen)
    {
        CanvasGroup canvasGroup = screen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = screen.AddComponent<CanvasGroup>();
        }
        return canvasGroup;
    }

    private void ActivateScreen(GameObject screen)
    {
        screen.SetActive(true);
        CanvasGroup cg = GetOrAddCanvasGroup(screen);
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    private GameObject GetActiveScreen()
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                // Check if it's actually visible (alpha > 0)
                CanvasGroup cg = child.gameObject.GetComponent<CanvasGroup>();
                if (cg == null || cg.alpha > 0f)
                    return child.gameObject;
            }
        }
        return null;
    }

    // Public utility methods
    public string GetCurrentScreenName()
    {
        return currentScreen != null ? currentScreen.name : "None";
    }

    public void SetScreenDirectly(GameObject screen)
    {
        if (isTransitioning) return;

        if (currentScreen != null)
        {
            if (!preloadScreens)
                currentScreen.SetActive(false);
            else
            {
                CanvasGroup cg = GetOrAddCanvasGroup(currentScreen);
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        ActivateScreen(screen);
        currentScreen = screen;
        hasStartedNavigation = true; // Mark that navigation has started

        if (showDebugLogs)
            Debug.Log($"Screen set directly to: {screen.name}");
    }

    public void ClearHistory()
    {
        screenHistory.Clear();
        if (showDebugLogs)
            Debug.Log("Screen history cleared");
    }

    public bool CanGoBack()
    {
        return screenHistory.Count > 0 || (defaultScreen != null && currentScreen != defaultScreen);
    }

    public int GetHistoryCount()
    {
        return screenHistory.Count;
    }

    // NEW: Method to show home screen manually
    public void ShowHomeScreen()
    {
        if (homeScreen != null)
        {
            NavigateTo(homeScreen, false); // Don't add to history since it's home
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning("Home screen not assigned!");
        }
    }

    // NEW: Method to start navigation manually (alternative to first button click)
    public void ShowFirstScreen()
    {
        GameObject screenToShow = homeScreen != null ? homeScreen : defaultScreen;
        if (screenToShow != null && !hasStartedNavigation)
        {
            NavigateTo(screenToShow);
        }
    }

    // Method to handle application quit on back press (optional)
    public void QuitApplication()
    {
        if (showDebugLogs)
            Debug.Log("Quitting application");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}