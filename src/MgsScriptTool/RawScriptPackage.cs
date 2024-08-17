using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class RawScriptPackage {
	public readonly RawScript Script;
	public readonly ImmutableArray<ImmutableArray<StringToken>> Strings;

	public RawScriptPackage(RawScript script, ImmutableArray<ImmutableArray<StringToken>> strings) {
		Script = script;
		Strings = strings;
	}
}
