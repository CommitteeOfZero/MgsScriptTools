using System.Collections.Immutable;

namespace MagesScriptTool;

abstract class InstructionSpec {
	public readonly string Name;
	public readonly ImmutableArray<OperandKind> Operands;

	public InstructionSpec(string name, ImmutableArray<OperandKind> operands) {
		Name = name;
		Operands = operands;
	}
}
