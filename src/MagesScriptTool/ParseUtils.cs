namespace MagesScriptTool;

class ParseUtils {
	public static bool TrySkip(TextStream reader, char c) {
		if (!reader.Has(0) || reader.Peek(0) != c) {
			return false;
		}
		reader.Skip(1);
		return true;
	}

	public static bool TrySkip(TextStream reader, string s) {
		if (!reader.Has(s.Length - 1)) {
			return false;
		}
		for (int i = 0; i < s.Length; i++) {
			if (reader.Peek(i) != s[i]) {
				return false;
			}
		}
		reader.Skip(s.Length);
		return true;
	}

	public static bool SkipSpaceComments(TextStream reader) {
		bool consumed = false;
		while (true) {
			consumed |= SkipSpace(reader);
			if (!SkipComment(reader)) {
				break;
			}
			consumed = true;
		}
		return consumed;
	}

	public static bool SkipHSpaceComments(TextStream reader) {
		bool consumed = false;
		while (true) {
			consumed |= SkipHSpace(reader);
			if (!SkipComment(reader)) {
				break;
			}
			consumed = true;
		}
		return consumed;
	}

	public static bool SkipComment(TextStream reader) {
		TextStream.Position startPos = reader.Tell();
		if (TrySkip(reader, "/*")) {
			while (true) {
				if (!reader.Has(0)) {
					reader.Seek(startPos);
					throw new ParsingException("Unterminated multiline comment.");
				}
				if (TrySkip(reader, "*/")) {
					break;
				}
				reader.Skip(1);
			}
		} else if (TrySkip(reader, "//")) {
			while (reader.Has(0) && reader.Peek(0) != '\n') {
				reader.Skip(1);
			}
		} else {
			return false;
		}
		return true;
	}

	public static bool SkipSpace(TextStream reader) {
		bool consumed = false;
		while (true) {
			if (!IsSpace(reader.Peek(0))) {
				break;
			}
			reader.Skip(1);
			consumed = true;
		}
		return consumed;
	}

	public static bool SkipHSpace(TextStream reader) {
		bool consumed = false;
		while (true) {
			if (!IsHSpace(reader.Peek(0))) {
				break;
			}
			reader.Skip(1);
			consumed = true;
		}
		return consumed;
	}

	public static bool IsHSpace(char ch) {
		return ch is '\t' or ' ';
	}

	public static bool IsSpace(char ch) {
		return IsHSpace(ch) || ch == '\n';
	}
}
