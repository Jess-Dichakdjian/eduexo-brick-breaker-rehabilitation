using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrickScript : MonoBehaviour
{
    public GameManager gm;

    public BallScript Ball;

    public Transform explosion;
    public SpriteRenderer spriteRenderer { get; private set; }

    public Sprite[] states;
    public int health { get; private set; }

    public int points = 100;

    public bool unbreakable;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        gm = FindObjectOfType<GameManager>();
        Ball = FindObjectOfType<BallScript>();

    }

    private void Start()
    {
        ResetBricks();
    }



    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.tag == "Ball" || other.gameObject.tag == "Balltwo")
        {
            if (unbreakable)
            {
                return;
            }

            gm.NumberCollision();
            health--;


            if (health <= 0) // se ho i blocchetti blu li distruggo, altrimenti diminuisco di 1 la salute
            {
                Transform newExplosion = Instantiate(explosion, this.transform.position, this.transform.rotation);
                Destroy(newExplosion.gameObject, 2f);

                gm.audioSource[1].Play();

                this.gameObject.SetActive(false);
                gm.UpdateNumberOfBricks();
                if (gm.numberOfBricks <= 0)
                    Ball.rb.linearVelocity = Vector2.zero;

            } else
            {
                spriteRenderer.sprite = states[health - 1];
                gm.audioSource[0].Play();
            }

            //gm.UpdateScore(points);
        }
        if (other.gameObject.tag == "Ball")
        {
            Ball.cont = 0;
        }
    }

  
    public void ResetBricks()
    {
        gameObject.SetActive(true);

        if (!unbreakable)
        {
            health = states.Length; 
            spriteRenderer.sprite = states[health - 1];
        }
    }






}
