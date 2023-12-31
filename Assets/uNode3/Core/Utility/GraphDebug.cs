﻿using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;
using System.Runtime.CompilerServices;

namespace MaxyGames.UNode {
	public static class GraphDebug {
		#region Classes
		public struct DebugValue {
			public bool isSet;
			public float time;
			public object value;

			public bool isValid => time > 0;
		}
		public class DebugFlow {
			private StateType _state;
			public StateType nodeState {
				get {
					if(customCondition != null) {
						return customCondition();
					}
					return _state;
				}
				set {
					_state = value;
				}
			}
			public float calledTime;
			public float breakpointTimes;
			public bool isTransitionRunning;
			public Func<StateType> customCondition;
			public object nodeValue;
			public bool isValid => calledTime > 0;
		}

		public class DebugMessage {
			public Dictionary<int, DebugMessageData> datas = new Dictionary<int, DebugMessageData>();


			public string GetMessage(Connection connection) {
				if(connection == null || connection.isValid == false) return null;

				var input = connection.Input;
				var output = connection.Output;
				var graph = input.node.graphContainer;

				var graphID = uNodeUtility.GetObjectID(graph as UnityEngine.Object);
				bool isValue = connection is ValueConnection;

				if(datas.TryGetValue(graphID, out var messageData)) {
					if(isValue) {
						if(messageData.valueMessages.TryGetValue(input.node.id, out var map)) {
							if(map.TryGetValue(input.id, out var message)) {
								return message;
							}
						}
					}
					else {
						if(messageData.flowMessages.TryGetValue(output.node.id, out var map)) {
							if(map.TryGetValue(output.id, out var message)) {
								return message;
							}
						}
					}
				}
				return null;
			}

			public bool HasMessage(Connection connection) {
				return GetMessage(connection) != null;
			}

			public void SetMessage(Connection connection, string message) {
				if(connection == null || connection.isValid == false) return;

				var input = connection.Input;
				var output = connection.Output;
				var graph = input.node.graphContainer;

				var graphID = uNodeUtility.GetObjectID(graph as UnityEngine.Object);
				bool isValue = connection is ValueConnection;

				if(datas.TryGetValue(graphID, out var messageData) == false) {
					datas[graphID] = messageData = new DebugMessageData();
				}
				if(isValue) {
					if(messageData.valueMessages.TryGetValue(input.node.id, out var map) == false) {
						if(message == null) return;
						messageData.valueMessages[input.node.id] = map = new Dictionary<string, string>();
					}
					if(message == null) {
						map.Remove(input.id);
					}
					else {
						map[input.id] = message;
					}
				}
				else {
					if(messageData.flowMessages.TryGetValue(output.node.id, out var map) == false) {
						if(message == null) return;
						messageData.flowMessages[output.node.id] = map = new Dictionary<string, string>();
					}
					if(message == null) {
						map.Remove(output.id);
					}
					else {
						map[output.id] = message;
					}
				}
			}

			private void Update() {
				try {
					foreach(var (id, message) in datas) {
						if(debugData.TryGetValue(id, out var map)) {
							foreach(var (_, data) in map) {
								data.messageData = message;
							}
						}
					}
				}
				catch { }
			}

			public void Save() {
#if UNITY_EDITOR
				var bytes = SerializerUtility.Serialize(debugMessage);
				UnityEditor.SessionState.SetString("UNODE_DATA_DEBUG_MESSAGE", Convert.ToBase64String(bytes));
#endif
				Update();
			}
		}

		public class DebugMessageData {
			public Dictionary<int, Dictionary<string, string>> flowMessages = new Dictionary<int, Dictionary<string, string>>();
			public Dictionary<int, Dictionary<string, string>> valueMessages = new Dictionary<int, Dictionary<string, string>>();
		}

		static DebugMessage _debugMessage;
		internal static DebugMessage debugMessage {
			get {
				if(_debugMessage == null) {
#if UNITY_EDITOR
					var str = UnityEditor.SessionState.GetString("UNODE_DATA_DEBUG_MESSAGE", string.Empty);
					if(!string.IsNullOrEmpty(str)) {
						var bytes = Convert.FromBase64String(str);
						_debugMessage = SerializerUtility.Deserialize<DebugMessage>(bytes);
					}
#endif
					if(_debugMessage == null)
						_debugMessage = new DebugMessage();
				}
				return _debugMessage;
			}
		}

		/// <summary>
		/// Class that contains Debug data
		/// </summary>
		public class DebugData {
			public DebugMessageData messageData;

			public Dictionary<int, DebugFlow> nodeDebug = new Dictionary<int, DebugFlow>();
			public Dictionary<int, Dictionary<string, DebugFlow>> flowDebug = new Dictionary<int, Dictionary<string, DebugFlow>>();

			public Dictionary<string, DebugValue> flowConnectionDebug = new Dictionary<string, DebugValue>();
			public Dictionary<string, DebugValue> valueConnectionDebug = new Dictionary<string, DebugValue>();

			public DebugValue GetDebugValue(FlowOutput port) {
				if(port.isAssigned) {
					flowConnectionDebug.TryGetValue(port.node.id + port.id, out var value);
					return value;
				}
				return default;
			}

			public DebugFlow GetDebugValue(FlowInput port) {
				if(port.isPrimaryPort) {
					nodeDebug.TryGetValue(port.node.id, out var result);
					return result;
				} else {
					flowDebug.TryGetValue(port.node.id, out var map);
					if(map != null && map.TryGetValue(port.id, out var result)) {
						return result;
					}
				}
				return default;
			}

			public DebugValue GetDebugValue(ValueInput port) {
				if(port.isAssigned) {
					valueConnectionDebug.TryGetValue(port.node.id + port.id, out var value);
					return value;
				}
				return default;
			}
		}
		#endregion

		public static Dictionary<int, ConditionalWeakTable<object, DebugData>> debugData = new Dictionary<int, ConditionalWeakTable<object, DebugData>>();
		/// <summary>
		/// Are debug mode is on.
		/// </summary>
		public static bool useDebug = true;
		/// <summary>
		/// The timer for debug.
		/// </summary>
		public static float debugLinesTimer;

		/// <summary>
		/// The time since startup, same as Time.realtimeSinceStartup the different is that this is thread safe allow us to get time on any threads.
		/// Note: only work inside unity editor.
		/// </summary>
		public static float debugTime;
		/// <summary>
		/// The callback for HasBreakpoint, this will filled from uNodeEditorInitializer.
		/// </summary>
		public static Func<int, int, bool> hasBreakpoint;
		/// <summary>
		/// The callback for AddBreakpoint, this will filled from uNodeEditorInitializer.
		/// </summary>
		public static Action<int, int> addBreakpoint;
		/// <summary>
		/// The callback for RemoveBreakpoint, this will filled from uNodeEditorInitializer.
		/// </summary>
		public static Action<int, int> removeBreakpoint;

		private static int m_lastDebugID;
		private static ConditionalWeakTable<object, string> m_debugIDs = new ConditionalWeakTable<object, string>();

		internal static string GetDebugID(object obj) {
			if(obj is UnityEngine.Object) {
				return obj.GetHashCode().ToString();
			}
			else {
				if(!m_debugIDs.TryGetValue(obj, out var result)) {
					result = "@" + (++m_lastDebugID);
					m_debugIDs.AddOrUpdate(obj, result);
				}
				return result;
			}
		}

		internal static object GetDebugObject(string id) {
			if(string.IsNullOrEmpty(id))
				return null;
			if(id[0] == '@') {
				foreach(var (obj, debugID) in m_debugIDs) {
					if(id == debugID) {
						return obj;
					}
				}
			}
#if UNITY_EDITOR
			else if(int.TryParse(id, out var result)) {
				return UnityEditor.EditorUtility.InstanceIDToObject(result);
			}
#endif
			return null;
		}

		/// <summary>
		/// Are the node has breakpoint.
		/// </summary>
		/// <param name="graphID"></param>
		/// <param name="nodeID"></param>
		/// <returns></returns>
		public static bool HasBreakpoint(int graphID, int nodeID) {
			if(hasBreakpoint == null) {
#if UNITY_EDITOR
				throw new Exception("uNode is not initialized");
#else
				return false;
#endif
			}
			return hasBreakpoint(graphID, nodeID);
		}

		public static bool HasBreakpoint(NodeObject node) {
			if(node != null) {
				return HasBreakpoint(node.graphContainer.GetGraphID(), node.id);
			}
			return false;
		}

		/// <summary>
		/// Add breakpoint to node.
		/// </summary>
		/// <param name="graphID"></param>
		/// <param name="nodeID"></param>
		public static void AddBreakpoint(int graphID, int nodeID) {
			if(addBreakpoint == null) {
#if UNITY_EDITOR
				throw new Exception("uNode is not initialized");
#else
				return;
#endif
			}
			addBreakpoint(graphID, nodeID);
		}

		/// <summary>
		/// Remove breakpoint from node.
		/// </summary>
		/// <param name="graphID"></param>
		/// <param name="nodeID"></param>
		public static void RemoveBreakpoint(int graphID, int nodeID) {
			if(removeBreakpoint == null) {
#if UNITY_EDITOR
				throw new Exception("uNode is not initialized");
#else
				return;
#endif
			}
			removeBreakpoint(graphID, nodeID);
		}

		/// <summary>
		/// Call this function to debug EventNode that using value node.
		/// </summary>
		public static T Value<T>(T value, object owner, int objectUID, int nodeUID, string portID, bool isSet = false) {
			if(!useDebug || uNodeUtility.isPlaying == false)
				return value;
			if(owner is ValueType) {
				//If the owner of value is struct then we use the type instead
				owner = owner.GetType();
			}
			if(!debugData.TryGetValue(objectUID, out var debugMap)) {
				debugMap = new ConditionalWeakTable<object, DebugData>();
				debugData[objectUID] = debugMap;
			}
			if(!debugMap.TryGetValue(owner, out var data)) {
				data = new DebugData();
				debugMap.AddOrUpdate(owner, data);

				if(debugMessage.datas.TryGetValue(objectUID, out var messageData)) {
					data.messageData = messageData;
				}
			}
			var id = nodeUID + portID;
			data.valueConnectionDebug[id] = new DebugValue() {
				time = debugTime,
				value = value,
				isSet = isSet,
			};
			if(data.messageData != null) {
				if(data.messageData.valueMessages.TryGetValue(nodeUID, out var map)) {
					if(map.TryGetValue(portID, out var message)) {
						var graphID = objectUID;
						var valueMessage = value == null ? "null" : value.ToString();
						if(string.IsNullOrEmpty(message)) {
							Debug.Log(GraphException.GetMessage(valueMessage, graphID, nodeUID, owner));
						}
						else {
							Debug.Log(GraphException.GetMessage(message + ": " + valueMessage, graphID, nodeUID, owner));
						}
					}
				}
			}
			return value;
		}

		public static void Flow(object owner, int objectUID, int nodeUID, string portID) {
			if(!useDebug || uNodeUtility.isPlaying == false)
				return;
			if(owner is ValueType) {
				//If the owner of value is struct then we use the type instead
				owner = owner.GetType();
			}
			if(!debugData.TryGetValue(objectUID, out var debugMap)) {
				debugMap = new ConditionalWeakTable<object, DebugData>();
				debugData[objectUID] = debugMap;
			}
			if(!debugMap.TryGetValue(owner, out var data)) {
				data = new DebugData();
				debugMap.AddOrUpdate(owner, data);

				if(debugMessage.datas.TryGetValue(objectUID, out var messageData)) {
					data.messageData = messageData;
				}
			}
			var id = nodeUID + portID;
			data.flowConnectionDebug[id] = new DebugValue() {
				time = debugTime,
			};
			if(data.messageData != null) {
				if(data.messageData.flowMessages.TryGetValue(nodeUID, out var map)) {
					if(map.TryGetValue(portID, out var message)) {
						var graphID = objectUID;
						Debug.Log(GraphException.GetMessage(message, graphID, nodeUID, owner));
					}
				}
			}
		}

		/// <summary>
		/// Call this function to debug the flow port.
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="objectUID"></param>
		/// <param name="nodeUID"></param>
		/// <param name="state">true : success, false : failure, null : running</param>
		public static void FlowNode(object owner, int objectUID, int nodeUID, bool? state) {
			FlowNode(owner, objectUID, nodeUID, null, state);
		}

		/// <summary>
		/// Call this function to debug the flow port.
		/// </summary>
		/// <param name="owner"></param>
		/// <param name="objectUID"></param>
		/// <param name="nodeUID"></param>
		/// <param name="state">true : success, false : failure, null : running</param>
		public static void FlowNode(object owner, int objectUID, int nodeUID, string portID, bool? state) {
			if(!useDebug || uNodeUtility.isPlaying == false)
				return;
			if(owner is ValueType) {
				//If the owner of value is struct then we use the type instead
				owner = owner.GetType();
			}
			var s = state == null ? StateType.Running : (state.Value ? StateType.Success : StateType.Failure);
			if(!debugData.TryGetValue(objectUID, out var debugMap)) {
				debugMap = new ConditionalWeakTable<object, DebugData>();
				debugData[objectUID] = debugMap;
			}
			if(!debugMap.TryGetValue(owner, out var data)) {
				data = new DebugData();
				debugMap.AddOrUpdate(owner, data);
			}
			if(portID == null) {
				if(!data.nodeDebug.TryGetValue(nodeUID, out var nodeDebug)) {
					nodeDebug = new DebugFlow();
					data.nodeDebug[nodeUID] = nodeDebug;
				}
				nodeDebug.calledTime = debugTime;
				nodeDebug.nodeState = s;
				if(HasBreakpoint(objectUID, nodeUID)) {
					nodeDebug.breakpointTimes = debugTime;
					Debug.Break();
				}
			} else {
				if(!data.flowDebug.TryGetValue(nodeUID, out var flowData)) {
					flowData = new Dictionary<string, DebugFlow>();
					data.flowDebug[nodeUID] = flowData;
				}
				if(!flowData.TryGetValue(portID, out var nodeDebug)) {
					nodeDebug = new DebugFlow();
					flowData[portID] = nodeDebug;
				}
				nodeDebug.calledTime = debugTime;
				nodeDebug.nodeState = s;
				if(HasBreakpoint(objectUID, nodeUID)) {
					nodeDebug.breakpointTimes = debugTime;
					Debug.Break();
				}
			}
		}
	}
}