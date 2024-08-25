using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class StringTokenTag : StringToken {
	public readonly string Name;
	public readonly ImmutableArray<ExpressionNode> Operands;

	public StringTokenTag(string name, ImmutableArray<ExpressionNode> operands) {
		Name = name;
		Operands = operands;
	}
}
