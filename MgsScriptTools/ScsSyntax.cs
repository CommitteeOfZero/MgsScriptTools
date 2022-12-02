using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

public class ScsSyntax {
	public static void Stringify(StringBuilder builder, StringBuilder? sdbBuilder, ScsPart[] parts) {
		new ScsSyntaxStringifier(builder, sdbBuilder).Stringify(parts);
	}

	public static ScsPart[] Parse(TextStream reader) {
		return new ScsSyntaxParser(reader).Parse();
	}

	class ScsSyntaxStringifier {
		StringBuilder _builder;
		StringBuilder? _sdbBuilder;
		int _row;

		public ScsSyntaxStringifier(StringBuilder builder, StringBuilder? sdbBuilder) {
			_builder = builder;
			_sdbBuilder = sdbBuilder;
			_row = 1;
		}

		public void Stringify(ScsPart[] parts) {
			foreach (var part in parts)
				StringifyPart(part);
		}

		void StringifyPart(ScsPart part) {
			switch (part) {
				case ScsInstruction { Value: Instruction instruction, Offset: int offset }: {
					string s = FormatInstruction(instruction);
					if (_sdbBuilder is not null)
						_sdbBuilder.Append($"{offset,6},{_row,6}, {s}\n");
					Append($"\t{s}\n");
					break;
				}
				case ScsLabel { Value: int index }: {
					Append($"{index}");
					Append(":");
					Append("\n");
					break;
				}
				case ScsReturnAddress { Value: int index }: {
					Append("*");
					Append($"{index}");
					Append(":");
					Append("\n");
					break;
				}
				case ScsError { Position: int position, Error: Exception error }: {
					string message = error.ToString().ReplaceLineEndings("\n");
					message = $"An error has occurred at 0x{position:X}: {message}";
					foreach (var line in message.Split('\n'))
						Append($"// {line}\n");
					break;
				}
				case ScsComment { Value: string text }: {
					foreach (var line in text.Split('\n'))
						Append($"// {line}\n");
					break;
				}
				case ScsRaw { Value: byte[] raw }: {
					for (int i = 0; i < raw.Length; i++) {
						if (i % 16 == 0) {
							if (i > 0)
								Append("\n");
							Append("\thex ");
						}
						Append($" {raw[i]:X02}");
					}
					if (raw.Length > 0)
						Append("\n");
					break;
				}
				default: {
					throw new NotImplementedException(part.GetType().Name);
				}
			}
		}

		string FormatInstruction(Instruction insn) {
			StringBuilder sb = new();
			if (insn.Name == "Eval") {
				ExpressionSyntax.Stringify(sb, insn.Operands[0]);
				sb.Append(";");
			} else {
				sb.Append(insn.Name);
				for (int i = 0; i < insn.Operands.Length; i++) {
					if (i != 0)
						sb.Append(",");
					sb.Append(" ");
					ExpressionSyntax.Stringify(sb, insn.Operands[i]);
				}
			}
			return sb.ToString();
		}

		void Append(string s) {
			foreach (char c in s) {
				if (c == '\n')
					_row++;
			}
			_builder.Append(s);
		}
	}

	class ScsSyntaxParser {
		TextStream _reader;

		public ScsSyntaxParser(TextStream stream) {
			_reader = stream;
		}

		public ScsPart[] Parse() {
			List<ScsPart> parts = new();
			ParseUtils.SkipSpaceComments(_reader);
			while (_reader.Has(0)) {
				parts.Add(ParsePart());
				ParseUtils.SkipSpaceComments(_reader);
			}
			return parts.ToArray();
		}

		ScsPart ParsePart() {
			char ch = _reader.Peek(0);
			if (IsInstructionNameStart(ch)) {
				string name = ParseInstructionName();
				switch (name) {
					case "hex": {
						return new ScsRaw(ParseRaw());
					}
					default: {
				Expression[] operands = ParseOperands();
				var insn = new Instruction {
					Name = name,
					Operands = operands,
				};
				return new ScsInstruction(insn);
					}
				}
			} else if (IsDigit(ch)) {
				int index = ParseNumber();
				if (!ParseUtils.TrySkip(_reader, ':'))
					throw new ParsingException($"Expected ':'");
				ParseUtils.SkipHSpaceComments(_reader);
				if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n'))
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
				return new ScsLabel(index);
			} else if (ParseUtils.TrySkip(_reader, '*')) {
				if (!IsDigit(_reader.Peek(0)))
					throw new ParsingException($"Expected a number");
				int index = ParseNumber();
				if (!ParseUtils.TrySkip(_reader, ':'))
					throw new ParsingException($"Expected ':'");
				ParseUtils.SkipHSpaceComments(_reader);
				if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n'))
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
				return new ScsReturnAddress(index);
			} else if (ch == '$') {
				return ParseEvalInstruction();
			} else {
				throw new ParsingException($"Unexpected character");
			}
		}

		Expression[] ParseOperands() {
			List<Expression> operands = new();
			if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
				if (!ParseUtils.SkipHSpaceComments(_reader))
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
				while (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
					if (operands.Count > 0) {
					if (!ParseUtils.TrySkip(_reader, ','))
						throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
					ParseUtils.SkipHSpaceComments(_reader);
				}
					operands.Add(ExpressionSyntax.Parse(_reader));
					ParseUtils.SkipHSpaceComments(_reader);
				}
			}
			return operands.ToArray();
		}

		byte[] ParseRaw() {
			MemoryStream buffer = new();
			if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
				if (!ParseUtils.SkipHSpaceComments(_reader))
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
				while (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
					buffer.WriteByte(ParseHexByte());
					ParseUtils.SkipHSpaceComments(_reader);
				}
			}
			return buffer.ToArray();
		}

		ScsPart ParseEvalInstruction() {
			var expression = ExpressionSyntax.Parse(_reader);
			ParseUtils.SkipHSpaceComments(_reader);
			if (!ParseUtils.TrySkip(_reader, ';'))
				throw new ParsingException($"Expected ';'");
			ParseUtils.SkipHSpaceComments(_reader);
			if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n'))
				throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
			var instruction = new Instruction {
				Name = "Eval",
				Operands = new Expression[] {
					expression,
				},
			};
			return new ScsInstruction(instruction);
		}

		string ParseInstructionName() {
			Debug.Assert(IsInstructionNameStart(_reader.Peek(0)));
			string s = "";
			while (IsInstructionNamePart(_reader.Peek(0)))
				s += _reader.Next();
			return s;
		}

		int ParseNumber() {
			Debug.Assert(IsDigit(_reader.Peek(0)));
			string s = "";
			while (IsDigit(_reader.Peek(0)))
				s += _reader.Next();
			return int.Parse(s);
		}

		byte ParseHexByte() {
			int high = ParseNibble();
			int low = ParseNibble();
			return (byte)((high << 4) | low);
		}

		int ParseNibble() {
			char ch = _reader.Peek(0);
			if (ch is >= '0' and <= '9')
				return (_reader.Next() - '0') + 0x0;
			if (ch is >= 'A' and <= 'Z')
				return (_reader.Next() - 'A') + 0xA;
			if (ch is >= 'a' and <= 'z')
				return (_reader.Next() - 'a') + 0xA;
			throw new ParsingException($"Unexpected character: {ch}");
		}

		bool IsInstructionNameStart(char c) {
			return c is ('_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'));
		}

		bool IsInstructionNamePart(char c) {
			return IsInstructionNameStart(c) || IsDigit(c);
		}

		bool IsDigit(char c) {
			return c is (>= '0' and <= '9');
		}
	}
}

public abstract class ScsPart {
}

public class ScsInstruction : ScsPart {
	public Instruction Value;
	public int Offset;

	public ScsInstruction(Instruction value) {
		Value = value;
	}
}

public class ScsRaw : ScsPart {
	public byte[] Value;

	public ScsRaw(byte[] value) {
		Value = value;
	}
}

public class ScsComment : ScsPart {
	public string Value;

	public ScsComment(string value) {
		Value = value;
	}
}

public class ScsLabel : ScsPart {
	public int Value;

	public ScsLabel(int value) {
		Value = value;
	}
}

public class ScsReturnAddress : ScsPart {
	public int Value;

	public ScsReturnAddress(int value) {
		Value = value;
	}
}

public class ScsError : ScsPart {
	public int Position;
	public Exception Error;

	public ScsError(int position, Exception error) {
		Position = position;
		Error = error;
	}
}
