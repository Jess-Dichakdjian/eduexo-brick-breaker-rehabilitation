using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BalltwoScript1: MonoBehaviour
{
	GameManager gm;
	BallScript ball;

	private void OnTriggerEnter2D(Collider2D other) // se tocco il collider di destra ho perso la pallina
	{
		if (other.tag == "Right")
		{
			this.gameObject.SetActive(false);
		}
	}

	void Update()
	{
		if(gm == null)
		    gm = GameObject.Find("GameManager")?.GetComponent<GameManager>();

		if(ball == null)
		    ball= GameObject.Find("Ball")?.GetComponent<BallScript>();

		if (ball != null && !ball.inPlay)
		{
			this.gameObject.SetActive(false);
		}
	}

}