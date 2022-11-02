using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

class MstSyntax {
	MstStringSyntax _stringSyntax;

	public MstSyntax(MstStringSyntax stringSyntax) {
		_stringSyntax = stringSyntax;
	}

	public void Stringify(StringBuilder builder, MstEntry[] entries) {
		foreach (var entry in entries) {
			builder.Append(entry.Index);
			builder.Append(':');
			_stringSyntax.Stringify(builder, entry.Parts);
			builder.Append('\n');
		}
	}

	public MstEntry[] Parse(TextStream reader) {
		List<MstEntry> entries = new();
		while (reader.Has(0)) {
			if (!IsDigit(reader.Peek(0)))
				throw new ParsingException($"Expected string index");
			var index = ParseNumber(reader);
			if (!ParseUtils.TrySkip(reader, ':'))
				throw new ParsingException($"Expected ':'");
			var parts = _stringSyntax.Parse(reader);
			if (reader.Has(0) && !ParseUtils.TrySkip(reader, '\n'))
				throw new ParsingException($"Expected '\\n'");
			entries.Add(new MstEntry(index, parts));
		}
		return entries.ToArray();
	}

	int ParseNumber(TextStream reader) {
		Debug.Assert(IsDigit(reader.Peek(0)));
		string s = "";
		while (IsDigit(reader.Peek(0)))
			s += reader.Next();
		return int.Parse(s);
	}

	bool IsDigit(char c) {
		return c is (>= '0' and <= '9');
	}
}

class MstEntry {
	public int Index;
	public MstStringPart[] Parts;

	public MstEntry(int index, MstStringPart[] parts) {
		Index = index;
		Parts = parts;
	}
}
