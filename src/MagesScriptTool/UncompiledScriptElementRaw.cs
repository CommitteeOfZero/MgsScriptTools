using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class UncompiledScriptElementRaw : UncompiledScriptElement {
	public readonly ImmutableArray<byte> Data;

	public UncompiledScriptElementRaw(ImmutableArray<byte> data) {
		Data = data;
	}
}
