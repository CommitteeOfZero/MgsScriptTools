using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class CompiledScriptPackage {
	public readonly CompiledScript Script;
	public readonly ImmutableArray<ImmutableArray<StringToken>> Strings;

	public CompiledScriptPackage(CompiledScript script, ImmutableArray<ImmutableArray<StringToken>> strings) {
		Script = script;
		Strings = strings;
	}
}
