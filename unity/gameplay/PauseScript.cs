using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseScript : MonoBehaviour
{
    public Rigidbody2D rb;
    public float speed;
    GameManager gm;
    BallScript ball;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }


    void Update()
    {
        rb.linearVelocity = transform.right * speed;

        gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        ball = GameObject.Find("Ball").GetComponent<BallScript>();
        if (ball.inPlay == false)
        {
            Destroy(GameObject.Find("Pause(Clone)"));
        }

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Paddle"))
        {
            Destroy(GameObject.Find("Pause(Clone)"));
            
            ball.PausePower();
        }
        if (other.tag == "Right")
        {
            Destroy(GameObject.Find("Pause(Clone)"));
        }
    }



}