using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiBallScript : MonoBehaviour
{

    public Rigidbody2D rb;
public float speed;
public GameObject balltwo;
public GameObject balltwoparent;
public Rigidbody2D rballtwo;
public BallScript ballscript;
public GameObject ball;
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
    //Ball = GameObject.Find("ball").GetComponent<BallScript>();
    //    if (Ball.inPlay == false)
    //{
    //    Destroy(GameObject.Find("Multiball(Clone)"));
   //}
    Ball = GameObject.Find("ball")?.GetComponent<BallScript>();
        if (Ball != null && !Ball.inPlay)
    {
        Destroy(GameObject.Find("Multiball(Clone)"));
    }
}

void OnTriggerEnter2D(Collider2D other)
{
    if (other.CompareTag("Paddle")) // selo tocco con il paddle genero la seconda pallina
        {
        Destroy(GameObject.Find("Multiball(Clone)"));
        balltwoparent = GameObject.Find("BalltwoParent");
        balltwo = balltwoparent.transform.Find("Balltwo").gameObject;
        ball = GameObject.Find("Ball");
        balltwo.transform.position = ball.transform.position;
        balltwo.SetActive(true);
        Vector2 force = Vector2.zero;
        force.x = -1f;
        force.y = Random.Range(-1f, 1f);
        rballtwo = balltwo.GetComponent<Rigidbody2D>();

        ballscript = ball.GetComponent<BallScript>();

        rballtwo.AddForce(force.normalized * ballscript.speedBall);

    }
    if (other.tag == "Right")
    {
        Destroy(GameObject.Find("Multiball(Clone)"));
    }

}
}