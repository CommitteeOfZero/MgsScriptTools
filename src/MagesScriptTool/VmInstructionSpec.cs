using System.Collections.Immutable;

namespace MagesScriptTool;

class VmInstructionSpec : InstructionSpec {
	public readonly ImmutableArray<byte> Opcode;
    
    public VmInstructionSpec(string name, ImmutableArray<byte> opcode, ImmutableArray<OperandKind> operands) : base(name, operands) {
        Opcode = opcode;
    }
}