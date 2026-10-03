using UnityEngine;
using System;
using System.IO;
using System.Globalization;

public class ExoPerformanceLogger : MonoBehaviour
{
    [Header("References")]
    public GameManager gm;
    public PaddleScript paddle;
    public BallScript ball;
    public BallReturnCheckpoint predictor;

    [Header("Logging Settings")]
    public bool enableLogging = true;
    public float logInterval = 0.02f; // 50 Hz
    public string folderName = "ExoLogs";

    [Header("Debug")]
    public bool isLogging = false;
    public int currentGameIndex = 0;
    public string currentLogPath = "";
    public string currentSummaryPath = "";

    private StreamWriter writer;
    private float lastLogTime = 0f;
    private float gameStartTime = 0f;
    private bool wasInGame = false;

    // Summary values
    private int sampleCount = 0;
    private int predictionSampleCount = 0;
    private int transparentSampleCount = 0;

    private float errorPercentSum = 0f;
    private float maxErrorPercent = 0f;

    private float residualTorqueSumTransparent = 0f;
    private float maxResidualTorqueTransparent = 0f;
    private int residualTorqueTransparentSamples = 0;

    private int assistanceActivationCount = 0;
    private int previousAssistLevel = 0;

    void Start()
    {
        if (gm == null) gm = FindObjectOfType<GameManager>();
        if (paddle == null) paddle = FindObjectOfType<PaddleScript>();
        if (ball == null) ball = FindObjectOfType<BallScript>();
        if (predictor == null) predictor = FindObjectOfType<BallReturnCheckpoint>();
    }

    void Update()
    {
        if (!enableLogging || gm == null)
            return;

        if (gm.inGame && !wasInGame)
            StartNewGameLog();

        if (!gm.inGame && wasInGame)
            StopGameLog();

        wasInGame = gm.inGame;

        if (!isLogging)
            return;

        if (Time.time - lastLogTime >= logInterval)
        {
            LogSample();
            lastLogTime = Time.time;
        }
    }

    void StartNewGameLog()
    {
        currentGameIndex++;

        string folderPath = Path.Combine(Application.dataPath, folderName);
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        currentLogPath = Path.Combine(folderPath, "exo_game_" + currentGameIndex + "_" + timestamp + ".csv");
        currentSummaryPath = Path.Combine(folderPath, "exo_game_" + currentGameIndex + "_" + timestamp + "_summary.csv");

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time," +
            "gameIndex," +
            "sliderY," +
            "ballY," +
            "targetY," +
            "errorWorld," +
            "errorPercent," +
            "transparentLower," +
            "transparentUpper," +
            "inTransparentRegion," +
            "feedbackState," +
            "assistLevel," +
            "residualTorquePercent," +
            "servoCommand," +
            "alpha," +
            "arduinoTargetAngle," +
            "lives," +
            "collisions"
        );

        ResetSummaryValues();

        gameStartTime = Time.time;
        lastLogTime = Time.time;
        isLogging = true;

        Debug.Log("[ExoLogger] Started logging: " + currentLogPath);
    }

    void StopGameLog()
    {
        if (!isLogging)
            return;

        isLogging = false;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }

        SaveSummary();

        Debug.Log("[ExoLogger] Stopped logging.");
        Debug.Log("[ExoLogger] Data: " + currentLogPath);
        Debug.Log("[ExoLogger] Summary: " + currentSummaryPath);
    }

    void ResetSummaryValues()
    {
        sampleCount = 0;
        predictionSampleCount = 0;
        transparentSampleCount = 0;

        errorPercentSum = 0f;
        maxErrorPercent = 0f;

        residualTorqueSumTransparent = 0f;
        maxResidualTorqueTransparent = 0f;
        residualTorqueTransparentSamples = 0;

        assistanceActivationCount = 0;
        previousAssistLevel = 0;
    }

    void LogSample()
    {
        if (writer == null)
            return;

        float t = Time.time - gameStartTime;

        float sliderY = paddle != null ? paddle.transform.position.y : float.NaN;
        float ballY = ball != null ? ball.transform.position.y : float.NaN;

        bool hasPrediction = predictor != null && predictor.hasPrediction;

        float targetY = hasPrediction ? predictor.predictedPaddleY : float.NaN;
        float errorWorld = hasPrediction ? predictor.lastErrorWorld : float.NaN;
        float errorPercent = hasPrediction ? predictor.lastErrorPercent : float.NaN;

        float transparentLower = hasPrediction ? predictor.transparentLowerBound : float.NaN;
        float transparentUpper = hasPrediction ? predictor.transparentUpperBound : float.NaN;

        bool inTransparentRegion = false;
        string feedbackState = "None";
        int assistLevel = paddle != null ? paddle.currentAssistLevel : 0;

        if (hasPrediction)
        {
            inTransparentRegion = predictor.lastFeedbackState == BallReturnCheckpoint.FeedbackState.Safe;
            feedbackState = predictor.lastFeedbackState.ToString();
            assistLevel = (int)predictor.lastAssistLevel;
        }

        float residualTorquePercent = paddle != null ? paddle.residualTorquePercent : float.NaN;
        int servoCommand = paddle != null ? paddle.servoCommand : -1;
        float alpha = paddle != null ? paddle.alpha : float.NaN;
        int arduinoTargetAngle = paddle != null ? paddle.arduinoTargetAngle : -1;
        int lives = gm != null ? gm.lives : -1;
        int collisions = paddle != null ? paddle.collision_number : -1;

        writer.WriteLine(
            Format(t) + "," +
            currentGameIndex + "," +
            Format(sliderY) + "," +
            Format(ballY) + "," +
            Format(targetY) + "," +
            Format(errorWorld) + "," +
            Format(errorPercent) + "," +
            Format(transparentLower) + "," +
            Format(transparentUpper) + "," +
            Bool01(inTransparentRegion) + "," +
            feedbackState + "," +
            assistLevel + "," +
            Format(residualTorquePercent) + "," +
            servoCommand + "," +
            Format(alpha) + "," +
            arduinoTargetAngle + "," +
            lives + "," +
            collisions
        );

        UpdateSummaryValues(hasPrediction, inTransparentRegion, errorPercent, residualTorquePercent, assistLevel);
    }

    void UpdateSummaryValues(
        bool hasPrediction,
        bool inTransparentRegion,
        float errorPercent,
        float residualTorquePercent,
        int assistLevel
    )
    {
        sampleCount++;

        if (hasPrediction)
        {
            predictionSampleCount++;
            errorPercentSum += errorPercent;

            if (errorPercent > maxErrorPercent)
                maxErrorPercent = errorPercent;

            if (inTransparentRegion)
            {
                transparentSampleCount++;

                if (!float.IsNaN(residualTorquePercent))
                {
                    residualTorqueSumTransparent += residualTorquePercent;
                    residualTorqueTransparentSamples++;

                    if (residualTorquePercent > maxResidualTorqueTransparent)
                        maxResidualTorqueTransparent = residualTorquePercent;
                }
            }
        }

        if (previousAssistLevel == 0 && assistLevel > 0)
            assistanceActivationCount++;

        previousAssistLevel = assistLevel;
    }

    void SaveSummary()
    {
        float meanErrorPercent = predictionSampleCount > 0 ? errorPercentSum / predictionSampleCount : 0f;
        float percentTimeTransparent = predictionSampleCount > 0 ? (float)transparentSampleCount / predictionSampleCount * 100f : 0f;

        float meanResidualTorqueTransparent = residualTorqueTransparentSamples > 0
            ? residualTorqueSumTransparent / residualTorqueTransparentSamples
            : 0f;

        try
        {
            using (StreamWriter sw = new StreamWriter(currentSummaryPath))
            {
                sw.WriteLine("Metric,Value");
                sw.WriteLine("Game index," + currentGameIndex);
                sw.WriteLine("Total samples," + sampleCount);
                sw.WriteLine("Prediction samples," + predictionSampleCount);
                sw.WriteLine("Transparent samples," + transparentSampleCount);
                sw.WriteLine("Percent time in transparent region," + Format(percentTimeTransparent));
                sw.WriteLine("Mean tracking error percent," + Format(meanErrorPercent * 100f));
                sw.WriteLine("Max tracking error percent," + Format(maxErrorPercent * 100f));
                sw.WriteLine("Mean residual torque percent in transparent mode," + Format(meanResidualTorqueTransparent));
                sw.WriteLine("Max residual torque percent in transparent mode," + Format(maxResidualTorqueTransparent));
                sw.WriteLine("Assistance activations," + assistanceActivationCount);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ExoLogger] Could not save summary: " + e.Message);
        }
    }

    string Format(float value)
    {
        if (float.IsNaN(value))
            return "";

        return value.ToString("F4", CultureInfo.InvariantCulture);
    }

    int Bool01(bool value)
    {
        return value ? 1 : 0;
    }

    void OnApplicationQuit()
    {
        if (isLogging)
            StopGameLog();
    }
}
