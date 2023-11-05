#pragma warning disable
using UnityEngine;
using System.Collections.Generic;

public class springBoardC# : MonoBehaviour {	
	public GameObject Player = null;
	
	private void OnTriggerEnter2D(Collider2D colliderInfo) {
		if(colliderInfo.gameObject.CompareTag("Player")) {
			Debug.Log("Player In");
			Player.GetComponent<Rigidbody2D>().AddForce(new Vector2(0F, 15F));
		}
	}
	
	private void OnCollisionEnter2D(Collision2D collisionInfo) {
	}
}

