using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class StringTokenTag : StringToken {
	public readonly StringTagKind Kind;
	public readonly ImmutableArray<ExpressionNode> Operands;

	public StringTokenTag(StringTagKind kind, ImmutableArray<ExpressionNode> operands) {
		Kind = kind;
		Operands = operands;
	}
}
