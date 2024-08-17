using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class Instruction {
	public readonly string Name;
	public readonly ImmutableArray<ExpressionNode> Operands;

	public Instruction(string name, ImmutableArray<ExpressionNode> operands) {
		Name = name;
		Operands = operands;
	}
}
