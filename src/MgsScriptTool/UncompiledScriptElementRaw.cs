using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class UncompiledScriptElementRaw : UncompiledScriptElement {
	public readonly ImmutableArray<byte> Data;

	public UncompiledScriptElementRaw(ImmutableArray<byte> data) {
		Data = data;
	}
}
