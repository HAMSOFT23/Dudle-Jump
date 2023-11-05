#pragma warning disable
using UnityEngine;
using System.Collections.Generic;

namespace test {
	public class uTest : MaxyGames.UNode.RuntimeBehaviour {	
		public float playerSpeed = 2.8F;
		public float playerJumpForce = 6F;
		public bool maxVelocityReached;
		
		/// <summary>
		/// Update frame by frame
		/// </summary>
		private void Update() {
			this.GetComponent<Rigidbody2D>().velocity = new Vector2((Input.GetAxisRaw("Horizontal") * playerSpeed), this.GetComponent<Rigidbody2D>().velocity.y);
		}
		
		private void OnCollisionEnter2D(Collision2D collisionInfo) {
			if(collisionInfo.collider.gameObject.CompareTag("Platform")) {
				Debug.Log("Collision Detected");
				this.GetComponent<Rigidbody2D>().velocity = new Vector2(this.GetComponent<Rigidbody2D>().velocity.x, playerJumpForce);
			}
		}
		
		private void FixedUpdate() {
			if((this.GetComponent<Rigidbody2D>().velocity.y >= 0F)) {
				Debug.Log("Max Velocity Y Reched ");
				maxVelocityReached = true;
				base.GetComponent<System.Object>();
			}
		}
	}

}
