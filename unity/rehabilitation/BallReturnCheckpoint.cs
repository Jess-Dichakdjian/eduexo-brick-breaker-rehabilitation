using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BallReturnCheckpoint : MonoBehaviour
{
    public enum AssistLevel
    {
        Transparent = 0,
        Gentle = 1,
        Strong = 2
    }

    public enum FeedbackState
    {
        Safe = 0,
        Medium = 1,
        Far = 2
    }

    [Header("References")]
    public Rigidbody2D ballRb;
    public Transform paddle;
    public PaddleScript paddleScript;
    public LineRenderer line;
    public SpriteRenderer ballRenderer;

    [Header("Prediction Settings")]
    public LayerMask predictionMask; // walls + TargetLine layer ONLY
    public int maxReflections = 6;
    public float maxDistancePerStep = 30f;
    public bool drawLine = true;

    [Header("Transparent Region / Feedback Thresholds")]
    [Range(0f, 1f)] public float transparentThreshold = 0.08f;
    [Range(0f, 1f)] public float mediumThreshold = 0.18f;

    [Header("Optional Physical Assistance")]
    public bool optionalPhysicalAssistance = true;
    public float sendCommandInterval = 0.05f;

    [Header("Adaptive Difficulty")]
    public float transparentThresholdIncreaseStep = 0.02f;
    public float maxTransparentThreshold = 0.25f;

    [Header("Ball Feedback Colors")]
    public Color safeColor = Color.green;
    public Color mediumColor = new Color(1f, 0.5f, 0f); // orange
    public Color farColor = Color.red;

    [Header("Checkpoint Identity")]
    public string checkpointName = "CP_A";

    [Header("State / Debug")]
    public bool hasPrediction = false;
    public float predictedPaddleY;
    public float targetAngle01;

    [Range(0f, 1f)] public float lastErrorPercent;
    public float lastErrorWorld;

    public float transparentLowerBound;
    public float transparentUpperBound;

    public AssistLevel lastAssistLevel = AssistLevel.Transparent;
    public FeedbackState lastFeedbackState = FeedbackState.Safe;

    public int assistCount = 0;

    private float lastSendTime = -999f;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Start()
    {
        if (line != null)
        {
            line.positionCount = 0;
            line.useWorldSpace = true;
        }

        if (paddleScript != null)
            paddle = paddleScript.transform;

        if (ballRenderer == null && ballRb != null)
            ballRenderer = ballRb.GetComponent<SpriteRenderer>();
    }

void Update()
{
    if (paddleScript == null)
        return;

    if (paddle == null)
        paddle = paddleScript.transform;

    if (ballRb == null)
        return;

    // FAST FALLBACK:
    // Use current ball Y as the reference target.
    // This avoids relying on the raycast prediction while debugging.
    float clampMinY = paddleScript.downScreenEdge;
    float clampMaxY = paddleScript.upScreenEdge;

    predictedPaddleY = Mathf.Clamp(ballRb.position.y, clampMinY, clampMaxY);
    hasPrediction = true;

    UpdateErrorFeedbackAndAssist();

    if (Time.time - lastSendTime >= sendCommandInterval)
    {
        SendCurrentCommandToArduino();
        lastSendTime = Time.time;
    }
}
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;
        if (ballRb == null || paddleScript == null) return;

        if (paddle == null)
            paddle = paddleScript.transform;

        Vector2 velocity = ballRb.linearVelocity;

        Debug.Log($"[{checkpointName}] Ball entered checkpoint, velocity=({velocity.x:F2}, {velocity.y:F2})");

        // If the paddle is on the right side, predict only when the ball is moving right.
        if (velocity.x <= 0f)
        {
            Debug.Log($"[{checkpointName}] Ball moving away from paddle, prediction skipped.");
            ClearPrediction();
            return;
        }

        PredictReturnPosition();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        // Clear only the drawn line. Keep prediction active while ball travels toward paddle.
        ClearLine();
    }

    void PredictReturnPosition()
    {
        float clampMinY = paddleScript.downScreenEdge;
        float clampMaxY = paddleScript.upScreenEdge;

        Vector2 center = ballRb.position;
        Vector2 dir = ballRb.linearVelocity.normalized;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        float radius = 0.2f;
        Vector2 pos = center + dir * radius;

        if (line != null && drawLine)
        {
            line.positionCount = 1;
            line.SetPosition(0, pos);
        }

        hasPrediction = false;
        predictedPaddleY = Mathf.Clamp(pos.y, clampMinY, clampMaxY);

        for (int i = 0; i < maxReflections; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(pos, dir, maxDistancePerStep, predictionMask);

            if (!hit)
            {
                if (line != null && drawLine)
                {
                    Vector2 end = pos + dir * maxDistancePerStep;
                    line.positionCount++;
                    line.SetPosition(line.positionCount - 1, end);
                }
                break;
            }

            if (line != null && drawLine)
            {
                line.positionCount++;
                line.SetPosition(line.positionCount - 1, hit.point);
            }

            if (hit.collider.CompareTag("TargetLine"))
            {
                predictedPaddleY = Mathf.Clamp(hit.point.y, clampMinY, clampMaxY);
                hasPrediction = true;

                Debug.Log($"[{checkpointName}] Predicted paddle Y = {predictedPaddleY:F2}");

                UpdateErrorFeedbackAndAssist();
                SendCurrentCommandToArduino();
                lastSendTime = Time.time;
                break;
            }

            dir = Vector2.Reflect(dir, hit.normal).normalized;
            pos = hit.point + dir * 0.01f;
        }
    }

    void UpdateErrorFeedbackAndAssist()
    {
        float clampMinY = paddleScript.downScreenEdge;
        float clampMaxY = paddleScript.upScreenEdge;
        float range = Mathf.Max(0.0001f, clampMaxY - clampMinY);

        predictedPaddleY = Mathf.Clamp(predictedPaddleY, clampMinY, clampMaxY);

        lastErrorWorld = Mathf.Abs(predictedPaddleY - paddle.position.y);
        lastErrorPercent = lastErrorWorld / range;

        float transparentHalfWidth = transparentThreshold * range;
        transparentLowerBound = Mathf.Clamp(predictedPaddleY - transparentHalfWidth, clampMinY, clampMaxY);
        transparentUpperBound = Mathf.Clamp(predictedPaddleY + transparentHalfWidth, clampMinY, clampMaxY);

        if (lastErrorPercent < transparentThreshold)
        {
            lastFeedbackState = FeedbackState.Safe;
            lastAssistLevel = AssistLevel.Transparent;
        }
        else if (lastErrorPercent < mediumThreshold)
        {
            lastFeedbackState = FeedbackState.Medium;
            lastAssistLevel = optionalPhysicalAssistance ? AssistLevel.Gentle : AssistLevel.Transparent;
        }
        else
        {
            lastFeedbackState = FeedbackState.Far;
            lastAssistLevel = optionalPhysicalAssistance ? AssistLevel.Strong : AssistLevel.Transparent;
        }

        UpdateBallColor();
        targetAngle01 = paddleScript.WorldYToAngle01(predictedPaddleY);
    }

    void UpdateBallColor()
    {
        if (ballRenderer == null)
            return;

        if (lastFeedbackState == FeedbackState.Safe)
        {
            ballRenderer.color = safeColor;
        }
        else if (lastFeedbackState == FeedbackState.Medium)
        {
            float t = Mathf.InverseLerp(transparentThreshold, mediumThreshold, lastErrorPercent);
            ballRenderer.color = Color.Lerp(safeColor, mediumColor, t);
        }
        else
        {
            float t = Mathf.InverseLerp(mediumThreshold, 0.5f, lastErrorPercent);
            ballRenderer.color = Color.Lerp(mediumColor, farColor, t);
        }
    }

    void SendCurrentCommandToArduino()
    {
        if (paddleScript == null)
            return;

        int assistInt = (int)lastAssistLevel;
        int targetInt = Mathf.Clamp(Mathf.RoundToInt(targetAngle01), 0, 110);

        paddleScript.currentAssistLevel = assistInt;
        paddleScript.SendAssistCommand(assistInt, targetInt);

        assistCount++;

        Debug.Log(
            $"[{checkpointName}] Command #{assistCount} | " +
            $"assist={lastAssistLevel} | feedback={lastFeedbackState} | " +
            $"predY={predictedPaddleY:F2} | paddleY={paddle.position.y:F2} | " +
            $"error%={lastErrorPercent:F3} | target01={targetInt}"
        );
    }

    public void DecreaseHelp()
    {
        // Larger transparent region = assistance activates less often.
        transparentThreshold += transparentThresholdIncreaseStep;
        transparentThreshold = Mathf.Clamp(transparentThreshold, 0.01f, maxTransparentThreshold);

        // Keep medium threshold above transparent threshold.
        mediumThreshold = Mathf.Max(mediumThreshold, transparentThreshold + 0.02f);

        Debug.Log($"[{checkpointName}] Help decreased. New transparent threshold = {transparentThreshold:F3}");
    }

    public void IncreaseHelp()
    {
        transparentThreshold -= transparentThresholdIncreaseStep;
        transparentThreshold = Mathf.Clamp(transparentThreshold, 0.01f, maxTransparentThreshold);

        Debug.Log($"[{checkpointName}] Help increased. New transparent threshold = {transparentThreshold:F3}");
    }

    public void ClearPrediction()
    {
        hasPrediction = false;
        lastAssistLevel = AssistLevel.Transparent;
        lastFeedbackState = FeedbackState.Safe;
        lastErrorPercent = 0f;
        lastErrorWorld = 0f;
        transparentLowerBound = 0f;
        transparentUpperBound = 0f;
        ClearLine();

        if (ballRenderer != null)
            ballRenderer.color = safeColor;

        if (paddleScript != null)
            paddleScript.SendAssistLevel(0);
    }

    void ClearLine()
    {
        if (line != null)
            line.positionCount = 0;
    }
}
