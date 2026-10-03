using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game State")]
    public int lives = 3;
    public int score;
    public int LevelNumber;
    public bool inGame;

    [Header("UI")]
    public Text scoreText;
    public Text HighscoreText;
    public Text yourHighscoreText;
    public Slider sliderHighscore;

    public GameObject level1Panel;
    public GameObject GameOverPanel;
    public GameObject WinnerPanel;
    public GameObject PauseMenu;

    [Header("Performance UI")]
    public GameObject PerformancePanel;
    public Text performanceText;
    public Button decreaseHelpButton;

    [Header("Timed Rehab Session")]
    public bool useTimedSession = true;
    public float sessionDuration = 60f;
    public Text sessionTimerText;

    [Header("Assistance Intensity UI")]
    public Slider pauseAssistanceSlider;
    public Text pauseAssistanceLabel;

    public Slider endAssistanceSlider;
    public Text endAssistanceLabel;

    [Range(0, 3)]
    public int assistanceIntensityLevel = 2;

    [Tooltip("Only text for the summary for now. Later the slider can change this automatically.")]
    public string currentAssistanceIntensityName = "Medium";

    private float sessionTimeRemaining;
    private bool sessionCompleted = false;

    [Header("Performance Criteria")]
    [Range(0f, 1f)] public float goodMeanErrorThreshold = 0.08f;
    [Range(0f, 1f)] public float goodTransparentTimeThreshold = 0.70f;

    [Header("Level Data")]
    public int numberOfBricks;
    public int number_unbreakable = 0;

    private int Total_numberBricks;
    private int totalPoints;

    [Header("References")]
    public BrickScript brick;
    public BallScript ball;
    public PaddleScript paddle;
    public BallReturnCheckpoint predictor;
    public BrickScript[] bricks;

    [Header("Lives")]
    public Image[] hearts;
    public Sprite fullHeart;
    public Sprite emptyHeart;

    [Header("Feedback")]
    public feedback fb;
    public int number_collision = 0;
    public int vero_numero_collisioni;
    private int flag;

    [Header("Countdown")]
    public Text countDownText;

    [Header("Audio")]
    public AudioSource[] audioSource;

    [Header("Power Ups")]
    public GameObject MultiBall;
    public GameObject Life;
    public GameObject BiggerPaddle;
    public GameObject PausePower;
    public GameObject Balltwo;

    private float InstantiationTimer = 7f;

    // Performance tracking during one session
    private int performanceSamples = 0;
    private int transparentSamples = 0;
    private int gentleAssistSamples = 0;
    private int strongAssistSamples = 0;

    private float errorPercentSum = 0f;
    private float maxErrorPercent = 0f;

    void Start()
    {
        brick = FindObjectOfType<BrickScript>();
        ball = FindObjectOfType<BallScript>();
        paddle = FindObjectOfType<PaddleScript>();
        predictor = FindObjectOfType<BallReturnCheckpoint>();
        bricks = FindObjectsOfType<BrickScript>();

        if (scoreText != null)
            scoreText.text = score.ToString();

        CountBricksAndPoints();

        audioSource = GetComponents<AudioSource>();

        if (Balltwo != null)
            Balltwo.SetActive(false);

        HideEndPanels();
        

        if (decreaseHelpButton != null)
            decreaseHelpButton.gameObject.SetActive(false);

        sessionTimeRemaining = sessionDuration;

        if (sessionTimerText != null)
            sessionTimerText.text = Mathf.CeilToInt(sessionTimeRemaining).ToString() + " s";
    }

    void Update()
    {
        if (paddle != null)
            vero_numero_collisioni = paddle.collision_number;

        if (Input.GetKeyDown(KeyCode.Space))
            Pause();

        UpdateHearts();

        if (inGame)
        {
            SuperPowers();
            UpdatePerformanceTracking();
            UpdateSessionTimer();
        }

        if (paddle != null && fb != null)
        {
            if (paddle.beta >= 10f)
                fb.Compensation();
        }
    }

    void HideEndPanels()
    {
        if (GameOverPanel != null)
            GameOverPanel.SetActive(false);

        if (WinnerPanel != null)
            WinnerPanel.SetActive(false);

        if (PerformancePanel != null)
            PerformancePanel.SetActive(false);
    }

    void SetupAssistanceSliders()
{
    if (pauseAssistanceSlider != null)
    {
        pauseAssistanceSlider.minValue = 0;
        pauseAssistanceSlider.maxValue = 3;
        pauseAssistanceSlider.wholeNumbers = true;
        pauseAssistanceSlider.SetValueWithoutNotify(assistanceIntensityLevel);
    }

    if (endAssistanceSlider != null)
    {
        endAssistanceSlider.minValue = 0;
        endAssistanceSlider.maxValue = 3;
        endAssistanceSlider.wholeNumbers = true;
        endAssistanceSlider.SetValueWithoutNotify(assistanceIntensityLevel);
    }

    ApplyAssistanceIntensity(assistanceIntensityLevel);
}

public void SetAssistanceIntensityFromSlider(float value)
{
    assistanceIntensityLevel = Mathf.RoundToInt(value);
    ApplyAssistanceIntensity(assistanceIntensityLevel);
}

public void ApplyAssistanceIntensity(int level)
{
    assistanceIntensityLevel = Mathf.Clamp(level, 0, 3);

    if (predictor == null)
        predictor = FindObjectOfType<BallReturnCheckpoint>();

    if (predictor == null)
        return;

    if (assistanceIntensityLevel == 0)
    {
        currentAssistanceIntensityName = "Off / Transparent";

        predictor.optionalPhysicalAssistance = false;

        // Full transparent region: no physical assistance.
        predictor.transparentThreshold = 1.0f;
        predictor.mediumThreshold = 1.0f;
    }
    else if (assistanceIntensityLevel == 1)
    {
        currentAssistanceIntensityName = "Low";

        predictor.optionalPhysicalAssistance = true;

        // Transparent until 50%, gentle until 75%, strong after 75%.
        predictor.transparentThreshold = 0.50f;
        predictor.mediumThreshold = 0.75f;
    }
    else if (assistanceIntensityLevel == 2)
    {
        currentAssistanceIntensityName = "Medium";

        predictor.optionalPhysicalAssistance = true;

        // Transparent until 25%, gentle until 60%, strong after 60%.
        predictor.transparentThreshold = 0.25f;
        predictor.mediumThreshold = 0.60f;
    }
    else if (assistanceIntensityLevel == 3)
    {
        currentAssistanceIntensityName = "High";

        predictor.optionalPhysicalAssistance = true;

        // Transparent until 10%, gentle until 55%, strong after 55%.
        predictor.transparentThreshold = 0.10f;
        predictor.mediumThreshold = 0.55f;
    }

    SyncAssistanceUI();
}

    void SyncAssistanceUI()
    {
        string labelText = "Assistance: " + currentAssistanceIntensityName;

        if (pauseAssistanceLabel != null)
            pauseAssistanceLabel.text = labelText;

        if (endAssistanceLabel != null)
            endAssistanceLabel.text = labelText;

        if (pauseAssistanceSlider != null)
            pauseAssistanceSlider.SetValueWithoutNotify(assistanceIntensityLevel);

        if (endAssistanceSlider != null)
            endAssistanceSlider.SetValueWithoutNotify(assistanceIntensityLevel);
    }

    void CountBricksAndPoints()
    {
        number_unbreakable = 0;
        totalPoints = 0;

        GameObject[] brickObjects = GameObject.FindGameObjectsWithTag("Brick");
        numberOfBricks = brickObjects.Length;

        for (int i = 0; i < bricks.Length; i++)
        {
            if (bricks[i] == null) continue;

            if (bricks[i].unbreakable)
            {
                number_unbreakable += 1;
            }
            else
            {
                totalPoints += bricks[i].states.Length;
            }
        }

        numberOfBricks -= number_unbreakable;
        Total_numberBricks = numberOfBricks;

        if (brick != null)
            totalPoints *= brick.points;
    }

    void UpdateHearts()
    {
        if (hearts == null || hearts.Length == 0)
            return;

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;
            hearts[i].sprite = i < lives ? fullHeart : emptyHeart;
        }
    }

    void ResetPerformanceTracking()
    {
        performanceSamples = 0;
        transparentSamples = 0;
        gentleAssistSamples = 0;
        strongAssistSamples = 0;

        errorPercentSum = 0f;
        maxErrorPercent = 0f;

        sessionTimeRemaining = sessionDuration;
        sessionCompleted = false;

        if (sessionTimerText != null)
            sessionTimerText.text = Mathf.CeilToInt(sessionTimeRemaining).ToString();
        
        SetupAssistanceSliders();   
        
        if (PerformancePanel != null)
            PerformancePanel.SetActive(false);

        if (decreaseHelpButton != null)
            decreaseHelpButton.gameObject.SetActive(false);
    }

    void UpdatePerformanceTracking()
    {
        if (predictor == null || !predictor.hasPrediction)
            return;

        performanceSamples++;

        float error = predictor.lastErrorPercent;
        errorPercentSum += error;

        if (error > maxErrorPercent)
            maxErrorPercent = error;

        if (predictor.lastFeedbackState == BallReturnCheckpoint.FeedbackState.Safe)
            transparentSamples++;

        if (predictor.lastAssistLevel == BallReturnCheckpoint.AssistLevel.Gentle)
            gentleAssistSamples++;

        if (predictor.lastAssistLevel == BallReturnCheckpoint.AssistLevel.Strong)
            strongAssistSamples++;
    }

    void UpdateSessionTimer()
    {
        if (!useTimedSession || !inGame || sessionCompleted)
            return;

        sessionTimeRemaining -= Time.deltaTime;

        if (sessionTimerText != null)
            sessionTimerText.text = Mathf.CeilToInt(Mathf.Max(sessionTimeRemaining, 0f)).ToString() + " s";

        if (sessionTimeRemaining <= 0f)
        {
            sessionTimeRemaining = 0f;
            CompleteTimedSession();
        }
    }

    void CompleteTimedSession()
    {
        sessionCompleted = true;
        inGame = false;

        if (sessionTimerText != null)
            sessionTimerText.text = "0 s";

        if (ball != null)
            ball.gameObject.SetActive(false);

        if (Balltwo != null && Balltwo.activeSelf)
            Balltwo.SetActive(false);

        // IMPORTANT:
        // Timer ending is NOT game over.
        // Hide GameOver and Winner panels, show only the performance summary.
        if (GameOverPanel != null)
            GameOverPanel.SetActive(false);

        if (WinnerPanel != null)
            WinnerPanel.SetActive(false);

        ShowPerformanceSummary(true);

        if (paddle != null)
            paddle.SendAssistLevel(0);
    }

    void ShowPerformanceSummary(bool won)
    {
        if (PerformancePanel != null)
        {
            PerformancePanel.SetActive(true);

            // Bring performance panel in front of old UI panels.
            PerformancePanel.transform.SetAsLastSibling();
        }

        float meanError = performanceSamples > 0 ? errorPercentSum / performanceSamples : 0f;
        float transparentRatio = performanceSamples > 0 ? (float)transparentSamples / performanceSamples : 0f;
        float gentleRatio = performanceSamples > 0 ? (float)gentleAssistSamples / performanceSamples : 0f;
        float strongRatio = performanceSamples > 0 ? (float)strongAssistSamples / performanceSamples : 0f;

        float trackingAccuracy = Mathf.Clamp01(1f - meanError);

        bool goodPerformance =
            meanError <= goodMeanErrorThreshold &&
            transparentRatio >= goodTransparentTimeThreshold;

        if (performanceText != null)
        {
            string resultText;

            if (sessionCompleted)
                resultText = "Session complete!";
            else
                resultText = won ? "Game completed!" : "Game over.";

            string summary =
                "<b>" + resultText + "</b>\n\n" +
                "Tracking accuracy: <b>" + (trackingAccuracy * 100f).ToString("F1") + "%</b>\n" +
                "Mean tracking error: " + (meanError * 100f).ToString("F1") + "%\n" +
                "Max tracking error: " + (maxErrorPercent * 100f).ToString("F1") + "%\n" +
                "Transparent time: " + (transparentRatio * 100f).ToString("F1") + "%\n" +
                "Gentle assist: " + (gentleRatio * 100f).ToString("F1") + "%\n" +
                "Strong assist: " + (strongRatio * 100f).ToString("F1") + "%\n\n" +
                "<b>Assistance intensity:</b> " + currentAssistanceIntensityName + "\n\n";

            if (goodPerformance)
            {
                summary += "<color=#5CFF7A>Recommendation: try decreasing assistance.</color>";
            }
            else
            {
                summary += "<color=#FFD166>Recommendation: keep or increase assistance.</color>";
            }
            performanceText.text = summary;
        }

        if (decreaseHelpButton != null)
            decreaseHelpButton.gameObject.SetActive(goodPerformance);
    }

    public void DecreaseHelpFromUI()
    {
        if (predictor != null)
        {
            predictor.DecreaseHelp();

            if (performanceText != null)
            {
                performanceText.text +=
                    "\n\nAssistance decreased: the transparent region has been increased.";
            }
        }

        if (decreaseHelpButton != null)
            decreaseHelpButton.gameObject.SetActive(false);
    }

    public void UpdateLives(int changeInLives)
    {
        lives += changeInLives;
        number_collision = -1;

        if (lives <= 0)
        {
            lives = 0;
            GameOver();
        }

        if (lives == 1 && fb != null)
            fb.OneLife();
    }

    public void UpdateNumberOfBricks()
    {
        numberOfBricks--;

        if (fb != null)
        {
            if (numberOfBricks == Total_numberBricks / 2)
                fb.HalfLevel();

            if (numberOfBricks == 1)
                fb.LastOne();
        }

        if (numberOfBricks <= 0)
            Winner();
    }

    public void NumberCollision()
    {
        if (paddle == null)
            return;

        if (paddle.collision_number == number_collision && flag == 0)
        {
            if (fb != null)
                fb.Combo();

            flag = 1;
        }
        else
        {
            flag = 0;
        }

        number_collision = paddle.collision_number;
    }

    void GameOver()
    {
        sessionCompleted = false;
        inGame = false;

        if (GameOverPanel != null)
            GameOverPanel.SetActive(false);

        if (WinnerPanel != null)
            WinnerPanel.SetActive(false);

        if (Balltwo != null && Balltwo.activeSelf)
            Balltwo.SetActive(false);

        ShowPerformanceSummary(false);

        if (paddle != null)
            paddle.SendAssistLevel(0);
    }

    void Winner()
    {
        sessionCompleted = false;
        inGame = false;

        if (WinnerPanel != null)
            WinnerPanel.SetActive(true);

        if (GameOverPanel != null)
            GameOverPanel.SetActive(false);

        if (Balltwo != null && Balltwo.activeSelf)
            Balltwo.SetActive(false);

        ShowPerformanceSummary(true);

        if (paddle != null)
            paddle.SendAssistLevel(0);
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        inGame = false;

        if (PauseMenu != null)
            PauseMenu.SetActive(true);

        if (paddle != null)
            paddle.SendAssistLevel(0);
    }

    public void resume()
    {
        if (PauseMenu != null)
            PauseMenu.SetActive(false);

        inGame = true;
        Time.timeScale = 1f;
    }

    public void Play()
    {
        Time.timeScale = 1f;

        HideEndPanels();

        if (PauseMenu != null)
            PauseMenu.SetActive(false);

        StartCoroutine(WaitCountDownPlay());
    }

    // Use this for the Play Again button on the PerformancePanel.
    public void RestartSession()
    {
        Time.timeScale = 1f;

        HideEndPanels();

        if (level1Panel != null)
            level1Panel.SetActive(false);

        StartCoroutine(WaitCountDownPlay());
    }

    public void PlayAgain()
    {
        HideEndPanels();

        if (level1Panel != null)
            level1Panel.SetActive(true);
    }

    public void Back()
    {
        SceneManager.LoadScene("Menu");
    }

    public void Next1()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 12");
    }

    public void Next2()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 2");
    }

    public void Next3()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 22");
    }

    public void Next4()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 3");
    }

    public void Next5()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 32");
    }

    public void Next6()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 4");
    }

    public void Next7()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 42");
    }

    public void Next8()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 5");
    }

    public void Next9()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 52");
    }

    public void Back1()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Level 1");
    }

    public void Calibrazione()
    {
        if (PauseMenu != null) PauseMenu.SetActive(false);
        SceneManager.LoadScene("Calibration");
    }

    public void ResetGame()
    {
        if (ball != null)
            ball.ResetBall();

        if (paddle != null)
        {
            paddle.ResetPaddle();
            paddle.SendAssistLevel(0);
        }

        Destroy(GameObject.Find("Life(Clone)"));
        Destroy(GameObject.Find("Multiball(Clone)"));
        Destroy(GameObject.Find("Pause(Clone)"));
        Destroy(GameObject.Find("BiggerPaddle(Clone)"));

        if (Balltwo != null)
            Balltwo.SetActive(false);

        bricks = FindObjectsOfType<BrickScript>();
        for (int i = 0; i < bricks.Length; i++)
        {
            if (bricks[i] != null)
                bricks[i].ResetBricks();
        }

        CountBricksAndPoints();

        score = 0;
        lives = 3;

        if (scoreText != null)
            scoreText.text = score.ToString();

        number_collision = -1;

        ResetPerformanceTracking();

        if (predictor != null)
            predictor.ClearPrediction();
    }

    IEnumerator WaitCountDownPlay()
    {
        HideEndPanels();

        if (level1Panel != null)
            level1Panel.SetActive(false);

        ResetGame();

        if (ball != null)
            ball.gameObject.SetActive(false);

        if (countDownText != null) countDownText.text = "3";
        yield return new WaitForSeconds(1f);

        if (countDownText != null) countDownText.text = "2";
        yield return new WaitForSeconds(1f);

        if (countDownText != null) countDownText.text = "1";
        yield return new WaitForSeconds(1f);

        if (countDownText != null) countDownText.text = "";

        if (ball != null)
        {
            ball.gameObject.SetActive(true);
            inGame = true;
            ball.StartBall();
        }
    }

    public void SuperPowers()
    {
        if (ball == null)
            return;

        if (inGame && ball.inPlay)
        {
            InstantiationTimer -= Time.deltaTime;

            if (InstantiationTimer <= 0)
            {
                int s = Random.Range(0, 3);

                if (s == 0 && Balltwo != null && !Balltwo.activeSelf && MultiBall != null)
                {
                    Instantiate(MultiBall, new Vector3(-9, +4, 0), Quaternion.identity);
                }
                else if (s == 1 && Life != null)
                {
                    Instantiate(Life, new Vector3(-9, +4, 0), Quaternion.identity);
                }
                else if (s == 2 && BiggerPaddle != null)
                {
                    Instantiate(BiggerPaddle, new Vector3(-9, +4, 0), Quaternion.identity);
                }

                InstantiationTimer = 15f;
            }
        }
    }
}