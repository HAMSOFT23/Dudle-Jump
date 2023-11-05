#pragma warning disable
using UnityEngine;
using System.Collections.Generic;

namespace Bouncy {
	public class springBoard : MaxyGames.UNode.RuntimeBehaviour {	
		public UnityEngine.GameObject Player = null;
		
		private void OnTriggerEnter2D(UnityEngine.Collider2D colliderInfo) {
			if(colliderInfo.gameObject.CompareTag("Player")) {
				UnityEngine.Debug.Log("Player In");
				Player.GetComponent<UnityEngine.Rigidbody2D>().AddForce(new UnityEngine.Vector2(0F, 15F));
			}
		}
		
		private void OnCollisionEnter2D(UnityEngine.Collision2D collisionInfo) {
		}
	}

}
