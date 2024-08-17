using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class StringTagSpec {
	public readonly int Opcode;
	public readonly StringTagKind Kind;
	public readonly ImmutableArray<OperandKind> Operands;

	public StringTagSpec(int opcode, StringTagKind kind, ImmutableArray<OperandKind> operands) {
		Opcode = opcode;
		Kind = kind;
		Operands = operands;
	}
}
