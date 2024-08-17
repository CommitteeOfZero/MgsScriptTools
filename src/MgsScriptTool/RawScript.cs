using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class RawScript {
	public readonly ImmutableArray<byte> Code;
	public readonly ImmutableArray<int> Labels;
	public readonly ImmutableArray<int> ReturnLabels;

	public RawScript(ImmutableArray<byte> code, ImmutableArray<int> labels, ImmutableArray<int> returnLabels) {
		Code = code;
		Labels = labels;
		ReturnLabels = returnLabels;
	}
}
