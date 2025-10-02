using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class BackButtonHandler : MonoBehaviour
{
    [Header("Canvas/Screen References")]
    [SerializeField] private GameObject[] allScreens;
    
    [Header("Back Button References")]
    [SerializeField] private Button[] backButtons;
    
    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Back Navigation Settings")]
    [SerializeField] private GameObject startingScreen;
    [SerializeField] private bool enableDeviceBackButton = true;
    [SerializeField] private bool enableBackButtonHistory = true;
    [SerializeField] private int maxHistorySize = 10;
    [SerializeField] private bool preloadScreens = true;
    [SerializeField] private bool doNotExitApp = true; // New option to prevent app exit
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private GameObject currentScreen;
    private Stack<GameObject> screenHistory = new Stack<GameObject>();
    private bool isTransitioning = false;
    
    // Events
    public System.Action<GameObject, GameObject> OnScreenChanged;
    public System.Action OnBackToStartingScreen;
    
    void Start()
    {
        InitializeScreens();
        SetupBackButtons();
        
        // Set starting screen
        if (startingScreen != null)
        {
            SetScreenDirectly(startingScreen);
        }
        else
        {
            // Find the first active screen
            currentScreen = GetActiveScreen();
            if (currentScreen != null && showDebugLogs)
            {
                Debug.Log($"Starting screen set to first active: {currentScreen.name}");
            }
        }
    }
    
    void Update()
    {
        // Handle device back button - supports both old and new Input System
        if (enableDeviceBackButton && IsBackButtonPressed())
        {
            HandleBackNavigation();
        }
    }
    
    private bool IsBackButtonPressed()
    {
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
        
        return backPressed;
    }
    
    private void InitializeScreens()
    {
        if (preloadScreens)
        {
            PreloadAllScreens();
        }
        else
        {
            // Make sure all screens are initially disabled except starting screen
            foreach (GameObject screen in allScreens)
            {
                if (screen != null && screen != startingScreen)
                {
                    screen.SetActive(false);
                }
            }
        }
    }
    
    private void PreloadAllScreens()
    {
        foreach (GameObject screen in allScreens)
        {
            if (screen != null)
            {
                screen.SetActive(true);
                CanvasGroup cg = GetOrAddCanvasGroup(screen);
                
                if (screen == startingScreen)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
                else
                {
                    cg.alpha = 0f;
                    cg.interactable = false;
                    cg.blocksRaycasts = false;
                }
            }
        }
    }
    
    private void SetupBackButtons()
    {
        // Assign back button functionality
        foreach (Button backButton in backButtons)
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(HandleBackNavigation);
            }
        }
    }
    
    /// <summary>
    /// Navigate to a specific screen
    /// </summary>
    /// <param name="targetScreen">Screen to navigate to</param>
    /// <param name="addToHistory">Whether to add current screen to history</param>
    public void NavigateToScreen(GameObject targetScreen, bool addToHistory = true)
    {
        if (isTransitioning || targetScreen == null)
        {
            if (showDebugLogs && isTransitioning)
                Debug.Log("Transition already in progress, ignoring request");
            return;
        }

        if (currentScreen == targetScreen)
        {
            if (showDebugLogs)
                Debug.Log("Already on target screen, ignoring navigation request");
            return;
        }

        // Add current screen to history before navigating
        if (addToHistory && enableBackButtonHistory && currentScreen != null)
        {
            AddToHistory(currentScreen);
        }

        if (showDebugLogs)
        {
            string from = currentScreen != null ? currentScreen.name : "None";
            Debug.Log($"Navigating from {from} to {targetScreen.name}");
        }

        if (currentScreen == null)
        {
            SetScreenDirectly(targetScreen);
        }
        else
        {
            StartCoroutine(TransitionScreens(currentScreen, targetScreen));
        }
    }
    
    /// <summary>
    /// Navigate to screen by name
    /// </summary>
    /// <param name="screenName">Name of the screen to navigate to</param>
    /// <param name="addToHistory">Whether to add current screen to history</param>
    public void NavigateToScreenByName(string screenName, bool addToHistory = true)
    {
        GameObject targetScreen = System.Array.Find(allScreens, screen => screen != null && screen.name == screenName);
        
        if (targetScreen != null)
        {
            NavigateToScreen(targetScreen, addToHistory);
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning($"Screen with name '{screenName}' not found!");
        }
    }
    
    /// <summary>
    /// Handle back navigation
    /// </summary>
    public void HandleBackNavigation()
    {
        if (isTransitioning) return;

        if (screenHistory.Count > 0)
        {
            GameObject previousScreen = screenHistory.Pop();
            
            // Validate that the previous screen still exists
            if (previousScreen != null)
            {
                if (showDebugLogs)
                    Debug.Log($"Going back to: {previousScreen.name}");
                
                // Navigate without adding to history (to avoid circular references)
                NavigateToScreen(previousScreen, false);
            }
            else
            {
                // If screen was destroyed, try the next one in history
                HandleBackNavigation();
            }
        }
        else
        {
            // No history available
            HandleBackToStartingScreen();
        }
    }
    
    private void HandleBackToStartingScreen()
    {
        if (startingScreen != null && currentScreen != startingScreen)
        {
            if (showDebugLogs)
                Debug.Log("No history available, returning to starting screen");
            
            NavigateToScreen(startingScreen, false);
            OnBackToStartingScreen?.Invoke();
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("Back pressed but already on starting screen or no starting screen set");
            
            // Don't exit app if doNotExitApp is true
            if (!doNotExitApp)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                    Application.Quit();
                #elif UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }
            else
            {
                OnBackToStartingScreen?.Invoke();
            }
        }
    }
    
    private void AddToHistory(GameObject screen)
    {
        if (screen == null) return;
        
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
        fromCG.interactable = false;
        fromCG.blocksRaycasts = false;
        
        toCG.alpha = 0f;
        toCG.interactable = false;
        toCG.blocksRaycasts = true;

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
    
    private void SetScreenDirectly(GameObject screen)
    {
        if (screen == null) return;
        
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

        if (!screen.activeSelf)
        {
            screen.SetActive(true);
        }
        
        CanvasGroup screenCG = GetOrAddCanvasGroup(screen);
        screenCG.alpha = 1f;
        screenCG.interactable = true;
        screenCG.blocksRaycasts = true;

        currentScreen = screen;

        if (showDebugLogs)
            Debug.Log($"Screen set directly to: {screen.name}");
    }
    
    private GameObject GetActiveScreen()
    {
        foreach (GameObject screen in allScreens)
        {
            if (screen != null && screen.activeSelf)
            {
                CanvasGroup cg = screen.GetComponent<CanvasGroup>();
                if (cg == null || cg.alpha > 0.5f)
                {
                    return screen;
                }
            }
        }
        return null;
    }
    
    // Public utility methods
    public string GetCurrentScreenName()
    {
        return currentScreen != null ? currentScreen.name : "None";
    }
    
    public GameObject GetCurrentScreen()
    {
        return currentScreen;
    }
    
    public void ClearHistory()
    {
        screenHistory.Clear();
        if (showDebugLogs)
            Debug.Log("Screen history cleared");
    }
    
    public bool CanGoBack()
    {
        return screenHistory.Count > 0 || (startingScreen != null && currentScreen != startingScreen);
    }
    
    public int GetHistoryCount()
    {
        return screenHistory.Count;
    }
    
    // Methods for UI buttons to call
    public void OnMenuButtonClicked(GameObject menuScreen)
    {
        NavigateToScreen(menuScreen);
    }
    
    public void OnSettingsButtonClicked(GameObject settingsScreen)
    {
        NavigateToScreen(settingsScreen);
    }
    
    public void OnGameplayButtonClicked(GameObject gameplayScreen)
    {
        NavigateToScreen(gameplayScreen);
    }
    
    // Method to go back - can be called from UI back buttons
    public void GoBack()
    {
        HandleBackNavigation();
    }
}