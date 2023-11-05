#pragma warning disable
using UnityEngine;
using System.Collections.Generic;

namespace test {
	public class jugadorController : MaxyGames.UNode.RuntimeBehaviour {	
		public float playerSpeed = 2.8F;
		public float playerJumpForce = 6F;
		public bool maxVelocityReached;
		
		/// <summary>
		/// Update frame by frame
		/// </summary>
		private void Update() {
			this.GetComponent<UnityEngine.Rigidbody2D>().velocity = new UnityEngine.Vector2((UnityEngine.Input.GetAxisRaw("Horizontal") * playerSpeed), this.GetComponent<UnityEngine.Rigidbody2D>().velocity.y);
		}
		
		private void OnCollisionEnter2D(UnityEngine.Collision2D collisionInfo) {
			if(collisionInfo.collider.gameObject.CompareTag("Platform")) {
				UnityEngine.Debug.Log("Collision Detected");
				this.GetComponent<UnityEngine.Rigidbody2D>().AddRelativeForce(new UnityEngine.Vector2(0F, playerJumpForce), UnityEngine.ForceMode2D.Impulse);
			}
		}
		
		private void FixedUpdate() {
			if((this.GetComponent<UnityEngine.Rigidbody2D>().velocity.y >= 0F)) {
				maxVelocityReached = true;
				base.GetComponent<UnityEngine.BoxCollider2D>().enabled = false;
			}
			 else if((this.GetComponent<UnityEngine.Rigidbody2D>().velocity.y <= 0F)) {
				maxVelocityReached = false;
				base.GetComponent<UnityEngine.BoxCollider2D>().enabled = true;
			}
		}
	}

}
