using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class ExpressionNodeOperation : ExpressionNode {
	public readonly OperatorKind Kind;
	public readonly ImmutableArray<ExpressionNode> Left;
	public readonly ImmutableArray<ExpressionNode> Right;

	public ExpressionNodeOperation(OperatorKind kind, ImmutableArray<ExpressionNode> left, ImmutableArray<ExpressionNode> right) {
		Kind = kind;
		Left = left;
		Right = right;
	}
}
