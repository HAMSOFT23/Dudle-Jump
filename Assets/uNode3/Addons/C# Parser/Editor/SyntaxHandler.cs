using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using MaxyGames.UNode.Nodes;

namespace MaxyGames.UNode.Editors {
	public abstract class SyntaxHandles {
		public virtual int order => 0;

		public virtual bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			value = null;
			return false;
		}

		public virtual bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			result = null;
			return false;
		}
	}

	public class IfStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is IfStatementSyntax && data.CanCreateNode) {
				var ex = syntax as IfStatementSyntax;
				var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<NodeIf>("If", data.parent);
				var cond = CSharpParser.ParseExpression(ex.Condition, new ParserSetting(data));
				if(cond != null && !cond.targetType.HasFlags(MemberData.TargetType.NodePort)) {
					cond = CSharpParser.ToValueNode(cond, null, new ParserSetting(data));
				}
				n.condition.ConnectTo(cond);
				var nodes = CSharpParser.ParseStatement(ex.Statement, data.model, data.parent);
				if(nodes != null && nodes.Count > 0) {
					n.onTrue.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
				}
				if(ex.Else != null) {
					nodes = CSharpParser.ParseStatement(ex.Else.Statement, data.model, data.parent);
					if(nodes != null && nodes.Count > 0) {
						n.onFalse.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
					}
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class ForStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is ForStatementSyntax && data.CanCreateNode) {
				var ex = syntax as ForStatementSyntax;
				//var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<ForNumberLoop>("for", data.parent);
				NodeSetValue nodeSet = null;
				string varName = null;
				if(ex.Declaration == null) {
					var initializer = ex.Initializers.First();
					if(initializer is AssignmentExpressionSyntax && initializer.Kind() == SyntaxKind.SimpleAssignmentExpression) {
						var assigment = initializer as AssignmentExpressionSyntax;
						var left = CSharpParser.ParseExpression(assigment.Left, new ParserSetting(data));
						var right = CSharpParser.ParseExpression(assigment.Right, new ParserSetting(data));
						n.indexType = right.type;
						n.Register();
						n.start.ConnectTo(right);
						nodeSet = CSharpParser.CreateComponent<NodeSetValue>("Set", data.parent);
						nodeSet.target.ConnectTo(left);
						nodeSet.value.ConnectTo(n.index);
						n.body.ConnectTo(nodeSet.enter);
					}
				}
				else {
					var variable = ex.Declaration.Variables.First();
					varName = variable.Identifier.ValueText;
					n.indexType = CSharpParser.GetMemberFromExpression(ex.Declaration.Type, data.model, data.parent).startType;
					n.Register();
					n.start.ConnectTo(CSharpParser.GetMemberFromExpression(variable.Initializer.Value, data.model, data.parent));
				}
				if(ex.Condition is BinaryExpressionSyntax) {
					BinaryExpressionSyntax binary = ex.Condition as BinaryExpressionSyntax;
					MemberData left = null;
					MemberData right = null;
					if(binary.Left is IdentifierNameSyntax && (binary.Left as IdentifierNameSyntax).Identifier.ValueText.Equals(varName)) {

					}
					else {
						CSharpParser.TryParseExpression(binary.Left, data.model, data.parent, out left);
					}
					if(binary.Right is IdentifierNameSyntax && (binary.Right as IdentifierNameSyntax).Identifier.ValueText.Equals(varName)) {

					}
					else {
						CSharpParser.TryParseExpression(binary.Right, data.model, data.parent, out right);
					}
					MemberData incrementVal = null;
					var increment = ex.Incrementors.First();
					SetType incrementType = default(SetType);
					if(increment.Kind() == SyntaxKind.PostIncrementExpression || increment.Kind() == SyntaxKind.PreIncrementExpression) {
						incrementVal = new MemberData(1);
						incrementType = SetType.Add;
					}
					else if(increment.Kind() == SyntaxKind.PostDecrementExpression || increment.Kind() == SyntaxKind.PreDecrementExpression) {
						incrementVal = new MemberData(1);
						incrementType = SetType.Subtract;
					}
					else if(increment is AssignmentExpressionSyntax) {
						if(increment.Kind() == SyntaxKind.SimpleAssignmentExpression) {
							incrementType = SetType.Change;
						}
						else if(increment.Kind() == SyntaxKind.AddAssignmentExpression) {
							incrementType = SetType.Add;
						}
						else if(increment.Kind() == SyntaxKind.SubtractAssignmentExpression) {
							incrementType = SetType.Subtract;
						}
						else if(increment.Kind() == SyntaxKind.DivideAssignmentExpression) {
							incrementType = SetType.Divide;
						}
						else if(increment.Kind() == SyntaxKind.MultiplyAssignmentExpression) {
							incrementType = SetType.Multiply;
						}
						else if(increment.Kind() == SyntaxKind.ModuloAssignmentExpression) {
							incrementType = SetType.Modulo;
						}
						var exp = increment as AssignmentExpressionSyntax;
						if(!(exp.Left is IdentifierNameSyntax)) {
							return base.StatementHandler(syntax, data, out result);
						}
						MemberData rVal;
						CSharpParser.TryParseExpression(exp.Right, data.model, data.parent, out rVal);
						if(rVal != null) {
							incrementVal = rVal;
						}
					}
					else {
						return base.StatementHandler(syntax, data, out result);
					}
					if(incrementVal != null && (left != null || right != null)) {
						ComparisonType operatorType = default(ComparisonType);
						if(binary.Kind() == SyntaxKind.EqualsExpression) {
							operatorType = ComparisonType.Equal;
						}
						else if(binary.Kind() == SyntaxKind.GreaterThanExpression) {
							if(right == null) {
								operatorType = ComparisonType.LessThan;
							}
							else {
								operatorType = ComparisonType.GreaterThan;
							}
						}
						else if(binary.Kind() == SyntaxKind.GreaterThanOrEqualExpression) {
							if(right == null) {
								operatorType = ComparisonType.LessThanOrEqual;
							}
							else {
								operatorType = ComparisonType.GreaterThanOrEqual;
							}
						}
						else if(binary.Kind() == SyntaxKind.LessThanExpression) {
							if(right == null) {
								operatorType = ComparisonType.GreaterThan;
							}
							else {
								operatorType = ComparisonType.LessThan;
							}
						}
						else if(binary.Kind() == SyntaxKind.LessThanOrEqualExpression) {
							if(right == null) {
								operatorType = ComparisonType.GreaterThanOrEqual;
							}
							else {
								operatorType = ComparisonType.LessThanOrEqual;
							}
						}
						else if(binary.Kind() == SyntaxKind.NotEqualsExpression) {
							operatorType = ComparisonType.NotEqual;
						}
						else {
							throw new System.Exception("Couldn't handle expression type:" + binary.Kind().ToString());
						}
						n.compareType = operatorType;
						n.count.ConnectTo(left ?? right);
						n.iteratorSetType = incrementType;
						n.step.ConnectTo(incrementVal);
					}
					else {
						return base.StatementHandler(syntax, data, out result);
					}
				}
				CSharpParser.DefineStatementNode(syntax, n);
				CSharpParser.RegisterAutoProxyConnectionForPort(n.index);
				if(ex.Declaration != null) {
					foreach(var locVar in ex.Declaration.Variables) {
						CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(locVar, data.model), n, _ => CSharpParser.CreateFromPort(n.index));
					}
				}
				var nodes = CSharpParser.ParseStatement(ex.Statement, data.model, data.parent);
				if(nodes != null && nodes.Count > 0) {
					if(nodeSet != null) {
						nodeSet.exit.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
					}
					else {
						n.body.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
					}
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}

		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is NameSyntax) {
				var symbol = CSharpParser.GetSymbol(syntax, data.model) as ILocalSymbol;
				if(symbol != null && CSharpParser.IsInSource(symbol.Locations)) {
					ForNumberLoop node = CSharpParser.GetSymbolOwner(symbol) as ForNumberLoop;
					if(node != null) {
						value = CSharpParser.CreateFromPort(node.index);
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class DoStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is DoStatementSyntax && data.CanCreateNode) {
				var ex = syntax as DoStatementSyntax;
				var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<DoWhileLoop>("do-while", data.parent);
				n.condition.ConnectTo(CSharpParser.GetMemberFromExpression(ex.Condition, data.model, data.parent));
				var nodes = CSharpParser.ParseStatement(ex.Statement, data.model, data.parent);
				if(nodes != null && nodes.Count > 0) {
					n.body.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class WhileStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is WhileStatementSyntax && data.CanCreateNode) {
				var ex = syntax as WhileStatementSyntax;
				var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<WhileLoop>("while", data.parent);
				n.condition.ConnectTo(CSharpParser.GetMemberFromExpression(ex.Condition, data.model, data.parent));
				var nodes = CSharpParser.ParseStatement(ex.Statement, data.model, data.parent);
				if(nodes != null && nodes.Count > 0) {
					n.body.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class ForeachStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is ForEachStatementSyntax && data.CanCreateNode) {
				var ex = syntax as ForEachStatementSyntax;
				var n = CSharpParser.CreateComponent<ForeachLoop>("foreach", data.parent);
				n.deconstructValue = false;
				n.collection.ConnectTo(CSharpParser.GetMemberFromExpression(ex.Expression, data.model, data.parent));
				if(n.collection.isAssigned && n.collection.UseDefaultValue) {
					n.collection.ConnectTo(CSharpParser.ToValueNode(n.collection.defaultValue, null, new ParserSetting(data)));
				}
				CacheNode cacheNode = null;

				var typeSymbol = CSharpParser.GetTypeSymbol(ex.Type, data.model);
				if(typeSymbol != null) {
					var elementType = CSharpParser.ParseType(typeSymbol)?.type;
					if(elementType != null && elementType != typeof(object)) {
						var collectionTypeSymbol = CSharpParser.GetTypeSymbol(ex.Expression, data.model);
						if(collectionTypeSymbol != null) {
							var collectionType = CSharpParser.ParseType(collectionTypeSymbol)?.type;
							if(collectionType != null && collectionType.ElementType() != elementType) {
								var local = CSharpParser.CreateComponent<CacheNode>("localVar", data.parent);
								var convert = CSharpParser.CreateComponent<NodeConvert>("convert", data.parent);
								convert.type = elementType;
								convert.target.ConnectToAsProxy(n.output);
								local.target.ConnectTo(convert.output);
								n.exit.ConnectTo(local.enter);

								CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(ex, data.model), n, _ => CSharpParser.CreateFromPort(local.output));
								CSharpParser.RegisterAutoProxyConnectionForPort(local.output);
								cacheNode = local;
							}
						}
					}
				}

				CSharpParser.DefineStatementNode(syntax, n);
				if(cacheNode == null) {
					CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(ex, data.model), n, _ => CSharpParser.CreateFromPort(n.output));
					CSharpParser.RegisterAutoProxyConnectionForPort(n.output);
				}
				var nodes = CSharpParser.ParseStatement(ex.Statement, data.model, data.parent);
				if(cacheNode != null) {
					n.body.ConnectTo(cacheNode.enter);
					if(nodes != null && nodes.Count > 0) {
						cacheNode.exit.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
					}
				}
				else {
					if(nodes != null && nodes.Count > 0) {
						n.body.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
					}
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}

		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is NameSyntax) {
				var symbol = CSharpParser.GetSymbol(syntax, data.model) as ILocalSymbol;
				if(symbol != null && CSharpParser.IsInSource(symbol.Locations)) {
					value = CSharpParser.GetSymbolReferenceValue(symbol, new ParserSetting(data));
						return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class UsingStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is UsingStatementSyntax && data.CanCreateNode) {
				var ex = syntax as UsingStatementSyntax;
				var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<NodeUsing>("using", data.parent);
				CSharpParser.DefineStatementNode(syntax, n);
				if(ex.Declaration?.Variables.Count > 0) {
					var dec = ex.Declaration.Variables.First();
					n.target.ConnectTo(CSharpParser.GetMemberFromExpression(dec.Initializer.Value, data.model, data.parent));
					CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(dec, data.model), n, _ => CSharpParser.CreateFromPort(n.output));
					CSharpParser.RegisterAutoProxyConnectionForPort(n.output);
				}
				else {
					n.target.ConnectTo(CSharpParser.GetMemberFromExpression(ex.Expression, data.model, data.parent));
				}
				var nodes = CSharpParser.ParseStatement(ex.Statement, data.model, data.parent);
				if(nodes != null && nodes.Count > 0) {
					n.body.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}

		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is NameSyntax) {
				var symbol = CSharpParser.GetSymbol(syntax, data.model) as ILocalSymbol;
				if(symbol != null && CSharpParser.IsInSource(symbol.Locations)) {
					NodeUsing node = CSharpParser.GetSymbolOwner(symbol) as NodeUsing;
					if(node != null) {
						value = CSharpParser.CreateFromPort(node.output);
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class SwitchStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is SwitchStatementSyntax && data.CanCreateNode) {
				var ex = syntax as SwitchStatementSyntax;
				var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<NodeSwitch>("switch", data.parent);
				n.target.ConnectTo(CSharpParser.GetMemberFromExpression(ex.Expression, data.model, data.parent));
				List<Node> flows = new List<Node>();
				List<NodeSwitch.Data> datas = new List<NodeSwitch.Data>();
				foreach(var section in ex.Sections) {
					var nodes = CSharpParser.ParseStatement(section.Statements, data.model, data.parent);
					foreach(var label in section.Labels) {
						if(label is DefaultSwitchLabelSyntax) {
							if(nodes != null && nodes.Count > 0) {
								n.defaultTarget.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
							}
						}
						else if(label is CaseSwitchLabelSyntax) {
							var caseData = new NodeSwitch.Data();
							caseData.value = CSharpParser.ParseExpression((label as CaseSwitchLabelSyntax).Value, data.model, data.parent);
							if(nodes != null && nodes.Count > 0) {
								flows.Add(nodes[0]);
								datas.Add(caseData);
							}
						}
					}
				}
				n.datas = datas;
				n.Register();
				for(int i = 0; i < flows.Count; i++) {
					n.datas[i].flow.ConnectTo(flows[i].nodeObject.primaryFlowInput);
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class TryExpressionHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is TryStatementSyntax && data.CanCreateNode) {
				var ex = syntax as TryStatementSyntax;
				var symbol = data.model.GetSymbolInfo(ex).Symbol;
				var n = CSharpParser.CreateComponent<NodeTry>("Try", data.parent);
				var nodes = CSharpParser.ParseStatement(ex.Block, data.model, data.parent);
				if(nodes != null && nodes.Count > 0) {
					n.Try.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
				}
				if(ex.Catches.Count > 0) {
					var exceptions = new List<NodeTry.Data>();
					System.Action postAction = null;
					foreach(var c in ex.Catches) {
						var exception = new NodeTry.Data();
						if(c.Declaration != null) {
							exception.type = CSharpParser.ParseType(c.Declaration.Type, data.model);
						}
						else {
							exception.type = typeof(System.Exception);
						}
						var block = CSharpParser.ParseStatement(ex.Block, data.model, data.parent);
						if(block != null && nodes.Count > 0) {
							postAction += () => {
								exception.flow.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
							};
						}
						exceptions.Add(exception);
					}
					n.exceptions = exceptions;
					//Register for updates the ports
					n.Register();
					//Execute the post action to fill the ports values
					postAction?.Invoke();
				}
				if(ex.Finally != null && ex.Finally.Block != null) {
					nodes = CSharpParser.ParseStatement(ex.Finally.Block, data.model, data.parent);
					if(nodes != null && nodes.Count > 0) {
						n.Finally.ConnectTo(nodes[0].nodeObject.primaryFlowInput);
					}
				}
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class MemberAccessExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			//if(syntax is MemberAccessExpressionSyntax) {
			//	MemberAccessExpressionSyntax expression = syntax as MemberAccessExpressionSyntax;
			//	if(expression.Name is NameSyntax) {
			//		var nameSymbol = CSharpParser.GetSymbol(expression.Name, data.model);
			//		if(nameSymbol != null && (nameSymbol.Kind == SymbolKind.Field || nameSymbol.Kind == SymbolKind.Event || nameSymbol.Kind == SymbolKind.Local)) {//Handle field invoke.
			//			var result = CSharpParser.ParseExpression(expression.Name, new ParserSetting(data));
			//			if(result != null) {
			//				value = result;
			//				return true;
			//			}
			//		}
			//	}
			//}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class InvokeExpressionHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is ExpressionStatementSyntax && data.CanCreateNode) {
				var ex = syntax as ExpressionStatementSyntax;
				if(ex.Expression is InvocationExpressionSyntax) {
					var expression = ex.Expression as InvocationExpressionSyntax;
					List<MemberData> parameters;
					var member = CSharpParser.GetMemberFromExpression(expression, new ParserSetting(data), out parameters);
					if(member != null) {
						MultipurposeNode n = CSharpParser.CreateMultipurposeNode("invoke", data.parent, member, parameters);
						result = new ParserResult() {
							node = n,
							next = (nod) => {
								n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
							}
						};
						return true;
					}
				}
				else if(ex.Expression is AwaitExpressionSyntax) {
					var expression = ex.Expression as AwaitExpressionSyntax;
					List<MemberData> parameters;
					var member = CSharpParser.GetMemberFromExpression(expression.Expression, new ParserSetting(data), out parameters);
					if(member != null) {
						MultipurposeNode n = CSharpParser.CreateMultipurposeNode("invoke", data.parent, member, parameters);
						var await = CSharpParser.CreateComponent<AwaitNode>("await", data.parent);
						await.value.ConnectTo(n.output);
						result = new ParserResult() {
							node = await,
							next = (nod) => {
								await.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
							}
						};
						return true;
					}
				}
			}
			return base.StatementHandler(syntax, data, out result);
		}

		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is InvocationExpressionSyntax && data.CanCreateNode) {
				var expression = syntax as InvocationExpressionSyntax;
				List<MemberData> parameters;
				var member = CSharpParser.GetMemberFromExpression(expression, new ParserSetting(data), out parameters);
				if(member != null) {
					var node = CSharpParser.CreateMultipurposeNode("invoke", data.parent, member, parameters);
					if(node.output != null) {
						value = CSharpParser.CreateFromPort(node.output);
					}
					else {
						value = CSharpParser.CreateFromPort(node.enter);
					}
					return true;
				}
			}
			else if(syntax is AwaitExpressionSyntax) {
				var expression = syntax as AwaitExpressionSyntax;
				List<MemberData> parameters;
				var member = CSharpParser.GetMemberFromExpression(expression.Expression, new ParserSetting(data), out parameters);
				if(member != null) {
					MultipurposeNode n = CSharpParser.CreateMultipurposeNode("invoke", data.parent, member, parameters);
					var await = CSharpParser.CreateComponent<AwaitNode>("await", data.parent);
					await.value.ConnectTo(n.output);
					value = CSharpParser.CreateFromPort(await.output);
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class AssignmentHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(!(syntax is ExpressionStatementSyntax)) {
				return base.StatementHandler(syntax, data, out result);
			}
			if((syntax as ExpressionStatementSyntax).Expression is AssignmentExpressionSyntax) {
				var expression = (syntax as ExpressionStatementSyntax).Expression as AssignmentExpressionSyntax;
				var left = expression.Left;
				var right = expression.Right;

				if(left is ElementAccessExpressionSyntax && expression.Kind() == SyntaxKind.SimpleAssignmentExpression && data.CanCreateNode) {
					var elementAccess = left as ElementAccessExpressionSyntax;
					var arguments = elementAccess.ArgumentList.Arguments;
					if(arguments.Count == 1) {
						var ex = CSharpParser.ParseExpression(elementAccess.Expression, new ParserSetting(data));
						var argument = CSharpParser.ParseExpression(arguments[0].Expression, new ParserSetting(data));
						var value = CSharpParser.ParseExpression(right, new ParserSetting(data));
						if(ex != null && ex.type != null) {
							var node = CSharpParser.CreateNode<Nodes.SetListItem>("Set", data.parent);
							node.Register();
							node.target.AssignToDefault(ex);
							node.index.AssignToDefault(argument);
							node.value.AssignToDefault(value);
							result = new ParserResult() {
								node = node,
								next = (nod) => {
									node.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
								}
							};
							return true;
						}
					}
				}

				MemberData mData;
				if(!CSharpParser.TryParseExpression(right, new ParserSetting(data), out mData)) {
					return base.StatementHandler(syntax, data, out result);
				}
				MemberData member;
				if(left is ElementAccessExpressionSyntax) {
					List<MemberData> parameters;
					member = CSharpParser.GetMemberFromExpression(left, new ParserSetting(data, left is ElementAccessExpressionSyntax), out parameters);
					parameters.Add(mData);
					var n = CSharpParser.CreateMultipurposeNode("SetValue", data.parent, member, parameters);
					if(expression.Kind() != SyntaxKind.SimpleAssignmentExpression) {
						if(expression.Kind() == SyntaxKind.AddAssignmentExpression) {
						}
						else if(expression.Kind() == SyntaxKind.SubtractAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.DivideAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.MultiplyAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.ModuloAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.ExclusiveOrAssignmentExpression) {

						}
						throw new System.Exception("Assign operation for element is not supported in expression:" + expression);
					}
					result = new ParserResult() {
						node = n,
						next = (nod) => {
							n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
						}
					};
				}
				else {
					if(!CSharpParser.TryParseExpression(left, new ParserSetting(data, left is ElementAccessExpressionSyntax), out member)) {
						return base.StatementHandler(syntax, data, out result);
					}
					if(!CSharpParser.IsSupportInline(member)) {
						member = CSharpParser.ToValueNode(member, null, new ParserSetting(data));
					}
					if(!CSharpParser.IsSupportInline(mData)) {
						mData = CSharpParser.ToValueNode(mData, null, new ParserSetting(data));
					}
					var n = CSharpParser.CreateComponent<NodeSetValue>("SetValue", data.parent);
					n.target.ConnectTo(member);
					n.value.ConnectTo(mData);
					if(expression.Kind() == SyntaxKind.SimpleAssignmentExpression) {
						n.setType = SetType.Change;
					}
					else if(expression.Kind() == SyntaxKind.AddAssignmentExpression) {
						n.setType = SetType.Add;
					}
					else if(expression.Kind() == SyntaxKind.SubtractAssignmentExpression) {
						n.setType = SetType.Subtract;
					}
					else if(expression.Kind() == SyntaxKind.DivideAssignmentExpression) {
						n.setType = SetType.Divide;
					}
					else if(expression.Kind() == SyntaxKind.MultiplyAssignmentExpression) {
						n.setType = SetType.Multiply;
					}
					else if(expression.Kind() == SyntaxKind.ModuloAssignmentExpression) {
						n.setType = SetType.Modulo;
					}
					result = new ParserResult() {
						node = n,
						next = (nod) => {
							n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
						}
					};
				}
				return true;
			}
			else if((syntax as ExpressionStatementSyntax).Expression is PostfixUnaryExpressionSyntax) {
				var expression = (syntax as ExpressionStatementSyntax).Expression as PostfixUnaryExpressionSyntax;
				SetType setType;
				if(expression.Kind() == SyntaxKind.PostIncrementExpression) {
					setType = SetType.Add;
				}
				else if(expression.Kind() == SyntaxKind.PostDecrementExpression) {
					setType = SetType.Subtract;
				}
				else {
					return base.StatementHandler(syntax, data, out result);
				}
				var member = CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data));
				var n = CSharpParser.CreateComponent<NodeSetValue>("SetValue", data.parent);
				n.setType = setType;
				n.target.ConnectTo(CSharpParser.ToSupportedValue(member, new ParserSetting(data)));
				n.value.ConnectTo(new MemberData(Operator.Increment(ReflectionUtils.CreateInstance(member.type))));
				result = new ParserResult() {
					node = n,
					next = (nod) => {
						n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
					}
				};
				return true;
			}
			else if((syntax as ExpressionStatementSyntax).Expression is PrefixUnaryExpressionSyntax) {
				var expression = (syntax as ExpressionStatementSyntax).Expression as PrefixUnaryExpressionSyntax;
				SetType setType;
				bool isPrefix = false;
				if(expression.Kind() == SyntaxKind.PostIncrementExpression) {
					setType = SetType.Add;
				}
				else if(expression.Kind() == SyntaxKind.PostDecrementExpression) {
					setType = SetType.Subtract;
				}
				else if(expression.Kind() == SyntaxKind.PreIncrementExpression) {
					setType = SetType.Add;
					isPrefix = true;
				}
				else if(expression.Kind() == SyntaxKind.PreDecrementExpression) {
					setType = SetType.Subtract;
					isPrefix = true;
				}
				else {
					return base.StatementHandler(syntax, data, out result);
				}
				var member = CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data));
				if(!CSharpParser.IsSupportInline(member)) {
					member = CSharpParser.ToValueNode(member, null, new ParserSetting(data));
				}
				if(isPrefix) {
					var n = CSharpParser.CreateComponent<IncrementDecrementNode>("SetValue", data.parent);
					n.isDecrement = setType == SetType.Subtract;
					n.target.ConnectTo(member);
					n.isPrefix = isPrefix;
					result = new ParserResult() {
						node = n,
						next = (nod) => {
							n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
						}
					};
				}
				else {
					var n = CSharpParser.CreateComponent<NodeSetValue>("SetValue", data.parent);
					n.target.ConnectTo(member);
					n.value.ConnectTo(new MemberData(Operator.Increment(ReflectionUtils.CreateInstance(member.type))));
					n.setType = setType;
					result = new ParserResult() {
						node = n,
						next = (nod) => {
							n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
						}
					};
				}
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}

		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is AssignmentExpressionSyntax) {
				var expression = syntax as AssignmentExpressionSyntax;
				var left = expression.Left;
				var right = expression.Right;
				MemberData mData;
				if(!CSharpParser.TryParseExpression(right, new ParserSetting(data), out mData)) {
					return base.ExpressionHandler(syntax, data, out value);
				}
				MemberData member;
				if(left is ElementAccessExpressionSyntax) {
					List<MemberData> parameters;
					member = CSharpParser.GetMemberFromExpression(left, new ParserSetting(data, left is ElementAccessExpressionSyntax), out parameters);
					parameters.Add(mData);
					var n = CSharpParser.CreateMultipurposeNode("SetValue", data.parent, member, parameters);
					if(expression.Kind() != SyntaxKind.SimpleAssignmentExpression) {
						if(expression.Kind() == SyntaxKind.AddAssignmentExpression) {
						}
						else if(expression.Kind() == SyntaxKind.SubtractAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.DivideAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.MultiplyAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.ModuloAssignmentExpression) {

						}
						else if(expression.Kind() == SyntaxKind.ExclusiveOrAssignmentExpression) {

						}
						throw new System.Exception("Assign operation for element is not supported in expression:" + expression);
					}
					value = CSharpParser.CreateFromPort(n.output);
					return true;
				}
				else {
					if(!CSharpParser.TryParseExpression(left, new ParserSetting(data, left is ElementAccessExpressionSyntax), out member)) {
						return base.ExpressionHandler(syntax, data, out value);
					}
					if(!CSharpParser.IsSupportInline(member)) {
						member = CSharpParser.ToValueNode(member, null, new ParserSetting(data));
					}
					if(!CSharpParser.IsSupportInline(mData)) {
						mData = CSharpParser.ToValueNode(mData, null, new ParserSetting(data));
					}
					var n = CSharpParser.CreateComponent<NodeSetValue>("SetValue", data.parent);
					n.target.ConnectTo(member);
					n.value.ConnectTo(mData);
					if(expression.Kind() == SyntaxKind.SimpleAssignmentExpression) {
						n.setType = SetType.Change;
					}
					else if(expression.Kind() == SyntaxKind.AddAssignmentExpression) {
						n.setType = SetType.Add;
					}
					else if(expression.Kind() == SyntaxKind.SubtractAssignmentExpression) {
						n.setType = SetType.Subtract;
					}
					else if(expression.Kind() == SyntaxKind.DivideAssignmentExpression) {
						n.setType = SetType.Divide;
					}
					else if(expression.Kind() == SyntaxKind.MultiplyAssignmentExpression) {
						n.setType = SetType.Multiply;
					}
					else if(expression.Kind() == SyntaxKind.ModuloAssignmentExpression) {
						n.setType = SetType.Modulo;
					}
					value = mData;
					return true;
				}
			}
			else if(syntax is PostfixUnaryExpressionSyntax) {
				var expression = syntax as PostfixUnaryExpressionSyntax;
				SetType setType;
				if(expression.Kind() == SyntaxKind.PostIncrementExpression) {
					setType = SetType.Add;
				}
				else if(expression.Kind() == SyntaxKind.PostDecrementExpression) {
					setType = SetType.Subtract;
				}
				else {
					return base.ExpressionHandler(syntax, data, out value);
				}
				var member = CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data));
				var n = CSharpParser.CreateComponent<IncrementDecrementNode>("SetValue", data.parent);
				n.isDecrement = setType == SetType.Subtract;
				n.target.ConnectTo(member);
				n.isPrefix = false;
				value = CSharpParser.CreateFromPort(n.output);
				return true;
			}
			else if(syntax is PrefixUnaryExpressionSyntax) {
				var expression = syntax as PrefixUnaryExpressionSyntax;
				SetType setType;
				if(expression.Kind() == SyntaxKind.PreIncrementExpression) {
					setType = SetType.Add;
				}
				else if(expression.Kind() == SyntaxKind.PreDecrementExpression) {
					setType = SetType.Subtract;
				}
				else {
					if(expression.Kind() == SyntaxKind.LogicalNotExpression) {
						if(data.parent == null) {
							//If cannot create node because of the syntax is not inside function / property / indexer
							value = MemberData.CreateFromValue(Operator.Not(CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data)).Get(null)));
							return true;
						}
						var nod = CSharpParser.CreateComponent<NotNode>("Not", data.parent);
						nod.target.ConnectTo(CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data)));
						value = CSharpParser.CreateFromPort(nod.output);
						return true;
					}
					else if(expression.Kind() == SyntaxKind.BitwiseNotExpression) {
						var mValue = CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data));
						if(mValue.IsTargetingValue) {
							value = MemberData.CreateFromValue(Operators.BitwiseNot(mValue.Get(null)));
							return true;
						}
					}
					return base.ExpressionHandler(syntax, data, out value);
				}
				var member = CSharpParser.ParseExpression(expression.Operand, new ParserSetting(data));
				var n = CSharpParser.CreateComponent<IncrementDecrementNode>("Unary", data.parent);
				n.isDecrement = setType == SetType.Subtract;
				n.target.ConnectTo(member);
				n.isPrefix = true;
				value = CSharpParser.CreateFromPort(n.output);
				return true;
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class AnonymousExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(data.parent == null) {
				return base.ExpressionHandler(syntax, data, out value);
			}
			if(syntax is AnonymousMethodExpressionSyntax) {
				var expression = syntax as AnonymousMethodExpressionSyntax;
				var symbol = CSharpParser.GetSymbol(expression, data.model) as IMethodSymbol;
				var node = CSharpParser.CreateComponent<NodeAnonymousFunction>("AnonymousFunction", data.parent);
				node.returnType = CSharpParser.ParseType(symbol.ReturnType);
				var parameters = new List<NodeAnonymousFunction.ParameterData>();
				System.Action postAction = null;
				if(symbol.Parameters.Length > 0) {
					for(int i = 0; i < symbol.Parameters.Length; i++) {
						var type = CSharpParser.ParseType(symbol.Parameters[i]);
						var parameterData = new NodeAnonymousFunction.ParameterData() {
							type = type,
							name = symbol.Parameters[i].Name,
						};
						postAction += () => {
							//Register parameter used for identity parameter.
							CSharpParser.RegisterSymbol(symbol.Parameters[i], node, _ => CSharpParser.CreateFromPort(parameterData.port), type);
							CSharpParser.RegisterAutoProxyConnectionForPort(parameterData.port);
						};
						parameters.Add(parameterData);
					}
				}
				node.parameters = parameters;
				node.Register();
				postAction?.Invoke();

				List<ParserResult> list;
				CSharpParser.ParseStatement(expression.Block.Statements, data.model, data.parent, out list);
				var body = list != null && list.Count > 0 ? list.First(item => item != null) : null;
				if(body != null) {
					if(body.node.IsFlowNode()) {
						node.body.ConnectTo(body.node.nodeObject.primaryFlowInput);
					}
					else if(body.node.CanGetValue()) {
						var n = CSharpParser.CreateComponent<NodeReturn>("return", data.parent);
						n.value.ConnectTo(body.node.nodeObject.primaryValueOutput);
						n.returnAnyType = true;
						node.body.ConnectTo(n.enter);
					}
					else {
						throw new System.Exception(expression.Body.ToString());
					}
				}
				value = CSharpParser.CreateFromPort(node.nodeObject.primaryValueOutput);
				return true;
			}
			else if(syntax is LambdaExpressionSyntax) {
				var expression = syntax as LambdaExpressionSyntax;
				var symbol = CSharpParser.GetSymbol(expression, data.model) as IMethodSymbol;
				var node = CSharpParser.CreateComponent<NodeAnonymousFunction>("AnonymousFunction", data.parent);
				node.returnType = CSharpParser.ParseType(symbol.ReturnType);
				var parameters = new List<NodeAnonymousFunction.ParameterData>();
				System.Action postAction = null;
				if(symbol.Parameters.Length > 0) {
					for(int i = 0; i < symbol.Parameters.Length; i++) {
						var param = symbol.Parameters[i];
						var type = CSharpParser.ParseType(param);
						var parameterData = new NodeAnonymousFunction.ParameterData() {
							type = type,
							name = param.Name,
						};
						postAction += () => {
							//Register parameter used for identity parameter.
							CSharpParser.RegisterSymbol(param, node, _ => CSharpParser.CreateFromPort(parameterData.port), type);
							CSharpParser.RegisterAutoProxyConnectionForPort(parameterData.port);
						};
						parameters.Add(parameterData);
					}
				}
				node.parameters = parameters;
				node.Register();
				postAction?.Invoke();

				if(expression.Body is BlockSyntax) {
					ParserResult body;
					CSharpParser.ParseStatement(expression.Body as BlockSyntax, data.model, data.parent, out body);
					if(body != null) {
						if(body.node.IsFlowNode()) {
							node.body.ConnectTo(body.node.nodeObject.primaryFlowInput);
						}
						else if(body.node.CanGetValue()) {
							var n = CSharpParser.CreateComponent<NodeReturn>("return", data.parent);
							n.value.ConnectTo(body.node.nodeObject.primaryValueOutput);
							n.returnAnyType = true;
							node.body.ConnectTo(n.enter);
						}
						else {
							throw new System.Exception(expression.Body.ToString());
						}
					}
				}
				else if(expression.Body is ExpressionSyntax) {
					var member = CSharpParser.ParseExpression(expression.Body as ExpressionSyntax, new ParserSetting(data));
					if(member != null && member.targetType == MemberData.TargetType.NodePort) {
						var port = member.Get<UPort>(null);
						if(node.returnType != typeof(void) && port is ValueOutput) {
							var n = CSharpParser.CreateComponent<NodeReturn>("return", data.parent);
							n.value.ConnectTo(port);
							n.returnAnyType = true;
							node.body.ConnectTo(n.enter);
						}
						else if(port is FlowInput) {
							node.body.ConnectTo(port);
						}
						else {
							throw new System.Exception(expression.Body.ToString());
						}
					}
					else {
						throw new System.Exception(expression.Body.ToString());
					}
				}
				else {
					throw new System.Exception(expression.Body.ToString());
				}
				value = CSharpParser.CreateFromPort(node.output);
				return true;
			}
			else if(syntax is NameSyntax) {
				var symbol = CSharpParser.GetSymbol(syntax, data.model) as IParameterSymbol;
				if(symbol != null && CSharpParser.IsInSource(symbol.Locations)) {
					var member = CSharpParser.GetSymbolReferenceValue(symbol, new ParserSetting(data)) as MemberData;
					if(member != null) {
						value = member;
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ParenthesizedExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ParenthesizedExpressionSyntax) {
				var expression = syntax as ParenthesizedExpressionSyntax;
				value = CSharpParser.ParseExpression(expression.Expression, data.model, data.parent);
				return true;
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ParameterHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is IdentifierNameSyntax && data.CanCreateNode) {
				var expression = syntax as IdentifierNameSyntax;
				var symbol = CSharpParser.GetSymbol(expression, data.model);
				if(symbol is IParameterSymbol && CSharpParser.GetSymbolOwner(symbol) != null) {
					var symbolValue = CSharpParser.GetSymbolReferenceValue(symbol, new ParserSetting(data)) as MemberData;
					if(symbolValue != null) {
						value = symbolValue;
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class UnaryExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is PrefixUnaryExpressionSyntax) {
				var expression = syntax as PrefixUnaryExpressionSyntax;
				if(expression.Kind() == SyntaxKind.LogicalNotExpression) {
					if(data.CanCreateNode) {
						var node = CSharpParser.CreateComponent<NotNode>("not", data.parent);
						node.target.ConnectTo(CSharpParser.ParseExpression(expression.Operand, data.model, data.parent));
						value = CSharpParser.CreateFromPort(node.output);
						return true;
					}
				}
				else if(expression.Kind() == SyntaxKind.UnaryMinusExpression) {
					if(expression.Operand is LiteralExpressionSyntax) {
						MemberData member;
						if(CSharpParser.TryParseExpression(expression.Operand, new ParserSetting(data), out member)) {
							value = new MemberData(Operator.Negate(member.Get(null)));
							return true;
						}
					}
					if(data.CanCreateNode) {
						var node = CSharpParser.CreateComponent<NegateNode>("negate", data.parent);
						node.target.ConnectTo(CSharpParser.ParseExpression(expression.Operand, data.model, data.parent));
						value = CSharpParser.CreateFromPort(node.output);
						return true;
					}
				}
				else if(expression.Kind() == SyntaxKind.UnaryPlusExpression) {
					value = CSharpParser.ParseExpression(expression.Operand, data.model, data.parent);
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class BinaryExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is BinaryExpressionSyntax) {
				BinaryExpressionSyntax binary = syntax as BinaryExpressionSyntax;
				MemberData left = null;
				MemberData right = null;
				CSharpParser.TryParseExpression(binary.Left, new ParserSetting(data, binary.Left is ElementAccessExpressionSyntax), out left);
				CSharpParser.TryParseExpression(binary.Right, new ParserSetting(data), out right);
				if(left != null && right != null) {
					if(binary.Kind() == SyntaxKind.AsExpression) {
						if(data.parent == null) {
							//If cannot create node because of the syntax is not inside function / property / indexer
							value = MemberData.CreateFromValue(Operators.TypeAs(left.Get(null), right.startType));
							return true;
						}
						var n = CSharpParser.CreateComponent<NodeConvert>("Convert", data.parent);
						n.target.ConnectTo(left);
						n.type = right.startType;
						n.compactDisplay = true;
						value = CSharpParser.CreateFromPort(n.output);
						return true;
					}
					else if(binary.Kind() == SyntaxKind.IsExpression) {
						if(data.parent == null) {
							//If cannot create node because of the syntax is not inside function / property / indexer
							value = MemberData.CreateFromValue(Operators.TypeIs(left.Get(null), right.startType));
							return true;
						}
						var n = CSharpParser.CreateComponent<ISNode>("IS", data.parent);
						n.target.ConnectTo(left);
						n.type = right.startType;
						value = CSharpParser.CreateFromPort(n.output);
						return true;
					}
					else if(binary.Kind() == SyntaxKind.CoalesceExpression) {
						if(data.parent == null) {
							//If cannot create node because of the syntax is not inside function / property / indexer
							value = MemberData.CreateFromValue(Operators.Coalesce(left.Get(null), right.Get(null)));
							return true;
						}
						var n = CSharpParser.CreateComponent<CoalescingNode>("Coalescing", data.parent);
						n.input.ConnectTo(left);
						n.fallback.ConnectTo(right);
						value = CSharpParser.CreateFromPort(n.output);
						return true;
					}
					ArithmeticType operatorType = default(ArithmeticType);
					if(binary.Kind() == SyntaxKind.AddExpression) {
						operatorType = ArithmeticType.Add;
					}
					else if(binary.Kind() == SyntaxKind.SubtractExpression) {
						operatorType = ArithmeticType.Subtract;
					}
					else if(binary.Kind() == SyntaxKind.DivideExpression) {
						operatorType = ArithmeticType.Divide;
					}
					else if(binary.Kind() == SyntaxKind.MultiplyExpression) {
						operatorType = ArithmeticType.Multiply;
					}
					else if(binary.Kind() == SyntaxKind.ModuloExpression) {
						operatorType = ArithmeticType.Modulo;
					}
					else {
						ComparisonType comparisonType = default(ComparisonType);
						if(binary.Kind() == SyntaxKind.EqualsExpression) {
							comparisonType = ComparisonType.Equal;
						}
						else if(binary.Kind() == SyntaxKind.GreaterThanExpression) {
							comparisonType = ComparisonType.GreaterThan;
						}
						else if(binary.Kind() == SyntaxKind.GreaterThanOrEqualExpression) {
							comparisonType = ComparisonType.GreaterThanOrEqual;
						}
						else if(binary.Kind() == SyntaxKind.LessThanExpression) {
							comparisonType = ComparisonType.LessThan;
						}
						else if(binary.Kind() == SyntaxKind.LessThanOrEqualExpression) {
							comparisonType = ComparisonType.LessThanOrEqual;
						}
						else if(binary.Kind() == SyntaxKind.NotEqualsExpression) {
							comparisonType = ComparisonType.NotEqual;
						}
						else {
							if(binary.Kind() == SyntaxKind.LogicalAndExpression) {
								if(data.parent == null) {
									//If cannot create node because of the syntax is not inside function / property / indexer
									value = MemberData.CreateFromValue(left.Get<bool>(null) && right.Get<bool>(null));
									return true;
								}
								var nod = CSharpParser.CreateComponent<MultiANDNode>("AND", data.parent);
								nod.inputs[0].port.ConnectTo(left);
								nod.inputs[1].port.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else if(binary.Kind() == SyntaxKind.LogicalOrExpression) {
								if(data.parent == null) {
									//If cannot create node because of the syntax is not inside function / property / indexer
									value = MemberData.CreateFromValue(left.Get<bool>(null) || right.Get<bool>(null));
									return true;
								}
								var nod = CSharpParser.CreateComponent<MultiORNode>("OR", data.parent);
								nod.inputs[0].port.ConnectTo(left);
								nod.inputs[1].port.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else if(binary.Kind() == SyntaxKind.LeftShiftExpression) {
								if(data.parent == null) {
									//If cannot create node because of the syntax is not inside function / property / indexer
									value = MemberData.CreateFromValue(Operators.LeftShift(left.Get(null), right.Get<int>(null)));
									return true;
								}
								var nod = CSharpParser.CreateComponent<ShiftNode>("Shift", data.parent);
								nod.operatorType = ShiftType.LeftShift;
								nod.targetA.ConnectTo(left);
								nod.targetB.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else if(binary.Kind() == SyntaxKind.RightShiftExpression) {
								if(data.parent == null) {
									//If cannot create node because of the syntax is not inside function / property / indexer
									value = MemberData.CreateFromValue(Operators.RightShift(left.Get(null), right.Get<int>(null)));
									return true;
								}
								var nod = CSharpParser.CreateComponent<ShiftNode>("Shift", data.parent);
								nod.operatorType = ShiftType.RightShift;
								nod.targetA.ConnectTo(left);
								nod.targetB.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else if(binary.Kind() == SyntaxKind.BitwiseAndExpression) {
								if(data.parent == null) {
									//If cannot create node because of the syntax is not inside function / property / indexer
									value = MemberData.CreateFromValue(Operators.And(left.Get(null), right.Get(null)));
									return true;
								}
								var nod = CSharpParser.CreateComponent<BitwiseNode>("AND", data.parent);
								nod.operatorType = BitwiseType.And;
								nod.targetA.ConnectTo(left);
								nod.targetB.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else if(binary.Kind() == SyntaxKind.BitwiseOrExpression) {
								if(data.parent == null) {
									//If cannot create node because of the syntax is not inside function / property / indexer
									value = MemberData.CreateFromValue(Operators.Or(left.Get(null), right.Get(null)));
									return true;
								}
								var nod = CSharpParser.CreateComponent<BitwiseNode>("Or", data.parent);
								nod.operatorType = BitwiseType.Or;
								nod.targetA.ConnectTo(left);
								nod.targetB.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else if(binary.Kind() == SyntaxKind.ExclusiveOrExpression) {
								var nod = CSharpParser.CreateComponent<BitwiseNode>("XOR", data.parent);
								nod.operatorType = BitwiseType.ExclusiveOr;
								nod.targetA.ConnectTo(left);
								nod.targetB.ConnectTo(right);
								value = CSharpParser.CreateFromPort(nod.output);
								return true;
							}
							else {
								throw new System.Exception("Couldn't handle expression type:" + binary.Kind().ToString() + "\nexpression:" + binary);
							}
						}
						if(data.parent == null) {
							//If cannot create node because of the syntax is not inside function / property / indexer
							value = MemberData.CreateFromValue(uNodeHelper.OperatorComparison(left.Get(null), right.Get(null), comparisonType));
							return true;
						}
						var n = CSharpParser.CreateComponent<ComparisonNode>("comparison", data.parent);
						n.inputA.ConnectTo(left);
						n.inputB.ConnectTo(right);
						n.operatorKind = comparisonType;
						value = CSharpParser.CreateFromPort(n.output);
						return true;
					}
					if(data.parent == null) {
						//If cannot create node because of the syntax is not inside function / property / indexer
						value = MemberData.CreateFromValue(uNodeHelper.ArithmeticOperator(left.Get(null), right.Get(null), operatorType));
						return true;
					}
					var node = CSharpParser.CreateComponent<MultiArithmeticNode>("arithmetic", data.parent);
					node.inputs[0].type = left.type;
					node.inputs[0].port.ConnectTo(left);
					node.inputs[1].type = right.type;
					node.inputs[1].port.ConnectTo(right);
					node.operatorKind = operatorType;
					value = CSharpParser.CreateFromPort(node.output);
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ReturnStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is ReturnStatementSyntax) {
				var expression = (syntax as ReturnStatementSyntax).Expression;
				if(expression == null) {
					var n = CSharpParser.CreateComponent<NodeReturn>("return", data.parent);
					result = new ParserResult() {
						node = n
					};
					return true;
				}
				MemberData val;
				if(CSharpParser.TryParseExpression(expression, data.model, data.parent, out val)) {
					MemberData member = new MemberData(val);
					var n = CSharpParser.CreateComponent<NodeReturn>("return", data.parent);
					n.value.ConnectTo(member);
					result = new ParserResult() {
						node = n
					};
					return true;
				}
			}
			else if(syntax is YieldStatementSyntax) {
				if((syntax as YieldStatementSyntax).ReturnOrBreakKeyword.ValueText == "break") {
					var n = CSharpParser.CreateComponent<NodeYieldBreak>("yield break", data.parent);
					result = new ParserResult() {
						node = n
					};
					return true;
				}
				var expression = (syntax as YieldStatementSyntax).Expression;
				MemberData val;
				if(CSharpParser.TryParseExpression(expression, data.model, data.parent, out val)) {
					MemberData member = new MemberData(val);
					var n = CSharpParser.CreateComponent<NodeYieldReturn>("yield return", data.parent);
					n.value.ConnectTo(member);
					result = new ParserResult() {
						node = n,
						next = (nod) => {
							n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
						}
					};
					return true;
				}
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class ThrowStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is ThrowStatementSyntax) {
				var expression = (syntax as ThrowStatementSyntax).Expression;
				if(expression == null) {
					var n = CSharpParser.CreateComponent<NodeThrow>("throw", data.parent);
					result = new ParserResult() {
						node = n
					};
					return true;
				}
				MemberData val;
				if(CSharpParser.TryParseExpression(expression, data.model, data.parent, out val)) {
					var n = CSharpParser.CreateComponent<NodeThrow>("throw", data.parent);
					n.value.ConnectTo(val);
					result = new ParserResult() {
						node = n
					};
					return true;
				}
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class JumpStatementHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is BreakStatementSyntax) {
				var n = CSharpParser.CreateComponent<NodeBreak>("break", data.parent);
				result = new ParserResult() {
					node = n
				};
				return true;
			}
			else if(syntax is ContinueStatementSyntax) {
				var n = CSharpParser.CreateComponent<NodeContinue>("continue", data.parent);
				result = new ParserResult() {
					node = n
				};
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class UncheckedExpressionHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is CheckedStatementSyntax statement) {
				List<ParserResult> list;
				CSharpParser.ParseStatement(statement.Block.Statements, data.model, data.parent, out list);
				if(list != null && list.Count > 0) {
					result = new ParserResult();
					//Assign target to the first node.
					result.node = list.First(item => item != null).node;
					//Assign next to the last node.
					for(int i = list.Count; i > 0; i--) {
						if(list[i - 1] != null && list[i - 1].next != null) {
							result.next = list[i - 1].next;
							break;
						}
					}
				}
				else {
					result = null;
				}
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class BlockExpressionHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is BlockSyntax) {
				List<ParserResult> list;
				CSharpParser.ParseStatement((syntax as BlockSyntax).Statements, data.model, data.parent, out list);
				if(list != null && list.Count > 0) {
					result = new ParserResult();
					//Assign target to the first node.
					result.node = list.First(item => item != null).node;
					//Assign next to the last node.
					for(int i = list.Count; i > 0; i--) {
						if(list[i - 1] != null && list[i - 1].next != null) {
							result.next = list[i - 1].next;
							break;
						}
					}
				}
				else {
					result = null;
				}
				return true;
			}
			else if(syntax is EmptyStatementSyntax) {
				result = null;
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class DeclarationExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is DeclarationExpressionSyntax) {
				var expression = syntax as DeclarationExpressionSyntax;
				if(expression.Designation is DiscardDesignationSyntax) {
					value = MemberData.Null;
					return true;
				}
				var LVS = data.parent as NodeContainer;
				if(LVS != null) {
					CSharpParser.ParseVariableDeclaration(expression, data.model, LVS.variableContainer, out var variable, out var dSyntax);
					if(variable != null) {
						CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(dSyntax, data.model), LVS, _ => MemberData.CreateFromValue(variable), variable.type);
						value = MemberData.CreateFromValue(variable);
					}
					else {
						value = null;
					}
					return true;
				}
				throw new System.Exception("Couldn't parse syntax: " + syntax.ToString());
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class IsPatternExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is IsPatternExpressionSyntax) {
				var expression = syntax as IsPatternExpressionSyntax;
				var LVS = data.parent as NodeContainer;
				if(LVS != null && expression.Pattern is DeclarationPatternSyntax declarationPattern) {
					CSharpParser.ParseVariableDeclaration(declarationPattern, data.model, LVS.variableContainer, out var variable, out var dSyntax);
					if(variable != null) {
						var n = CSharpParser.CreateComponent<ISNode>("IS", data.parent);
						CSharpParser.TryParseExpression(expression.Expression, new ParserSetting(data), out var left);
						n.target.ConnectTo(left);
						n.type = variable.type;
						value = CSharpParser.CreateFromPort(n.output);
						CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(dSyntax, data.model), LVS, _ => CSharpParser.CreateFromPort(n.value), variable.type);
						return true;
					}
				}
				throw new System.Exception("Couldn't parse syntax: " + syntax.ToString());
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ConditionalAccessExpressionHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is ExpressionStatementSyntax && data.CanCreateNode) {
				var expression = syntax as ExpressionStatementSyntax;
				if(expression.Expression is ConditionalAccessExpressionSyntax conditional) {
					var n = CSharpParser.CreateComponent<NodeIf>("If", data.parent);
					var cond = CSharpParser.ParseExpression(conditional.Expression, new ParserSetting(data));
					if(cond != null) {
						cond = CSharpParser.ToValueNode(cond, null, new ParserSetting(data));
					}
					n.condition.ConnectTo(CSharpParser.CreateFromPort(CSharpParser.CreateComponent<ComparisonNode>("Comparison", data.parent).output));
					var whenNotNull = CSharpParser.ParseExpression(conditional.Expression, new ParserSetting(data));
					whenNotNull.instance = cond;
					result = new ParserResult() {
						node = n,
						next = (nod) => {
							n.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
						}
					};
					return true;
				}
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class LocalVariableHandles : SyntaxHandles {
		public override bool StatementHandler(StatementSyntax syntax, ParserData data, out ParserResult result) {
			if(syntax is LocalDeclarationStatementSyntax && data.CanCreateNode) {
				ParserResult firstResult = null;
				ParserResult lastResult = data.previousResult;
				var expression = syntax as LocalDeclarationStatementSyntax;
				var variables = CSharpParser.ParseVariableDeclaration(expression.Declaration, data.model, (data.parent as NodeContainer).variableContainer, (dec, var) => {
					CSharpParser.RegisterSymbol(CSharpParser.GetSymbol(dec, data.model), data.parent, _ => MemberData.CreateFromValue(var));
				}, (dec, var) => {
					MemberData member;
					if(CSharpParser.TryParseExpression(dec.Initializer.Value, data.model, data.parent, out member)) {
						if(member.targetType == MemberData.TargetType.Values) {
							var.defaultValue = member.Get(null);
						}
						else {
							NodeSetValue node = CSharpParser.CreateComponent<NodeSetValue>("set variable", data.parent);
							node.target.ConnectTo(MemberData.CreateFromValue(var));
							node.value.ConnectTo(member);
							if(lastResult != null) {
								lastResult.next(node);
								//Make sure next can't be assigned anymore.
								lastResult.next = null;
							}
							var PR = new ParserResult() {
								node = node,
								next = (nod) => {
									node.exit.ConnectTo(nod.nodeObject.primaryFlowInput);
								}
							};
							lastResult = PR;
							if(firstResult == null) {
								firstResult = PR;
							}

						}
						return true;
					}
					return false;
				});
				if(data.parent is NodeContainer) {

				}
				else {
					throw new System.InvalidOperationException();
				}
				result = firstResult ?? lastResult;
				return true;
			}
			return base.StatementHandler(syntax, data, out result);
		}
	}

	public class ObjectCreationHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ObjectCreationExpressionSyntax) {
				var expression = syntax as ObjectCreationExpressionSyntax;
				List<MemberData> parameters;
				var member = CSharpParser.GetMemberFromExpression(expression, new ParserSetting(data), out parameters, out var initializers);
				if(member != null) {
					if(data.CanCreateNode) {
						if(member.IsTargetingValue && (initializers == null || initializers.Count == 0) && FilterAttribute.DefaultTypeFilter.IsValidTypeForValueConstant(member.type)) {
							value = member;
							return true;
						}
						var node = CSharpParser.CreateMultipurposeNode("ObjectCreation", data.parent, member, parameters);
						if(initializers != null) {
							if(node.target.targetType.IsTargetingValue()) {
								var ctor = ReflectionUtils.GetDefaultConstructor(node.target.type);
								if(ctor != null) {
									node.target = MemberData.CreateFromMember(ctor);
								}
							}
							for(int i = 0; i < initializers.Count; i++) {
								node.member.initializers.Add(new MultipurposeMember.InitializerData() {
									name = initializers[i].name,
									type = initializers[i].type,
								});
							}
							node.Register();
							for(int i = 0; i < initializers.Count; i++) {
								node.member.initializers[i].port.AssignToDefault(initializers[i].value);
							}
						}
						value = CSharpParser.CreateFromPort(node.output);
						return true;
					}
					else if(!data.parent) {
						value = member;
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ArrayCreationHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ArrayCreationExpressionSyntax) {
				var expression = syntax as ArrayCreationExpressionSyntax;
				if(data.CanCreateNode) {
					var node = CSharpParser.CreateComponent<MakeArrayNode>("ArrayCreation", data.parent);
					node.elementType = CSharpParser.ParseType((CSharpParser.GetTypeSymbol(expression, data.model) as IArrayTypeSymbol).ElementType);
					node.elements.Clear();
					if(expression.Initializer != null) {
						var members = new List<MemberData>();
						foreach(var ex in expression.Initializer.Expressions) {
							node.elements.Add(new MakeArrayNode.PortData());
							members.Add(CSharpParser.ParseExpression(ex, data.model, data.parent));
						}
						node.Register();
						for(int i = 0; i < members.Count; i++) {
							node.elements[i].port.ConnectTo(members[i]);
						}
					}
					//Check if not multidimensional array.
					if(expression.Type.RankSpecifiers.Count == 1) {
						foreach(var rank in expression.Type.RankSpecifiers) {
							if(rank.Sizes.Count == 1) {
								if(!(rank.Sizes[0] is OmittedArraySizeExpressionSyntax)) {
									var length = CSharpParser.ParseExpression(rank.Sizes[0], new ParserSetting(data));
									node.length.ConnectTo(CSharpParser.ToValueNode(length, null, new ParserSetting(data)));
								}
							}
						}
					}
					else {
						throw new System.Exception("Multidimensional array is not supported.");
					}
					value = CSharpParser.CreateFromPort(node.output);
					return true;
				}
				else if(!data.parent) {
					List<MemberData> parameters;
					var member = CSharpParser.GetMemberFromExpression(expression, new ParserSetting(data), out parameters);
					value = member;
					return true;
				}
			}
			else if(syntax is ImplicitArrayCreationExpressionSyntax) {
				var expression = syntax as ImplicitArrayCreationExpressionSyntax;
				if(data.CanCreateNode) {
					//MultipurposeMember memberInvoke = new MultipurposeMember() { target = member, parameters = parameters != null ? parameters.ToArray() : null };
					//uNodeEditorUtility.UpdateMultipurposeMember(memberInvoke);
					//var node = CSharpParser.CreateComponent<MultipurposeNode>("ArrayCreation", data.parent.transform);
					//node.target = memberInvoke;
					var node = CSharpParser.CreateComponent<MakeArrayNode>("ArrayCreation", data.parent);
					node.elementType = CSharpParser.ParseType((CSharpParser.GetTypeSymbol(expression, data.model) as IArrayTypeSymbol).ElementType);
					node.elements.Clear();
					if(expression.Initializer != null) {
						var members = new List<MemberData>();
						foreach(var ex in expression.Initializer.Expressions) {
							node.elements.Add(new MakeArrayNode.PortData());
							members.Add(CSharpParser.ParseExpression(ex, data.model, data.parent));
						}
						node.Register();
						for(int i = 0; i < members.Count; i++) {
							node.elements[i].port.ConnectTo(members[i]);
						}
					}
					////Check if not multidimensional array.
					//if(expression.Type.RankSpecifiers.Count == 1) {
					//	foreach(var rank in expression.Type.RankSpecifiers) {
					//		if(rank.Sizes.Count == 1) {
					//			if(!(rank.Sizes[0] is OmittedArraySizeExpressionSyntax)) {
					//				var length = CSharpParser.ParseExpression(rank.Sizes[0], new ParserSetting(data));
					//				node.arrayLength = CSharpParser.ToValueNode(length, null, new ParserSetting(data));
					//			}
					//		}
					//	}
					//} else {
					//	throw new System.Exception("Multidimensional array is not supported.");
					//}
					value = CSharpParser.CreateFromPort(node.output);
					return true;
				}
				else if(!data.parent) {
					List<MemberData> parameters;
					var member = CSharpParser.GetMemberFromExpression(expression, new ParserSetting(data), out parameters);
					value = member;
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class CastExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is CastExpressionSyntax) {
				var expression = syntax as CastExpressionSyntax;
				var symbol = CSharpParser.GetTypeSymbol(expression, data.model);
				var type = CSharpParser.ParseType(symbol);
				if(type != null && data.CanCreateNode) {
					var member = CSharpParser.ParseExpression(expression.Expression, data.model, data.parent);
					if(member != null) {
						var node = CSharpParser.CreateComponent<NodeConvert>("TypeConvert", data.parent);
						node.target.ConnectTo(member);
						node.type = type;
						node.compactDisplay = true;
						node.useASWhenPossible = false;
						value = CSharpParser.CreateFromPort(node.output);
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class InterpolatedStringExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is InterpolatedStringExpressionSyntax) {
				var expression = syntax as InterpolatedStringExpressionSyntax;
				var contents = expression.Contents;
				var members = new List<MemberData>();
				foreach(var c in contents) {
					if(c is InterpolationSyntax interpolationSyntax) {
						members.Add(CSharpParser.ParseExpression(interpolationSyntax.Expression, data.model, data.parent));
					}
					else if(c is InterpolatedStringTextSyntax textSyntax) {
						members.Add(MemberData.CreateFromValue(textSyntax.TextToken.Value));
					}
					else {
						throw new System.Exception("Unsupported syntax: " + c.GetType());
					}
				}
				if(data.parent == null) {
					//If cannot create node because of the syntax is not inside function / property / indexer
					value = MemberData.CreateFromValue(string.Concat(from m in members select m.Get(null)));
					return true;
				}
				var node = CSharpParser.CreateComponent<StringBuilderNode>("StringBuilder", data.parent);
				node.stringValues = new List<StringBuilderNode.Data>();
				foreach(var m in members) {
					node.stringValues.Add(new StringBuilderNode.Data());
				}
				node.Register();
				for(int i = 0; i < members.Count; i++) {
					node.stringValues[i].port.ConnectTo(members[i]);
				}
				value = CSharpParser.CreateFromPort(node.output);
				return true;
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ThisExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ThisExpressionSyntax) {
				if(data.CanCreateNode) {
					var owner = data.root;
					if(owner != null) {
						value = MemberData.This(owner);
						//var node = CSharpParser.CreateComponent<MultipurposeNode>("this", data.parent.transform, data.root);
						//node.target = new MultipurposeMember() { target = new MemberData(owner) };
						//value = new MemberData(node, MemberData.TargetType.ValueNode);
						return true;
					}
				}
			}
			else if(syntax is BaseExpressionSyntax) {
				value = MemberData.This(data.root);
				return true;
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class LiteralExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is LiteralExpressionSyntax) {
				var expression = syntax as LiteralExpressionSyntax;
				if(expression.Kind() == SyntaxKind.DefaultLiteralExpression) {
					var type = CSharpParser.ParseType(data.model.GetTypeInfo(syntax).Type).type;
					if(type.IsValueType) {
						value = new MemberData(System.Activator.CreateInstance(type));
					}
					else {
						value = new MemberData(null, MemberData.TargetType.Null);
					}
					return true;
				}
				else if(expression.Kind() == SyntaxKind.NullLiteralExpression) {
					value = new MemberData(null, MemberData.TargetType.Null);
					return true;
				}
				else {
					value = new MemberData(expression.Token.Value);
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	//public class IdentifierNamenHandles : SyntaxHandles {
	//	public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
	//		if(syntax is IdentifierNameSyntax) {
	//			var expression = syntax as IdentifierNameSyntax;
	//			var symbol = CSharpParser.GetSymbol(expression, data.model);
	//			if(symbol is IMethodSymbol) {
	//				if(CSharpParser.IsInSource(symbol.Locations)) {

	//				}
	//			}
	//		}
	//		return base.ExpressionHandler(syntax, data, out value);
	//	}
	//}

	public class ElementExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ElementAccessExpressionSyntax && data.CanCreateNode) {
				var expression = syntax as ElementAccessExpressionSyntax;
				if(data.CanCreateNode) {
					var arguments = expression.ArgumentList.Arguments;
					if(arguments.Count == 1) {
						var ex = CSharpParser.ParseExpression(expression.Expression, new ParserSetting(data));
						var argument = CSharpParser.ParseExpression(arguments[0].Expression, new ParserSetting(data));
						if(ex != null && ex.type != null) {
							var node = CSharpParser.CreateNode<Nodes.GetListItem>("Get", data.parent);
							node.Register();
							node.target.AssignToDefault(ex);
							node.index.AssignToDefault(argument);
							value = CSharpParser.CreateFromPort(node.output);
							return true;
						}
					}
				}
				{
					List<MemberData> parameters;
					var member = CSharpParser.GetMemberFromExpression(expression, new ParserSetting(data), out parameters);
					if(member != null) {
						var node = CSharpParser.CreateMultipurposeNode("invoke", data.parent, member, parameters);
						value = CSharpParser.CreateFromPort(node.output);
						return true;
					}
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class DefaultExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is DefaultExpressionSyntax) {
				var expression = syntax as DefaultExpressionSyntax;
				if(data.CanCreateNode) {
					var node = CSharpParser.CreateComponent<DefaultNode>("default", data.parent);
					node.type = CSharpParser.ParseType(expression, data.model);
					value = CSharpParser.CreateFromPort(node.output);
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ConditionalExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ConditionalExpressionSyntax) {
				var expression = syntax as ConditionalExpressionSyntax;
				if(data.CanCreateNode) {
					var node = CSharpParser.CreateComponent<ConditionalNode>("conditional", data.parent);
					node.condition.ConnectTo(CSharpParser.ParseExpression(expression.Condition, data.model, data.parent));
					node.onTrue.ConnectTo(CSharpParser.ParseExpression(expression.WhenTrue, data.model, data.parent));
					node.onFalse.ConnectTo(CSharpParser.ParseExpression(expression.WhenFalse, data.model, data.parent));
					value = CSharpParser.CreateFromPort(node.output);
					return true;
				}
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class TypeOfExpressionHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is TypeOfExpressionSyntax) {
				var expression = syntax as TypeOfExpressionSyntax;
				value = MemberData.CreateFromType(CSharpParser.ParseType(expression, data.model));
				if(value != null && value.targetType == MemberData.TargetType.uNodeGenericParameter) {
					var node = CSharpParser.CreateMultipurposeNode("typeof(T)", data.parent, value);
					value = MemberData.CreateFromValue(new UPortRef(node.output));
				}
				return true;
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}

	public class ArrayTypeHandles : SyntaxHandles {
		public override bool ExpressionHandler(ExpressionSyntax syntax, ParserData data, out MemberData value) {
			if(syntax is ArrayTypeSyntax) {
				var expression = syntax as ArrayTypeSyntax;
				value = MemberData.CreateFromType(CSharpParser.ParseType(expression, data.model));
				if(value != null && value.targetType == MemberData.TargetType.uNodeGenericParameter) {
					var node = CSharpParser.CreateMultipurposeNode("typeof(T)", data.parent, value);
					value = MemberData.CreateFromValue(new UPortRef(node.output));
				}
				return true;
			}
			return base.ExpressionHandler(syntax, data, out value);
		}
	}
}