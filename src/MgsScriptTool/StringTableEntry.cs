using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class StringTableEntry {
	public readonly int Index;
	public readonly ImmutableArray<StringToken> Tokens;

	public StringTableEntry(int index, ImmutableArray<StringToken> tokens) {
		Index = index;
		Tokens = tokens;
	}
}
