using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace MgsScriptTool;

sealed class UncompiledStringTableSyntax {
	readonly UncompiledStringSyntax _stringSyntax;

	public UncompiledStringTableSyntax(UncompiledStringSyntax stringSyntax) {
		_stringSyntax = stringSyntax;
	}

	public void Format(StringBuilder builder, ImmutableArray<StringTableEntry> entries) {
		foreach (StringTableEntry entry in entries) {
			builder.Append(entry.Index);
			builder.Append(':');
			_stringSyntax.Format(builder, entry.Tokens);
			builder.Append('\n');
		}
	}

	public ImmutableArray<StringTableEntry> Parse(TextStream reader) {
		List<StringTableEntry> entries = [];
		while (reader.Has(0)) {
			if (!IsDigit(reader.Peek(0))) {
				throw new ParsingException("Expected string index.");
			}
			int index = ParseNumber(reader);
			if (!ParseUtils.TrySkip(reader, ':')) {
				throw new ParsingException("Expected ':'.");
			}
			ImmutableArray<StringToken> tokens = _stringSyntax.Parse(reader);
			if (reader.Has(0) && !ParseUtils.TrySkip(reader, '\n')) {
				throw new ParsingException("Expected '\\n'.");
			}
			entries.Add(new(index, tokens));
		}
		return [..entries];
	}

	static int ParseNumber(TextStream reader) {
		Debug.Assert(IsDigit(reader.Peek(0)));
		string s = "";
		while (IsDigit(reader.Peek(0))) {
			s += reader.Next();
		}
		return int.Parse(s);
	}

	static bool IsDigit(char c) {
		return c is >= '0' and <= '9';
	}
}
