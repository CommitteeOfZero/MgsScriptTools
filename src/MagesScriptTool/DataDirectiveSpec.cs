using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class DataDirectiveSpec : InstructionSpec {
	public DataDirectiveSpec(string name, ImmutableArray<OperandKind> operands) : base(name, operands) {}
}
