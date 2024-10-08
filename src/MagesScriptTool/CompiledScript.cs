using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class CompiledScript {
	public readonly ImmutableArray<byte> Code;
	public readonly ImmutableArray<int> Labels;
	public readonly ImmutableArray<int> ReturnLabels;

	public CompiledScript(ImmutableArray<byte> code, ImmutableArray<int> labels, ImmutableArray<int> returnLabels) {
		Code = code;
		Labels = labels;
		ReturnLabels = returnLabels;
	}
}
