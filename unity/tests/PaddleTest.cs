using UnityEngine;
using System.Collections;
using System.IO.Ports;
using System;

public class PaddleTest : MonoBehaviour
{
    [Header("Paddle movement")]
    public float speedPaddle = 5f;
    public float upScreenEdge = 4f;
    public float downScreenEdge = -4f;

    [Header("Game refs")]
    public GameManager gm;
    public BallScript Ball;
    public TMPro.TMP_Dropdown paddleDimDrop;

    [Header("Debug / angle info")]
    public float alpha;  // value from Arduino (0–100)
    public float beta;   // kept for GameManager
    public float gamma;  // unused, kept for compatibility

    [Header("Ball prediction & robot assistance")]
    public float targetX = 7.6f;       // asse y del paddle
    public float assistanceFactor = 0.5f; // percentuale di aiuto robot
    float assistedDelta;

    public int collision_number = 0;

    Vector3 oldscale;
    float oldupScreenEdge;
    float olddownScreenEdge;

    // --- Arduino serial ---
    SerialPort sp2;
    bool serialDisabled = false;   // if true we stop trying and only use keyboard
    float lastSendTime = 0f;
    float sendInterval = 0.05f;  // 50ms
    float predictedY = 0; //Ball.PredictYAtX(targetX, downScreenEdge, upScreenEdge);

    void Start()
    {
        // Try to open COM10 at 9600 baud
        try
        {
            sp2 = new SerialPort("COM12", 9600);  // <--- YOUR PORT
            sp2.ReadTimeout = 10;
            sp2.Open();
            Debug.Log("[Paddle] Serial port opened on " + sp2.PortName);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Paddle] Could not open serial port: " + e.Message);
            sp2 = null;           // no serial
            serialDisabled = true;
        }
    }

    void Update()
    {
        // TEMP: if you ever want to force testing, you can comment this out.
        if (!gm.inGame) return;

        // 1) Keyboard always works
        UpdateFromKeyboard();

        // 2) Arduino tries to override if working
        if (!serialDisabled)
        {
            UpdateFromArduino();
        }

        // 3) Clamp to screen limits
        float clampedY = Mathf.Clamp(transform.position.y, downScreenEdge, upScreenEdge);
        transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);

        float deltaY = predictedY - transform.position.y;
        assistedDelta = deltaY * assistanceFactor;

        Vector3 pos = transform.position;
        pos.y += assistedDelta * Time.deltaTime;
        transform.position = pos;

        // (Opzional) Debug
        // Debug.Log($"PredictedY={predictedY:F2}, deltaY={deltaY:F2}, assistedDelta={assistedDelta:F2}");
    }

    void UpdateFromKeyboard()
    {
        float vertical = Input.GetAxis("Vertical");   // Up/Down arrows, W/S

        if (Mathf.Abs(vertical) > 0.01f)
        {
            // Move visually up/down in world sp2ace
            transform.Translate(Vector2.up * vertical * Time.deltaTime * speedPaddle, Space.World);
        }
    }

    void UpdateFromArduino()
    {
        if (sp2 == null || !sp2.IsOpen)
            return;

        try
        {
            // Expect a value between 0 and 100 from Arduino
            int value = sp2.ReadByte();

            value = Mathf.Clamp(value, 0, 100);
            alpha = value; // show in Insp2ector

            // Normalize 0–100 -> 0–1
            float t = value / 100f;

            // Map to paddle Y range
            float posY = Mathf.Lerp(downScreenEdge, upScreenEdge, t);

            // DEBUG: log what we read & where we move
            Debug.Log($"[Paddle] Serial value={value}, t={t:F2}, posY={posY:F2}");

            transform.position = new Vector3(transform.position.x, posY, transform.position.z);
        }
        catch (TimeoutException)
        {
            // no data this frame: ignore
        }
        catch (Exception e)
        {
            // This is where you were seeing "The device does not recognize the command"
            Debug.LogWarning("[Paddle] Serial read error in PaddleScript: " + e.Message);

            // Turn off serial so it stops spamming and keyboard can control everything
            serialDisabled = true;

            try
            {
                if (sp2.IsOpen) sp2.Close();
            }
            catch { }

            sp2 = null;
        }


        float targetValue = Mathf.Lerp(510f, 950f, Mathf.InverseLerp(-4f, 4f, predictedY));


        if (Time.time - lastSendTime > sendInterval)
        {
            lastSendTime = Time.time;

            if (sp2 != null && sp2.IsOpen)
            {
                try
                {
                    sp2.WriteLine(Mathf.RoundToInt(targetValue).ToString());
                }
                catch
                {
                    serialDisabled = true;
                }
            }
        }


    }

    void OnDestroy()
    {
        try
        {
            if (sp2 != null && sp2.IsOpen)
                sp2.Close();
        }
        catch { }
    }

    void OnApplicationQuit()
    {
        try
        {
            if (sp2 != null && sp2.IsOpen)
                sp2.Close();
        }
        catch { }
    }

    // -----------------------------
    // Existing game logic
    // -----------------------------

    public void ResetPaddle()
    {
        transform.position = new Vector2(transform.position.x, downScreenEdge);
        collision_number = 0;
    }

    public void PaddleDimSelector()
    {
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
        if (other.gameObject.tag == "Ball")
        {
            collision_number += 1;
            gm.audioSource[0].Play();
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
}
 