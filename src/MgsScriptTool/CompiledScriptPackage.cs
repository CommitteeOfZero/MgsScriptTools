using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class CompiledScriptPackage {
	public readonly CompiledScript Script;
	public readonly ImmutableArray<ImmutableArray<StringToken>> Strings;

	public CompiledScriptPackage(CompiledScript script, ImmutableArray<ImmutableArray<StringToken>> strings) {
		Script = script;
		Strings = strings;
	}
}
