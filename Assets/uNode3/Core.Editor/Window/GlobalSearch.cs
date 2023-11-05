using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using System;

namespace MaxyGames.UNode.Editors {
	public class GlobalSearch : EditorWindow {
		private GraphUtility.ReferenceTree referenceTree;
		private SearchFilter searchFilter = SearchFilter.All;
		private SearchField searchField;
		private Vector2 scrollPos;
		private string m_searchText;

		public string searchText {
			get => m_searchText;
			set => m_searchText = value;
		}

		public enum SearchFilter {
			All,
			TypeAndMembers,
			Type,
			Member,
			Variable,
			Property,
			Function,
			Nodes,
		}

		[MenuItem("Tools/uNode/Global Search", false, 103)]
		public static void ShowWindow() {
			GlobalSearch window = (GlobalSearch)GetWindow(typeof(GlobalSearch));
			window.autoRepaintOnSceneChange = true;
			window.wantsMouseMove = true;
			window.titleContent = new GUIContent("Global Search");
			window.Show();
		}

		private void OnEnable() {
			searchField = new SearchField();
			referenceTree = new GraphUtility.ReferenceTree(new List<object>());
		}

		private void OnGUI() {
			DrawToolbar();
			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
			if(referenceTree != null) {
				referenceTree.OnGUI(GUILayoutUtility.GetRect(0, 100000, 0, 100000));
			}
			EditorGUILayout.EndScrollView();
		}

		void DrawToolbar() {
			using(new GUILayout.HorizontalScope()) {
				var search = searchField.OnToolbarGUI(searchText);
				if(search != searchText) {
					searchText = search;
					SearchChanged();
				}
				var filter = (SearchFilter)EditorGUILayout.EnumPopup(searchFilter, GUILayout.MaxWidth(120));
				if(searchFilter != filter) {
					searchFilter = filter;
					SearchChanged();
				}
			}
		}

		void SearchChanged() {
			var searchText = this.searchText;
			if(string.IsNullOrEmpty(searchText)) {
				referenceTree.references = new List<object>();
				return;
			}
			var searchStrategy = new MemberSearchStrategy() {
				searchFilter = this.searchFilter,
			};

			var references = GraphUtility.SearchReferences(element => {
				try {
					if(element is NodeObject nodeObject && nodeObject.node is BaseEntryNode) {
						return false;
					}
					return searchStrategy.IsMatchSearch(element, searchText);
				}
				catch (Exception ex) {
					Debug.LogException(new GraphException(ex, element));
					return false;
				}
			});
			referenceTree.references = references;
		}

		private class MemberSearchStrategy : SearchStrategy {

			public SearchFilter searchFilter;


			public override bool IsMatchSearch(UGraphElement element, string searchString) {
				if(IsMatchSearch(element, searchString, searchFilter)) {
					return true;
				}
				if(searchFilter == SearchFilter.All || searchFilter == SearchFilter.Nodes) {
					GraphUtility.Analizer.AnalizeObject(element, (obj) => {
						if(obj is MemberData member) {
							return IsMatchSearch(member, searchString, searchFilter);
						}
						return false;
					});
				}
				return false;
			}

			private bool IsMatchSearch(MemberData member, string searchString, SearchFilter filter) {
				switch(filter) {
					case SearchFilter.Variable: {
						if(member.targetType == MemberData.TargetType.uNodeVariable || member.targetType == MemberData.TargetType.uNodeLocalVariable) {
							var reference = member.startItem.GetReferenceValue() as Variable;
							if(reference != null) {
								return IsMatchSearch(reference.name, searchString);
							}
						}
						break;
					}
					case SearchFilter.Property: {
						if(member.targetType == MemberData.TargetType.uNodeProperty) {
							var reference = member.startItem.GetReferenceValue() as Property;
							if(reference != null) {
								return IsMatchSearch(reference.name, searchString);
							}
						}
						break;
					}
					case SearchFilter.Function: {
						if(member.targetType == MemberData.TargetType.uNodeFunction) {
							var reference = member.startItem.GetReferenceValue() as Function;
							if(reference != null) {
								return IsMatchSearch(reference.name, searchString);
							}
						}
						break;
					}
					case SearchFilter.Member: {
						if(member.targetType.IsTargetingReflection()) {
							if(IsMatchSearch(member.name, searchString)) {
								return true;
							}
							var members = member.GetMembers(false);
							if(members != null) {
								foreach(var m in members) {
									if(m != null && m is not Type && IsMatchSearch(m.Name, searchString)) {
										return true;
									}
								}
							}
						}
						break;
					}
					case SearchFilter.Type: {
						if(member.targetType.IsTargetingReflection()) {
							return member.startType != null && IsMatchSearch(member.startName, searchString);
						}
						break;
					}
					case SearchFilter.TypeAndMembers: {
						if(IsMatchSearch(member, searchString, SearchFilter.Variable)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Property)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Function)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Type)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Member)) {
							return true;
						}
						break;
					}
					case SearchFilter.All: {
						if(IsMatchSearch(member, searchString, SearchFilter.Variable)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Property)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Function)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Type)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Member)) {
							return true;
						}
						if(IsMatchSearch(member, searchString, SearchFilter.Nodes)) {
							return true;
						}
						break;
					}
				}
				return false;
			}

			private bool IsMatchSearch(UGraphElement element, string searchString, SearchFilter filter) {
				switch(filter) {
					case SearchFilter.Variable: {
						if(element is Variable) {
							return IsMatchSearch(element.name, searchString);
						}
						break;
					}
					case SearchFilter.Property: {
						if(element is Property) {
							return IsMatchSearch(element.name, searchString);
						}
						break;
					}
					case SearchFilter.Function: {
						if(element is Function) {
							return IsMatchSearch(element.name, searchString);
						}
						break;
					}
					case SearchFilter.Member: {
						if(element is Variable || element is Property || element is Function) {
							return IsMatchSearch(element.name, searchString);
						}
						break;
					}
					case SearchFilter.Type: {
						if(element is Graph graph && graph.graphContainer is IReflectionType) {
							return IsMatchSearch(graph.graphContainer.GetGraphName(), searchString);
						}
						break;
					}
					case SearchFilter.Nodes: {
						if(element is NodeObject nodeObject) {
							return IsMatchSearch(nodeObject.GetTitle(), searchString);
						}
						break;
					}
					case SearchFilter.TypeAndMembers: {
						if(IsMatchSearch(element, searchString, SearchFilter.Variable)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Property)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Function)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Type)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Member)) {
							return true;
						}
						break;
					}
					case SearchFilter.All: {
						if(IsMatchSearch(element, searchString, SearchFilter.Variable)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Property)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Function)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Type)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Member)) {
							return true;
						}
						if(IsMatchSearch(element, searchString, SearchFilter.Nodes)) {
							return true;
						}
						break;
					}
				}
				return false;
			}
		}

		private abstract class SearchStrategy {
			public abstract bool IsMatchSearch(UGraphElement obj, string searchString);

			public virtual bool IsMatchSearch(string str, string searchString) {
				return IsMatchSearch(str, searchString, SearchKind.Contains);
			}

			protected bool IsMatchSearch(string str, string searchString, SearchKind searchKind) {
				if(string.IsNullOrEmpty(searchString))
					return true;
				switch(searchKind) {
					case SearchKind.Contains:
						return str.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0;
					case SearchKind.Endswith:
						return str.EndsWith(searchString, StringComparison.OrdinalIgnoreCase);
					case SearchKind.Startwith:
						return str.StartsWith(searchString, StringComparison.OrdinalIgnoreCase);
					case SearchKind.Equals:
						return str.Equals(searchString, StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
		}
	}
}