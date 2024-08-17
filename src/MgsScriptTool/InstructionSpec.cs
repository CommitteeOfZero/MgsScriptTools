using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class InstructionSpec {
	public readonly string Name;
	public readonly ImmutableArray<byte> Opcode;
	public readonly ImmutableArray<OperandKind> Operands;

	public InstructionSpec(string name, ImmutableArray<byte> opcode, ImmutableArray<OperandKind> operands) {
		Name = name;
		Opcode = opcode;
		Operands = operands;
	}
}
