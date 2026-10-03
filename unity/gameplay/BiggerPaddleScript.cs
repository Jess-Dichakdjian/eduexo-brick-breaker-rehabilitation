using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BiggerPaddleScript : MonoBehaviour
{
    public Rigidbody2D rb;
    public float speed;

    GameObject paddle;
    PaddleScript paddlescript;

    float oldupScreenEdge;
    float olddownScreenEdge;

    GameManager gm;
    BallScript ball;

    Vector3 oldscale;

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
            Destroy(GameObject.Find("BiggerPaddle(Clone)"));
        }


    }


    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Paddle"))
        {
            Destroy(GameObject.Find("BiggerPaddle(Clone)"));
            paddle = GameObject.Find("Paddle");
            paddlescript = paddle.GetComponent<PaddleScript>();
            paddlescript.AumentaPaddle();

        }
        if (other.tag == "Right")
        {
            Destroy(GameObject.Find("BiggerPaddle(Clone)"));
        }
    }


}