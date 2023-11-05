﻿using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace MaxyGames.UNode.Nodes {
	[NodeMenu("Jump", "Break", hasFlowInput = true)]
	public class NodeBreak : Node {
		public FlowInput enter { get; set; }

		protected override void OnRegister() {
			enter = PrimaryFlowInput(nameof(enter), (flow) => {
				flow.jumpStatement = new JumpStatement(nodeObject, JumpStatementType.Break);
			});
		}

		public override void OnGeneratorInitialize() {
			CG.RegisterPort(enter, () => CG.Break());
		}
	}
}