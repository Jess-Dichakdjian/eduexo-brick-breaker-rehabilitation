using UnityEngine;
using System;
using System.IO.Ports;
using System.Globalization;
using System.Collections;

public class PaddleScript : MonoBehaviour
{
    [Header("Serial Settings")]
    public string serialPortName = "COM3";   // CHANGE THIS in Inspector
    public int baudRate = 115200;
    public int readTimeout = 2;
    public int maxLinesPerFrame = 20;

    [Header("Paddle Movement")]
    public float speedPaddle = 7f;
    public float upScreenEdge = 4f;
    public float downScreenEdge = -4.4f;
    public bool useArduinoControl = true;
    public bool allowKeyboardIfArduinoMissing = true;

    [Header("Direct Follow Debug")]
    public bool directFollow = true;
    public bool useRawAnalogMapping = true;
    public bool invertArmDirection = false;

    [Tooltip("Raw analog value when the arm is at the lowest comfortable paddle position.")]
    public float rawAngleMin = 300f;

    [Tooltip("Raw analog value when the arm is at the highest comfortable paddle position.")]
    public float rawAngleMax = 760f;

    [Tooltip("Alpha value when the arm is at the lowest comfortable paddle position, used only if raw mapping is off.")]
    public float armInputMin = 0f;

    [Tooltip("Alpha value when the arm is at the highest comfortable paddle position, used only if raw mapping is off.")]
    public float armInputMax = 100f;

    [Tooltip("Turn this on for one test. Move the arm slowly through the full comfortable range, then copy observed min/max into rawAngleMin/rawAngleMax.")]
    public bool autoLearnRawRange = false;

    public int observedRawMin = 1023;
    public int observedRawMax = 0;

    [Range(0f, 1f)]
    public float calibratedArm01 = 0.5f;

    public float targetPaddleY = 0f;

    [Header("Game References")]
    public GameManager gm;
    public BallScript Ball;
    public TMPro.TMP_Dropdown paddleDimDrop;

    [Header("Arduino / Exo Data")]
    public float alpha = 50f;
    public float beta = 0f;
    public float gamma = 0f;

    public int arduinoTargetAngle = 50;
    public int currentAssistLevel = 0;
    public float residualTorquePercent = 0f;
    public int servoCommand = 90;

    [Header("Extra Raw Debug From Arduino")]
    public bool hasRawAngle = false;
    public int rawAngle = -1;
    public int forceRaw = -1;

    [Header("Connection Debug")]
    public bool arduinoConnected = false;
    public float connectionTimeout = 1.0f;
    public float lastPacketAge = 999f;
    public string lastPacketText = "";
    public bool printReceivedPackets = false;

    [Header("Smoothing")]
    public float snapStep = 0f;
    public float paddleSmoothSpeed = 20f;

    [Header("Visual Debug")]
    public SpriteRenderer borderSR;

    public int collision_number = 0;

    private SerialPort serialPort;
    private bool serialDisabled = false;
    private float lastPacketTime = -999f;

    private Vector3 oldscale;
    private float oldupScreenEdge;
    private float olddownScreenEdge;

    void Start()
    {
        OpenSerialPort();
    }

    void OpenSerialPort()
    {
        try
        {
            serialPort = new SerialPort(serialPortName, baudRate);
            serialPort.ReadTimeout = readTimeout;
            serialPort.WriteTimeout = 20;
            serialPort.NewLine = "\n";
            serialPort.Open();

            serialDisabled = false;
            arduinoConnected = true;

            Debug.Log("[Paddle] Serial opened on " + serialPortName + " at " + baudRate);
        }
        catch (Exception e)
        {
            serialDisabled = true;
            arduinoConnected = false;
            Debug.LogWarning("[Paddle] Could not open serial port " + serialPortName + ": " + e.Message);
        }
    }

    void Update()
    {
        ReadArduinoSerial();

        if (!serialDisabled && lastPacketTime > 0f)
        {
            lastPacketAge = Time.time - lastPacketTime;
            arduinoConnected = lastPacketAge <= connectionTimeout;
        }

        if (gm != null && !gm.inGame)
            return;

        bool canUseArduino = useArduinoControl && !serialDisabled && arduinoConnected;

        if (canUseArduino)
        {
            UpdatePaddleFromArduino();
        }
        else if (allowKeyboardIfArduinoMissing)
        {
            UpdateFromKeyboard();
        }

        ClampPaddleToScreen();
    }

    void ReadArduinoSerial()
    {
        if (serialDisabled || serialPort == null || !serialPort.IsOpen)
            return;

        int linesRead = 0;

        while (serialPort.BytesToRead > 0 && linesRead < maxLinesPerFrame)
        {
            try
            {
                string line = serialPort.ReadLine().Trim();
                linesRead++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                bool ok = ParseArduinoPacket(line);

                if (ok)
                {
                    lastPacketText = line;
                    lastPacketTime = Time.time;
                    arduinoConnected = true;

                    if (printReceivedPackets)
                        Debug.Log("[Paddle] RX: " + line);
                }
            }
            catch (TimeoutException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Paddle] Serial read error: " + e.Message);
                break;
            }
        }
    }

    bool ParseArduinoPacket(string text)
    {
        string[] parts = text.Split(',');

        // Expected first 5 values:
        // currentAngle,targetAngle,assistLevel,residualTorquePercent,servoCommand
        //
        // Optional diagnostic values:
        // rawAngle,forceRaw
        //
        // Full diagnostic format:
        // alpha,targetAngle,assistLevel,residualTorquePercent,servoCommand,rawAngle,forceRaw

        if (parts.Length >= 5)
        {
            bool okAlpha = float.TryParse(
                parts[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsedAlpha
            );

            bool okTarget = int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedTarget
            );

            bool okAssist = int.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedAssist
            );

            bool okTorque = float.TryParse(
                parts[3],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsedTorque
            );

            bool okServo = int.TryParse(
                parts[4],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedServo
            );

            if (!okAlpha)
                return false;

            alpha = Mathf.Clamp(parsedAlpha, 0f, 100f);

            if (okTarget)
                arduinoTargetAngle = Mathf.Clamp(parsedTarget, 0, 100);

            if (okAssist)
                currentAssistLevel = Mathf.Clamp(parsedAssist, 0, 2);

            if (okTorque)
                residualTorquePercent = Mathf.Clamp(parsedTorque, 0f, 100f);

            if (okServo)
                servoCommand = Mathf.Clamp(parsedServo, 0, 180);

            if (parts.Length >= 6)
            {
                bool okRawAngle = int.TryParse(
                    parts[5],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedRawAngle
                );

                if (okRawAngle)
                {
                    rawAngle = Mathf.Clamp(parsedRawAngle, 0, 1023);
                    hasRawAngle = true;

                    if (autoLearnRawRange)
                    {
                        if (rawAngle < observedRawMin)
                            observedRawMin = rawAngle;

                        if (rawAngle > observedRawMax)
                            observedRawMax = rawAngle;
                    }
                }
            }

            if (parts.Length >= 7)
            {
                bool okForceRaw = int.TryParse(
                    parts[6],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedForceRaw
                );

                if (okForceRaw)
                    forceRaw = Mathf.Clamp(parsedForceRaw, 0, 1023);
            }

            return true;
        }

        // Fallback: Arduino sends only one angle value
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float onlyAngle))
        {
            alpha = Mathf.Clamp(onlyAngle, 0f, 100f);
            return true;
        }

        return false;
    }

    void UpdatePaddleFromArduino()
    {
        float t;

        if (useRawAnalogMapping && hasRawAngle)
        {
            float minRaw = Mathf.Min(rawAngleMin, rawAngleMax);
            float maxRaw = Mathf.Max(rawAngleMin, rawAngleMax);

            if (Mathf.Abs(maxRaw - minRaw) < 1f)
                maxRaw = minRaw + 1f;

            t = Mathf.InverseLerp(minRaw, maxRaw, rawAngle);
        }
        else
        {
            float minA = Mathf.Min(armInputMin, armInputMax);
            float maxA = Mathf.Max(armInputMin, armInputMax);

            if (Mathf.Abs(maxA - minA) < 0.001f)
                maxA = minA + 0.001f;

            t = Mathf.InverseLerp(minA, maxA, alpha);
        }

        t = Mathf.Clamp01(t);

        if (invertArmDirection)
            t = 1f - t;

        calibratedArm01 = t;
        targetPaddleY = Mathf.Lerp(downScreenEdge, upScreenEdge, t);

        if (snapStep > 0f)
            targetPaddleY = Mathf.Round(targetPaddleY / snapStep) * snapStep;

        if (directFollow)
        {
            transform.position = new Vector3(
                transform.position.x,
                targetPaddleY,
                transform.position.z
            );
        }
        else
        {
            float lerpAmount = Mathf.Clamp01(Time.deltaTime * paddleSmoothSpeed);

            float smoothY = Mathf.Lerp(
                transform.position.y,
                targetPaddleY,
                lerpAmount
            );

            transform.position = new Vector3(
                transform.position.x,
                smoothY,
                transform.position.z
            );
        }

        if (borderSR != null)
            borderSR.color = Color.Lerp(Color.red, Color.green, t);
    }

    void UpdateFromKeyboard()
    {
        float vertical = Input.GetAxis("Vertical");

        if (Mathf.Abs(vertical) > 0.01f)
        {
            transform.Translate(
                Vector2.up * vertical * Time.deltaTime * speedPaddle,
                Space.World
            );
        }
    }

    void ClampPaddleToScreen()
    {
        float clampedY = Mathf.Clamp(transform.position.y, downScreenEdge, upScreenEdge);

        transform.position = new Vector3(
            transform.position.x,
            clampedY,
            transform.position.z
        );
    }

    // Unity sends to Arduino:
    // assistLevel,targetAngle
    // Example:
    // 0,50
    // 1,72
    // 2,20
    public void SendAssistCommand(int assistLevel, int targetAngle01)
    {
        assistLevel = Mathf.Clamp(assistLevel, 0, 2);
        targetAngle01 = Mathf.Clamp(targetAngle01, 0, 100);

        currentAssistLevel = assistLevel;
        arduinoTargetAngle = targetAngle01;

        if (serialDisabled || serialPort == null || !serialPort.IsOpen)
            return;

        try
        {
            string command = assistLevel.ToString() + "," + targetAngle01.ToString();
            serialPort.WriteLine(command);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Paddle] Serial send error: " + e.Message);
        }
    }

    public void SendAssistLevel(int level)
    {
        level = Mathf.Clamp(level, 0, 2);
        currentAssistLevel = level;

        if (serialDisabled || serialPort == null || !serialPort.IsOpen)
            return;

        try
        {
            serialPort.WriteLine(level.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Paddle] Serial send assist level error: " + e.Message);
        }
    }

    public int WorldYToAngle01(float worldY)
    {
        float t = Mathf.InverseLerp(downScreenEdge, upScreenEdge, worldY);
        return Mathf.Clamp(Mathf.RoundToInt(t * 100f), 0, 100);
    }

    public float Angle01ToWorldY(float angle01)
    {
        float t = Mathf.Clamp01(angle01 / 100f);
        return Mathf.Lerp(downScreenEdge, upScreenEdge, t);
    }

    public void ResetPaddle()
    {
        transform.position = new Vector2(transform.position.x, downScreenEdge);
        collision_number = 0;
    }

    public void PaddleDimSelector()
    {
        if (paddleDimDrop == null)
            return;

        if (paddleDimDrop.value == 0)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
            upScreenEdge = 4f;
            downScreenEdge = -4f;
        }
        else if (paddleDimDrop.value == 1)
        {
            transform.localScale = new Vector3(0.7f, 1f, 1f);
            upScreenEdge = 3.5f;
            downScreenEdge = -3.5f;
        }
        else if (paddleDimDrop.value == 2)
        {
            transform.localScale = new Vector3(1.3f, 1f, 1f);
            upScreenEdge = 4.5f;
            downScreenEdge = -4.5f;
        }
    }

    public void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Ball"))
        {
            collision_number += 1;

            if (gm != null && gm.audioSource != null && gm.audioSource.Length > 0)
                gm.audioSource[0].Play();

            if (Ball != null)
                Ball.cont = 0;
        }
    }

    public void AumentaPaddle()
    {
        oldscale = new Vector3(transform.localScale.x, 1f, 1f);
        oldupScreenEdge = upScreenEdge;
        olddownScreenEdge = downScreenEdge;

        transform.localScale = new Vector3(oldscale.x + 0.3f, 1f, 1f);

        upScreenEdge = oldupScreenEdge - 0.75f;
        downScreenEdge = olddownScreenEdge + 0.75f;

        StartCoroutine(RiduciPaddle());
    }

    IEnumerator RiduciPaddle()
    {
        yield return new WaitForSeconds(10f);

        transform.localScale = new Vector3(oldscale.x, 1f, 1f);
        upScreenEdge = oldupScreenEdge;
        downScreenEdge = olddownScreenEdge;
    }

    void OnApplicationQuit()
    {
        CloseSerialPort();
    }

    void OnDestroy()
    {
        CloseSerialPort();
    }

    void CloseSerialPort()
    {
        try
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                SendAssistLevel(0);
                serialPort.Close();
                Debug.Log("[Paddle] Serial closed.");
            }
        }
        catch { }
    }
}