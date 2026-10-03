using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallScript : MonoBehaviour
{
    public Rigidbody2D rb;
    public bool inPlay;
    public Transform paddle;
    public float speedBall;
    private float speedBallDefault = 200f + 200f * (3 / 4);

    //public Transform explosion;
    public GameManager gm;
    public BrickScript brick;
    public TMPro.TMP_Dropdown speedBallDrop;
    public TMPro.TMP_Dropdown BBTDrop;
    public int cont;

    public Vector2 oldvelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Physics2D.IgnoreLayerCollision(7, 8,true); // per non far scontrare le due palline (rossa e bianca)


    }


    
    void Update()
    {

        if (!gm.inGame)
            return;
        if (inPlay == false)
            ResetBall();

        if (cont >= 6)
        {
            Vector2 force2 = Vector2.zero;
            force2.x = -1f;
            force2.y = Random.Range(-1f, 1f);

            Vector2 vel = rb.linearVelocity;

            rb.linearVelocity = vel.magnitude * force2.normalized;
            cont = 0;
        }
    }

    public void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.tag == "up")
        {
            cont += 1;
        }
        if (other.gameObject.tag == "down")
        {
            cont += 1;
        }
    }

    public void StartBall()
    {
        Invoke("SetRandomTrajectory", 1f);
        inPlay = true;
    }
    public void ResetBall()
    {
        transform.position = new Vector3(-1f, 0f, 0f);
        rb.linearVelocity = Vector2.zero;
    }

    private void SetRandomTrajectory() //do alla pallina una forza iniziale diretta verso destra con direzione casuale lungo y
    {
        Vector2 force = Vector2.zero;
        force.x = 1f;
        force.y = Random.Range(-1f, 1f);

        rb.AddForce(force.normalized * speedBall);
    }

    private void OnTriggerEnter2D(Collider2D other)     //se tocco il collider di destra perdo una vita
    {
        if (other.tag == "Right")
        {
            rb.linearVelocity = Vector2.zero;
            inPlay = false;

            if (gm != null)
            {
                gm.UpdateLives(-1);
            }
            else
            { 
                Debug.LogError ("GameManager (gm) is null!");
            }

            cont = 0;
        
            if (gm != null && gm.inGame)
            {
                StartCoroutine(WaitCountDown());
            }
        }
        
    }

    public void BBTSelector()
    {
        if (BBTDrop.value == 0)
            speedBallDefault = 200f + 200f*(3/4);
        else if (BBTDrop.value == 1)
            speedBallDefault = 200f + 200f;
    }
    
    public void SpeedBallSelector()
    {
        if (speedBallDrop.value == 1)
            speedBall = speedBallDefault - 80f;
        else if (speedBallDrop.value == 2)
            speedBall = speedBallDefault -40f;
        else if (speedBallDrop.value == 0)
            speedBall = speedBallDefault;
        else if (speedBallDrop.value == 3)
            speedBall = speedBallDefault + 40f;
        else if (speedBallDrop.value == 4)
            speedBall = speedBallDefault + 80f;

    }



    IEnumerator WaitCountDown()
    {
        inPlay = false;
        Destroy(GameObject.Find("BiggerPaddle(Clone)"));
        Destroy(GameObject.Find("Life(Clone)"));
        Destroy(GameObject.Find("Multiball(Clone)"));
        Destroy(GameObject.Find("Pause(Clone)"));
        ResetBall();

        gm.countDownText.text = "3";
        yield return new WaitForSeconds(1f);
        gm.countDownText.text = "2";
        yield return new WaitForSeconds(1f);
        gm.countDownText.text = "1";
        yield return new WaitForSeconds(1f);
        gm.countDownText.text = "";

        StartBall();

         yield return new WaitUntil(() => inPlay);
    }

    public void PausePower()
    {
        oldvelocity = rb.linearVelocity;
        rb.linearVelocity = Vector2.zero;
        Invoke("RestartBall", 7f);
    }
    public void RestartBall()
    {
        rb.linearVelocity = oldvelocity;
    }

}
