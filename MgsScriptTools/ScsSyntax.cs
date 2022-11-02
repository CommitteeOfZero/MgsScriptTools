using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

public class ScsSyntax {
	public static void Stringify(StringBuilder builder, ScsPart[] parts) {
		new ScsSyntaxStringifier(builder).Stringify(parts);
	}

	public static ScsPart[] Parse(TextStream reader) {
		return new ScsSyntaxParser(reader).Parse();
	}

	class ScsSyntaxStringifier {
		StringBuilder _builder;

		public ScsSyntaxStringifier(StringBuilder builder) {
			_builder = builder;
		}

		public void Stringify(ScsPart[] parts) {
			foreach (var part in parts)
				StringifyPart(part);
		}

		void StringifyPart(ScsPart part) {
			switch (part) {
				case ScsInstruction { Value: Instruction instruction }: {
					if (instruction.Name == "Eval") {
						_builder.Append('\t');
						ExpressionSyntax.Stringify(_builder, instruction.Operands[0]);
						_builder.Append(';');
						_builder.Append('\n');
					} else {
						_builder.Append('\t');
						_builder.Append(instruction.Name);
						for (int i = 0; i < instruction.Operands.Length; i++) {
							if (i != 0)
								_builder.Append(',');
							_builder.Append(' ');
							ExpressionSyntax.Stringify(_builder, instruction.Operands[i]);
						}
						_builder.Append('\n');
					}
					break;
				}
				case ScsLabel { Value: int index }: {
					_builder.Append(index);
					_builder.Append(':');
					_builder.Append('\n');
					break;
				}
				case ScsReturnAddress { Value: int index }: {
					_builder.Append('*');
					_builder.Append(index);
					_builder.Append(':');
					_builder.Append('\n');
					break;
				}
				case ScsError { Position: int position, Error: Exception error }: {
					string message = error.ToString().ReplaceLineEndings("\n");
					message = $"An error has occurred at 0x{position:X}: {message}";
					foreach (var line in message.Split('\n'))
						_builder.Append($"// {line}\n");
					break;
				}
				case ScsComment { Value: string text }: {
					foreach (var line in text.Split('\n'))
						_builder.Append($"// {line}\n");
					break;
				}
				case ScsRaw { Value: byte[] raw }: {
					for (int i = 0; i < raw.Length; i++) {
						if (i % 16 == 0) {
							if (i > 0)
								_builder.Append('\n');
							_builder.Append("\thex ");
						}
						_builder.Append($" {raw[i]:X02}");
					}
					if (raw.Length > 0)
						_builder.Append('\n');
					break;
				}
				default: {
					throw new NotImplementedException(part.GetType().Name);
				}
			}
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
				Expression[] operands = ParseOperands();
				var insn = new Instruction {
					Name = name,
					Operands = operands,
				};
				return new ScsInstruction(insn);
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
				while (true) {
					operands.Add(ExpressionSyntax.Parse(_reader));
					ParseUtils.SkipHSpaceComments(_reader);
					if (!_reader.Has(0) || ParseUtils.TrySkip(_reader, '\n'))
						break;
					if (!ParseUtils.TrySkip(_reader, ','))
						throw new ParsingException($"Unexpected character: {_reader.Peek(0)}");
					ParseUtils.SkipHSpaceComments(_reader);
				}
			}
			return operands.ToArray();
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
