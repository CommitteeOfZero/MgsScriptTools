using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class DataDirective {
	public readonly string Name;
	public readonly ImmutableArray<ExpressionNode> Operands;

	public DataDirective(string name, ImmutableArray<ExpressionNode> operands) {
		Name = name;
		Operands = operands;
	}
}
