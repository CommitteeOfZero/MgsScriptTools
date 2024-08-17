using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class StringTagSpec {
	public readonly string Name;
	public readonly int Opcode;
	public readonly ImmutableArray<OperandKind> Operands;

	public StringTagSpec(string name, int opcode, ImmutableArray<OperandKind> operands) {
		Name = name;
		Opcode = opcode;
		Operands = operands;
	}
}
