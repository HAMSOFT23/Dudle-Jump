using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace MaxyGames.UNode.Editors.Analyzer {
	class DefaultGraphAnalyzer : GraphAnalyzer {
		public override int order => int.MinValue;

		public override bool IsValidAnalyzerForGraph(Type graphType) => true;

		public override bool IsValidAnalyzerForNode(Type nodeType) => true;

		public override bool IsValidAnalyzerForElement(Type elementType) => true;

		public override void CheckGraphErrors(ErrorAnalyzer analyzer, IGraph graph) {
			var graphData = graph.GraphData;
			if(graphData == null) return;
			if(graph is IUsingNamespace) {
				var namespaces = EditorReflectionUtility.GetNamespaces();
				foreach(var ns in graph.GetUsingNamespaces()) {
					if(!namespaces.Contains(ns)) {
						if(graph is INamespace nsGraph && nsGraph.Namespace != ns) {
							analyzer.RegisterError(graphData, $@"Using Namespace: '{ns}' was not found.");
						}
					}
				}
			}
			else if(graph is IScriptGraphType scriptGraphType) {
				var namespaces = EditorReflectionUtility.GetNamespaces();
				foreach(var ns in graph.GetUsingNamespaces()) {
					if(!namespaces.Contains(ns)) {
						if(scriptGraphType.ScriptTypeData.scriptGraph.Namespace != ns) {
							analyzer.RegisterError(graphData, $@"Using Namespace: '{ns}' was not found.");
						}
					}
				}
			}
			if(graph is IClassModifier) {
				var modifier = (graph as IClassModifier).GetModifier();
				if(modifier.ReadOnly) {
					if(graph is IClassGraph classGraph) {
						if(classGraph.IsStruct == false) {
							analyzer.RegisterError(graphData, $@"Readonly modifier is only supported for struct.");
						}
						else {
							foreach(var variable in classGraph.GetVariables()) {
								if(variable.modifier.ReadOnly == false) {
									analyzer.RegisterError(variable, $@"Variable from readonly struct must be readonly.");
								}
							}
						}
					}

				}
			}
			if(graph is IInterfaceSystem) {
				var ifaceSystem = graph as IInterfaceSystem;
				var ifaces = ifaceSystem.Interfaces;
				if(ifaces != null) {
					foreach(var iface in ifaces) {
						if(!iface.isAssigned)
							continue;
						var type = iface.type;
						if(type == null)
							continue;
						var methods = type.GetMethods();
						for(int i=0;i< methods.Length;i++) {
							var member = methods[i];
							if(member.Name.StartsWith("get_", StringComparison.Ordinal) || member.Name.StartsWith("set_", StringComparison.Ordinal)) {
								continue;
							}
							if(!graph.GetFunction(
								member.Name,
								member.GetGenericArguments().Length,
								member.GetParameters().Select(item => item.ParameterType).ToArray())) {
								analyzer.RegisterError(graphData, 
									$@"The graph does not implement interface method: '{type.PrettyName()}' type: '{EditorReflectionUtility.GetPrettyMethodName(member)}'",
									() => {
										NodeEditorUtility.AddNewFunction(graph.GraphData.functionContainer, member.Name, member.ReturnType,
										member.GetParameters().Select(item => item.Name).ToArray(),
										member.GetParameters().Select(item => item.ParameterType).ToArray(),
										member.GetGenericArguments().Select(item => item.Name).ToArray());
										uNodeGUIUtility.GUIChanged(graph, UIChangeType.Important);
									});
							}
						}
						var properties = type.GetProperties();
						for(int i=0;i< properties.Length;i++) {
							var member = properties[i];
							if(!graph.GetPropertyData(member.Name)) {
								analyzer.RegisterError(graphData,
									$@"The graph does not implement interface property: '{type.PrettyName()}' type: '{member.PropertyType.PrettyName()}'",
									() => {
										NodeEditorUtility.AddNewProperty(graph.GraphData.propertyContainer, member.Name, (val) => {
											val.type = member.PropertyType;
										});
										uNodeGUIUtility.GUIChanged(graph, UIChangeType.Important);
									});
							}
						}
					}
				}
			}
		}

		public override void CheckElementErrors(ErrorAnalyzer analizer, UGraphElement element) {
			if(element is IErrorCheck errorCheck) {
				errorCheck.CheckError(analizer);
			}
		}

		public override void CheckNodeErrors(ErrorAnalyzer analizer, Node node) {
			node.CheckError(analizer);
		}
	}
}