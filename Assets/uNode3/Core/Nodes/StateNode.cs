﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace MaxyGames.UNode.Nodes {
	//[NodeMenu("Flow", "State", IsCoroutine = true, order = 1, HideOnFlow = true)]
	public class StateNode : BaseCoroutineNode, ISuperNode, IGraphEventHandler {
		[HideInInspector]
		public TransitionData transitions = new TransitionData();

		public bool CanTrigger(GraphInstance instance) {
			var flow = instance.GetStateData(enter);
			return flow.state == StateType.Running;
		}

		public IEnumerable<NodeObject> nestedFlowNodes => nodeObject.GetObjectsInChildren<NodeObject>(obj => obj.node is BaseEventNode);

		public IEnumerable<TransitionEvent> GetTransitions() {
			return transitions.GetNodes<TransitionEvent>();
		}

		public event System.Action<Flow> onEnter;
		public event System.Action<Flow> onExit;

		protected override void OnRegister() {
			base.OnRegister();
			transitions.Register(this);
			//enter.Next(new RuntimeFlow(OnExit));
			enter.actionOnExit = OnExit;
			enter.actionOnStopped = OnExit;
		}

		protected override System.Collections.IEnumerator OnExecutedCoroutine(Flow flow) {
			if(onEnter != null) {
				onEnter(flow);
			}
			foreach(var tr in GetTransitions()) {
				tr.OnEnter(flow);
			}
			while(flow.state == StateType.Running) {
				foreach(var tr in GetTransitions()) {
					tr.OnUpdate(flow);
					if(flow.state != StateType.Running) {
						yield break;
					}
				}
				yield return null;
			}
		}

		public void OnExit(Flow flow) {
			if(onExit != null) {
				onExit(flow);
			}
			foreach(BaseEventNode node in nestedFlowNodes) {
				node.Stop(flow.instance);
			}
			foreach(var tr in GetTransitions()) {
				tr.OnExit(flow);
			}
		}

		public override void OnGeneratorInitialize() {
			//Register this node as state node.
			CG.RegisterAsStateFlow(enter);
			var transitions = GetTransitions().ToArray();
			for(int i = 0; i < transitions.Length; i++) {
				TransitionEvent transition = transitions[i];
				transition?.OnGeneratorInitialize();
			}
			CG.SetStateInitialization(enter, () => CG.GeneratePort(enter));
			foreach(BaseEventNode node in nestedFlowNodes) {
				foreach(var flow in node.outputs) {
					var targetFlow = flow.GetTargetFlow();
					if(targetFlow != null) {
						CG.RegisterAsStateFlow(targetFlow);
					}
				}
			}
			CG.RegisterPort(enter, () => {
				string onEnter = null;
				string onUpdate = null;
				string onExit = null;
				nodeObject.ForeachInChildrens(element => {
					if(element is NodeObject nodeObject) {
						foreach(var flow in nodeObject.FlowInputs) {
							if(/*flow.IsSelfCoroutine() &&*/ CG.IsStateFlow(flow)) {
								onExit += CG.StopEvent(flow).AddLineInFirst();
							}
						}
					}
				});
				//onExit += CG.Flow(nestedFlowNodes.Select(n => (n.node as BaseGraphEvent).GenerateStopFlows()).ToArray());
				for(int i = 0; i < transitions.Length; i++) {
					TransitionEvent transition = transitions[i];
					if(transition != null) {
						onEnter += transition.GenerateOnEnterCode().Add("\n", !string.IsNullOrEmpty(onEnter));
						onUpdate += transition.GenerateOnUpdateCode().AddLineInFirst();
						onExit += transition.GenerateOnExitCode().AddLineInFirst();
					}
				}
				foreach(var evt in nodeObject.GetNodesInChildren<BaseGraphEvent>()) {
					if(evt != null) {
						if(evt is StateOnEnterEvent) {
							onEnter += evt.GenerateFlows().AddLineInFirst().Replace("yield ", "");
						} else if(evt is StateOnExitEvent) {
							onExit += evt.GenerateFlows().AddLineInFirst();
						} else {
							CG.SetStateInitialization(evt, CG.Routine(CG.LambdaForEvent(evt.GenerateFlows())));
						}
					}
				}
				CG.SetStateStopAction(enter, onExit);
				return CG.Routine(
					CG.Routine(CG.Lambda(onEnter)),
					CG.Invoke(typeof(Runtime.Routine), nameof(Runtime.Routine.WaitWhile), CG.Lambda(onUpdate.AddLineInEnd() + CG.Return(CG.CompareNodeState(enter, null))))
				);
			});
		}

		protected override bool IsSelfCoroutine() {
			return true;
		}

		public override string GetTitle() {
			return name;
		}

		public override Type GetNodeIcon() {
			return typeof(TypeIcons.StateIcon);
		}

		public bool AllowCoroutine() {
			return true;
		}
	}
}

namespace MaxyGames.UNode {
	[Serializable]
	public class TransitionData : BaseNodeContainerData<TransitionContainer> {
		protected override bool IsValidNode(NodeObject node) {
			return node.node is TransitionEvent;
		}
	}

	public class TransitionContainer : UGraphElement {

	}
}