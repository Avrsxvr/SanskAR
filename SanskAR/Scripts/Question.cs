using UnityEngine;

[System.Serializable]
public class Question
{
    [TextArea(3, 5)]
    public string questionText;
    public string[] answers = new string[4];
    public int correctAnswerIndex;
    public int points = 10;
}

[CreateAssetMenu(fileName = "QuizData", menuName = "Quiz/Quiz Data")]
public class QuizData : ScriptableObject
{
    public Question[] questions;
    public string quizTitle = "Taj Mahal Quiz";
    public float timePerQuestion = 30f;

    private void OnEnable()
    {
        // Only populate if questions array is empty or null
        if (questions == null || questions.Length == 0)
        {
            LoadDefaultQuestions();
        }
    }

    public void LoadDefaultQuestions()
    {
        questions = new Question[4];

        // Question 1
        questions[0] = new Question
        {
            questionText = "The Taj Mahal complex is perfectly symmetrical except for one element. Which element breaks the symmetry?",
            answers = new string[] 
            { 
                "The minarets", 
                "The cenotaphs inside the mausoleum", 
                "The reflecting pool", 
                "The mosque" 
            },
            correctAnswerIndex = 1,
            points = 10
        };

        // Question 2
        questions[1] = new Question
        {
            questionText = "Which precious stone was NOT originally inlaid in the Taj Mahal's marble?",
            answers = new string[] 
            { 
                "Lapis Lazuli", 
                "Turquoise", 
                "Diamond", 
                "Carnelian" 
            },
            correctAnswerIndex = 2,
            points = 10
        };

        // Question 3
        questions[2] = new Question
        {
            questionText = "Which famous British official is often criticized for removing valuable stones and carpets from the Taj Mahal during colonial rule?",
            answers = new string[] 
            { 
                "Lord Curzon", 
                "Warren Hastings", 
                "Lord Dalhousie", 
                "Sir William Bentinck" 
            },
            correctAnswerIndex = 3,
            points = 10
        };

        // Question 4
        questions[3] = new Question
        {
            questionText = "The minarets of the Taj Mahal were built with a slight outward tilt. What was the primary purpose of this design?",
            answers = new string[] 
            { 
                "To prevent shadow on the dome", 
                "To make them look taller from afar", 
                "To protect the mausoleum if they collapsed during an earthquake", 
                "To align with sunrise and sunset" 
            },
            correctAnswerIndex = 2,
            points = 10
        };
    }
}