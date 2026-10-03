using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LifeScript : MonoBehaviour
{
    public Rigidbody2D rb;
    public float speed;
    GameManager gm;
    BallScript Ball;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

    }


    void Update()
    {
        rb.linearVelocity = transform.right * speed;

        gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        Ball = GameObject.Find("Ball").GetComponent<BallScript>();
        if (Ball.inPlay == false)
        {
            Destroy(GameObject.Find("Life(Clone)"));
        }

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Paddle"))
        {
            Destroy(GameObject.Find("Life(Clone)"));

            if (gm.lives <= 2) // se ho già 3 vite non posso aumentarle
            {
                gm.UpdateLives(1);
            }
        }
        if (other.tag == "Right")
        {
            Destroy(GameObject.Find("Life(Clone)"));
        }

    }
}