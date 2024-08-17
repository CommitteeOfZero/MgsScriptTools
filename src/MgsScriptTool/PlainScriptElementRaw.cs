using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class PlainScriptElementRaw : PlainScriptElement {
	public readonly ImmutableArray<byte> Data;

	public PlainScriptElementRaw(ImmutableArray<byte> data) {
		Data = data;
	}
}
