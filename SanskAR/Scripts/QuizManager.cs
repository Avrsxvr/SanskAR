using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[System.Serializable]
public class QuizCustomization
{
    [Header("Answer Button Colors")]
    public Color defaultButtonColor = Color.white;
    public Color correctAnswerColor = Color.green;
    public Color wrongAnswerColor = Color.red;
    public Color timeUpCorrectColor = Color.yellow;
    public Color hoverColor = Color.cyan;
    
    [Header("Timer Colors")]
    public Color normalTimerColor = Color.white;
    public Color urgentTimerColor = Color.red;
    [Range(1f, 30f)]
    public float urgentTimeThreshold = 10f;
    
    [Header("Text Display Options")]
    public bool showScoreLabel = true;
    public string scorePrefix = "Score: ";
    public string scoreSuffix = "";
    public bool showTimerLabel = true;
    public string timerPrefix = "Time: ";
    public string timerSuffix = "s";
    
    [Header("Question Display")]
    public bool showQuestionNumber = true;
    public string questionNumberFormat = "Question {0}: ";
    
    [Header("Results Display")]
    public bool showFinalScoreLabel = true;
    public string finalScorePrefix = "Final Score: ";
    public bool showPercentage = true;
    public bool showGrade = true;
    public string[] gradeMessages = { "Try again!", "Pass! C", "Good! B", "Great! A", "Excellent! A+" };
    public float[] gradeThresholds = { 0f, 60f, 70f, 80f, 90f };
    
    [Header("Animation & Effects")]
    public bool enableButtonHoverEffects = true;
    public bool enableColorTransitions = true;
    public float colorTransitionDuration = 0.3f;
    
    [Header("Sound Feedback")]
    public AudioClip correctAnswerSound;
    public AudioClip wrongAnswerSound;
    public AudioClip timeUpSound;
    public AudioClip buttonHoverSound;
}

public class QuizManager : MonoBehaviour
{
    [Header("Quiz Data")]
    public QuizData quizData;
    
    [Header("UI References")]
    public TextMeshProUGUI questionText;
    public Button[] answerButtons;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public GameObject questionPanel;
    public GameObject resultsPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI questionsCorrectText;
    
    [Header("Feedback")]
    public GameObject correctFeedback;
    public GameObject incorrectFeedback;
    public float feedbackDuration = 1f;
    
    [Header("Customization")]
    public QuizCustomization customization;
    
    [Header("Audio")]
    public AudioSource audioSource;
    
    // Private variables
    private int currentQuestionIndex = 0;
    private int score = 0;
    private int correctAnswers = 0;
    private float timeRemaining;
    private bool isAnswering = true;
    private Color[] originalButtonColors;
    
    void Start()
    {
        if (quizData == null)
        {
            Debug.LogError("Quiz Data is not assigned!");
            return;
        }
        
        InitializeCustomization();
        SetupAnswerButtons();
        StartQuiz();
    }
    
    void Update()
    {
        if (isAnswering && timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            UpdateTimerDisplay();
            
            if (timeRemaining <= 0)
            {
                TimeUp();
            }
        }
    }
    
    void InitializeCustomization()
    {
        // Store original button colors
        originalButtonColors = new Color[answerButtons.Length];
        for (int i = 0; i < answerButtons.Length; i++)
        {
            originalButtonColors[i] = answerButtons[i].GetComponent<Image>().color;
            
            // Set default color
            answerButtons[i].GetComponent<Image>().color = customization.defaultButtonColor;
            
            // Add hover effects if enabled
            if (customization.enableButtonHoverEffects)
            {
                AddHoverEffects(answerButtons[i], i);
            }
        }
    }
    
    void AddHoverEffects(Button button, int index)
    {
        var trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        // Hover enter
        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => OnButtonHover(index, true));
        trigger.triggers.Add(enterEntry);
        
        // Hover exit
        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => OnButtonHover(index, false));
        trigger.triggers.Add(exitEntry);
    }
    
    void OnButtonHover(int buttonIndex, bool isHovering)
    {
        if (!isAnswering) return;
        
        if (customization.buttonHoverSound && audioSource && isHovering)
        {
            audioSource.PlayOneShot(customization.buttonHoverSound);
        }
        
        Color targetColor = isHovering ? customization.hoverColor : customization.defaultButtonColor;
        
        if (customization.enableColorTransitions)
        {
            StartCoroutine(TransitionButtonColor(buttonIndex, targetColor));
        }
        else
        {
            answerButtons[buttonIndex].GetComponent<Image>().color = targetColor;
        }
    }
    
    IEnumerator TransitionButtonColor(int buttonIndex, Color targetColor)
    {
        Image buttonImage = answerButtons[buttonIndex].GetComponent<Image>();
        Color startColor = buttonImage.color;
        float elapsed = 0f;
        
        while (elapsed < customization.colorTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / customization.colorTransitionDuration;
            buttonImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }
        
        buttonImage.color = targetColor;
    }
    
    void SetupAnswerButtons()
    {
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int buttonIndex = i; // Capture for closure
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(buttonIndex));
        }
    }
    
    void StartQuiz()
    {
        currentQuestionIndex = 0;
        score = 0;
        correctAnswers = 0;
        questionPanel.SetActive(true);
        resultsPanel.SetActive(false);
        
        DisplayCurrentQuestion();
        UpdateScoreDisplay();
    }
    
    void DisplayCurrentQuestion()
    {
        if (currentQuestionIndex >= quizData.questions.Length)
        {
            EndQuiz();
            return;
        }
        
        Question currentQuestion = quizData.questions[currentQuestionIndex];
        
        // Format question text with customization
        string questionDisplayText = "";
        if (customization.showQuestionNumber)
        {
            questionDisplayText = string.Format(customization.questionNumberFormat, currentQuestionIndex + 1);
        }
        questionDisplayText += currentQuestion.questionText;
        questionText.text = questionDisplayText;
        
        // Set answer button texts and reset colors
        for (int i = 0; i < answerButtons.Length && i < currentQuestion.answers.Length; i++)
        {
            answerButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = currentQuestion.answers[i];
            answerButtons[i].interactable = true;
            answerButtons[i].GetComponent<Image>().color = customization.defaultButtonColor;
        }
        
        // Hide unused buttons if there are fewer than 4 answers
        for (int i = currentQuestion.answers.Length; i < answerButtons.Length; i++)
        {
            answerButtons[i].gameObject.SetActive(false);
        }
        
        // Reset timer
        timeRemaining = quizData.timePerQuestion;
        isAnswering = true;
    }
    
    public void OnAnswerSelected(int selectedIndex)
    {
        if (!isAnswering) return;
        
        isAnswering = false;
        Question currentQuestion = quizData.questions[currentQuestionIndex];
        
        // Disable all buttons
        foreach (Button button in answerButtons)
        {
            button.interactable = false;
        }
        
        // Check if answer is correct
        if (selectedIndex == currentQuestion.correctAnswerIndex)
        {
            // Correct answer
            score += currentQuestion.points;
            correctAnswers++;
            
            if (customization.enableColorTransitions)
            {
                StartCoroutine(TransitionButtonColor(selectedIndex, customization.correctAnswerColor));
            }
            else
            {
                answerButtons[selectedIndex].GetComponent<Image>().color = customization.correctAnswerColor;
            }
            
            PlaySound(customization.correctAnswerSound);
            ShowFeedback(true);
        }
        else
        {
            // Wrong answer
            if (customization.enableColorTransitions)
            {
                StartCoroutine(TransitionButtonColor(selectedIndex, customization.wrongAnswerColor));
                StartCoroutine(TransitionButtonColor(currentQuestion.correctAnswerIndex, customization.correctAnswerColor));
            }
            else
            {
                answerButtons[selectedIndex].GetComponent<Image>().color = customization.wrongAnswerColor;
                answerButtons[currentQuestion.correctAnswerIndex].GetComponent<Image>().color = customization.correctAnswerColor;
            }
            
            PlaySound(customization.wrongAnswerSound);
            ShowFeedback(false);
        }
        
        UpdateScoreDisplay();
        
        // Move to next question after delay
        StartCoroutine(NextQuestionAfterDelay());
    }
    
    void TimeUp()
    {
        if (!isAnswering) return;
        
        isAnswering = false;
        
        // Show correct answer with time up color
        Question currentQuestion = quizData.questions[currentQuestionIndex];
        
        if (customization.enableColorTransitions)
        {
            StartCoroutine(TransitionButtonColor(currentQuestion.correctAnswerIndex, customization.timeUpCorrectColor));
        }
        else
        {
            answerButtons[currentQuestion.correctAnswerIndex].GetComponent<Image>().color = customization.timeUpCorrectColor;
        }
        
        // Disable all buttons
        foreach (Button button in answerButtons)
        {
            button.interactable = false;
        }
        
        PlaySound(customization.timeUpSound);
        ShowFeedback(false);
        StartCoroutine(NextQuestionAfterDelay());
    }
    
    void PlaySound(AudioClip clip)
    {
        if (audioSource && clip)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    void ShowFeedback(bool correct)
    {
        if (correct && correctFeedback != null)
        {
            correctFeedback.SetActive(true);
            StartCoroutine(HideFeedbackAfterDelay(correctFeedback));
        }
        else if (!correct && incorrectFeedback != null)
        {
            incorrectFeedback.SetActive(true);
            StartCoroutine(HideFeedbackAfterDelay(incorrectFeedback));
        }
    }
    
    IEnumerator HideFeedbackAfterDelay(GameObject feedback)
    {
        yield return new WaitForSeconds(feedbackDuration);
        feedback.SetActive(false);
    }
    
    IEnumerator NextQuestionAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        
        currentQuestionIndex++;
        
        // Reset button colors
        foreach (Button button in answerButtons)
        {
            button.GetComponent<Image>().color = customization.defaultButtonColor;
            button.gameObject.SetActive(true);
        }
        
        DisplayCurrentQuestion();
    }
    
    void UpdateScoreDisplay()
    {
        string scoreDisplay = "";
        
        if (customization.showScoreLabel)
        {
            scoreDisplay = customization.scorePrefix + score.ToString() + customization.scoreSuffix;
        }
        else
        {
            scoreDisplay = score.ToString();
        }
        
        scoreText.text = scoreDisplay;
    }
    
    void UpdateTimerDisplay()
    {
        string timerDisplay = "";
        int timeValue = Mathf.CeilToInt(timeRemaining);
        
        if (customization.showTimerLabel)
        {
            timerDisplay = customization.timerPrefix + timeValue.ToString() + customization.timerSuffix;
        }
        else
        {
            timerDisplay = timeValue.ToString();
        }
        
        timerText.text = timerDisplay;
        
        // Change color when time is running low
        if (timeRemaining < customization.urgentTimeThreshold)
        {
            timerText.color = customization.urgentTimerColor;
        }
        else
        {
            timerText.color = customization.normalTimerColor;
        }
    }
    
    void EndQuiz()
    {
        questionPanel.SetActive(false);
        resultsPanel.SetActive(true);
        
        // Final Score Display
        string finalScoreDisplay = "";
        if (customization.showFinalScoreLabel)
        {
            finalScoreDisplay = customization.finalScorePrefix + score.ToString();
        }
        else
        {
            finalScoreDisplay = score.ToString();
        }
        finalScoreText.text = finalScoreDisplay;
        
        // Questions Correct Display
        string resultsDisplay = $"Questions Correct: {correctAnswers}/{quizData.questions.Length}";
        
        // Calculate percentage
        float percentage = (float)correctAnswers / quizData.questions.Length * 100f;
        
        if (customization.showPercentage)
        {
            resultsDisplay += $"\nPercentage: {percentage:F1}%";
        }
        
        // Add grade message
        if (customization.showGrade)
        {
            string gradeMessage = GetGradeMessage(percentage);
            resultsDisplay += $"\n{gradeMessage}";
        }
        
        questionsCorrectText.text = resultsDisplay;
    }
    
    string GetGradeMessage(float percentage)
    {
        for (int i = customization.gradeThresholds.Length - 1; i >= 0; i--)
        {
            if (percentage >= customization.gradeThresholds[i])
            {
                if (i < customization.gradeMessages.Length)
                {
                    return customization.gradeMessages[i];
                }
            }
        }
        return customization.gradeMessages[0]; // Fallback to first message
    }
    
    public void RestartQuiz()
    {
        StartQuiz();
    }
    
    public void QuitQuiz()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    // Public methods for runtime customization
    public void SetCorrectAnswerColor(Color color)
    {
        customization.correctAnswerColor = color;
    }
    
    public void SetWrongAnswerColor(Color color)
    {
        customization.wrongAnswerColor = color;
    }
    
    public void SetScoreDisplayFormat(bool showLabel, string prefix = "Score: ", string suffix = "")
    {
        customization.showScoreLabel = showLabel;
        customization.scorePrefix = prefix;
        customization.scoreSuffix = suffix;
        UpdateScoreDisplay();
    }
    
    public void SetTimerDisplayFormat(bool showLabel, string prefix = "Time: ", string suffix = "s")
    {
        customization.showTimerLabel = showLabel;
        customization.timerPrefix = prefix;
        customization.timerSuffix = suffix;
    }
    
    public void SetTimerColors(Color normalColor, Color urgentColor, float urgentThreshold = 10f)
    {
        customization.normalTimerColor = normalColor;
        customization.urgentTimerColor = urgentColor;
        customization.urgentTimeThreshold = urgentThreshold;
    }
}