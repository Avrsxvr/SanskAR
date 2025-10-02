using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FakeChatbotSystem : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField messageInput;
    public Button sendButton;
    public Transform chatParent;
    public GameObject userMessagePrefab;
    public GameObject botMessagePrefab;
    public ScrollRect chatScrollRect;

    [Header("Bot Configuration")]
    public float botResponseDelay = 1.5f;
    public float typewriterSpeed = 0.05f; // Speed of typewriter effect
    
    [Header("Bot Response Text")]
    [TextArea(3, 10)]
    public string botResponseText = "Hello this is SanskARI BOT to help you with your questions";

    [Header("Message Positioning")]
    public float messageSpacing = 80f; // Spacing between messages

    private List<GameObject> messageObjects = new List<GameObject>();

    void Start()
    {
        // Set up button click listener
        sendButton.onClick.AddListener(SendMessage);
        
        // Set up input field to send message on Enter
        messageInput.onEndEdit.AddListener(OnInputEndEdit);
        
        // Clean setup - remove any layout components
        CleanSetup();
    }

    void CleanSetup()
    {
        // Remove any existing layout components
        Component[] layoutComponents = chatParent.GetComponents<Component>();
        foreach (Component comp in layoutComponents)
        {
            if (comp is LayoutGroup || comp is ContentSizeFitter)
            {
                DestroyImmediate(comp);
            }
        }
        
        // Set up scroll rect
        if (chatScrollRect != null)
        {
            chatScrollRect.vertical = true;
            chatScrollRect.horizontal = false;
            chatScrollRect.movementType = ScrollRect.MovementType.Clamped;
        }
    }

    void OnInputEndEdit(string input)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessage();
        }
    }

    public void SendMessage()
    {
        string userMessage = messageInput.text.Trim();
        
        if (string.IsNullOrEmpty(userMessage))
            return;

        // Clear input field
        messageInput.text = "";
        messageInput.ActivateInputField();

        // Add user message
        AddUserMessage(userMessage);

        // Generate bot response with delay
        StartCoroutine(BotResponse());
    }

    void AddUserMessage(string message)
    {
        // Move all existing messages up
        MoveMessagesUp();

        // Create user message - uses prefab position exactly as set
        GameObject userMsgObj = Instantiate(userMessagePrefab, chatParent);
        
        // Set message text
        TMP_Text userText = userMsgObj.GetComponentInChildren<TMP_Text>();
        if (userText != null)
            userText.text = message;

        // Add to list
        messageObjects.Add(userMsgObj);

        // Update content and scroll
        UpdateContentHeight();
        StartCoroutine(ScrollToBottom());
    }

    IEnumerator BotResponse()
    {
        // Wait for bot response delay
        yield return new WaitForSeconds(botResponseDelay);

        // Move all existing messages up
        MoveMessagesUp();

        // Create bot message - uses prefab position exactly as set
        GameObject botMsgObj = Instantiate(botMessagePrefab, chatParent);
        
        // Get text component for typewriter effect
        TMP_Text botText = botMsgObj.GetComponentInChildren<TMP_Text>();
        if (botText != null)
        {
            // Start with empty text
            botText.text = "";
            
            // Start typewriter effect
            StartCoroutine(TypewriterEffect(botText, botResponseText));
        }

        // Add to list
        messageObjects.Add(botMsgObj);

        // Update content and scroll
        UpdateContentHeight();
        StartCoroutine(ScrollToBottom());
    }

    IEnumerator TypewriterEffect(TMP_Text textComponent, string fullText)
    {
        textComponent.text = "";
        
        for (int i = 0; i <= fullText.Length; i++)
        {
            textComponent.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(typewriterSpeed);
        }
    }

    void MoveMessagesUp()
    {
        // Move all existing messages up by spacing amount
        foreach (GameObject msgObj in messageObjects)
        {
            if (msgObj != null)
            {
                RectTransform msgRect = msgObj.GetComponent<RectTransform>();
                if (msgRect != null)
                {
                    Vector2 currentPos = msgRect.anchoredPosition;
                    msgRect.anchoredPosition = new Vector2(currentPos.x, currentPos.y + messageSpacing);
                }
            }
        }
    }

    void UpdateContentHeight()
    {
        RectTransform contentRect = chatParent as RectTransform;
        if (contentRect != null)
        {
            // Calculate total height based on number of messages
            float totalHeight = 100f + (messageObjects.Count * messageSpacing);
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);
        }
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        
        if (chatScrollRect != null)
        {
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // Method to clear chat
    public void ClearChat()
    {
        foreach (GameObject msgObj in messageObjects)
        {
            if (msgObj != null)
            {
                DestroyImmediate(msgObj);
            }
        }
        messageObjects.Clear();
        UpdateContentHeight();
    }
}