using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class DataDirectiveSpec {
	public readonly string Name;
	public readonly ImmutableArray<OperandKind> Operands;

	public DataDirectiveSpec(string name, ImmutableArray<OperandKind> operands) {
		Name = name;
		Operands = operands;
	}
}
