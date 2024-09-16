using System.Collections.Immutable;
using System.Text;

namespace MagesScriptTool;

sealed class TextStream {
	readonly ImmutableArray<Rune> _data;
	readonly string? _sourceName;

	int _offset = 0;
	int _column = 1;
	int _line = 1;

	public TextStream(string data, string? sourceName = null) {
		_data = [..data.ReplaceLineEndings("\n").EnumerateRunes()];
		_sourceName = sourceName;
	}

	public Rune PeekRune(int skip) {
		if (!Has(skip)) {
			return (Rune)'\0';
		}
		return _data[_offset + skip];
	}

	public char Peek(int skip) {
		return checked((char)PeekRune(skip).Value);
	}

	public void Skip(int offset) {
		for (int i = 0; i < offset; i++) {
			Next();
		}
	}

	public Rune NextRune() {
		if (!Has(0)) {
			throw new ParsingException("Attempted to read past the end of the stream.");
		}
		Rune value = _data[_offset++];
		if (value != (Rune)'\n') {
			_column++;
		} else {
			_column = 1;
			_line++;
		}
		return value;
	}

	public char Next() {
		return checked((char)NextRune().Value);
	}

	public bool Has(int offset) {
		return _offset + offset < _data.Length;
	}

	public Position Tell() {
		return new Position() {
			SourceName = _sourceName,
			Offset = _offset,
			Column = _column,
			Line = _line,
		};
	}

	public void Seek(Position position) {
		_offset = position.Offset;
		_column = position.Column;
		_line = position.Line;
	}

	public struct Position {
		public string? SourceName;
		public int Offset;
		public int Column;
		public int Line;

		public override string ToString() {
			if (SourceName is null) {
				return $"{Line}:{Column}";
			}
			return $"{SourceName}:{Line}:{Column}";
		}
	}
}
