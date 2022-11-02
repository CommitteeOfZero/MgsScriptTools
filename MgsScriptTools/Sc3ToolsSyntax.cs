using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

public class Sc3ToolsSyntax : MstStringSyntax {
	public Sc3ToolsSyntax() { }

	public override void Stringify(StringBuilder builder, MstStringPart[] parts) {
		new Sc3ToolsSyntaxStringifier(builder).Stringify(parts);
	}

	public override MstStringPart[] Parse(TextStream reader) {
		return new Sc3ToolsSyntaxParser(reader).Parse();
	}

	class Sc3ToolsSyntaxStringifier {
		StringBuilder _builder;

		public Sc3ToolsSyntaxStringifier(StringBuilder builder) {
			_builder = builder;
		}

		public void Stringify(MstStringPart[] parts) {
			foreach (var part in parts)
				StringifyPart(part);
		}

		void StringifyPart(MstStringPart part) {
			switch (part) {
				case MstStringChunk { Value: string chunk, Italic: bool italic }: {
					if (italic)
						chunk = $"<i>{chunk}</i>";
					_builder.Append(chunk);
					break;
				}
				case MstStringCommand { Value: MesCommand command }: {
					StringifyCommand(command);
					break;
				}
				case MstStringGlyph { Value: int index }: {
					// TODO: find a better solution
					_builder.Append($"<0x{index:X04}>");
					break;
				}
				default: {
					throw new NotImplementedException(part.GetType().Name);
				}
			}
		}

		void StringifyCommand(MesCommand command) {
			_builder.Append('[');
			_builder.Append(GetCommandName(command.Kind));
			switch (command.Kind) {
				case MesCommandKind.Space: {
					StringifyCommandOperandNumber("left", command.Operands[0]);
					break;
				}
				case MesCommandKind.LineFloat: {
					StringifyCommandOperandNumber("top", command.Operands[0]);
					break;
				}
				case MesCommandKind.Color: {
					StringifyCommandOperandCalc("index", command.Operands[0]);
					break;
				}
				case MesCommandKind.Wait: {
					StringifyCommandOperandNumber("unk", command.Operands[0]);
					break;
				}
				case MesCommandKind.Size: {
					StringifyCommandOperandNumber("size", command.Operands[0]);
					break;
				}
				case MesCommandKind.PrintHankaku: {
					StringifyCommandOperandNumber("index", command.Operands[0]);
					break;
				}
				case MesCommandKind.Dictionary: {
					StringifyCommandOperandNumber("index", command.Operands[0]);
					break;
				}
				case MesCommandKind.Auto: {
					StringifyCommandOperandNumber("index", command.Operands[0]);
					break;
				}
				case MesCommandKind.Evaluate: {
					StringifyCommandOperandCalc("expr", command.Operands[0]);
					break;
				}
				default: {
					if (command.Operands.Length != 0)
						throw new NotImplementedException(command.Kind.ToString());
					break;
				}
			}
			_builder.Append(']');
		}

		void StringifyCommandOperandNumber(string name, Expression operand) {
			int value = operand.GetInt();
			_builder.Append($" {name}=\"{value}\"");
		}

		void StringifyCommandOperandCalc(string name, Expression operand) {
			MemoryStream stream = new();
			CalcExpressionEncoding.Encode(stream, operand);
			string value = Convert.ToHexString(stream.ToArray());
			_builder.Append($" {name}=\"{value}\"");
		}

		string GetCommandName(MesCommandKind kind) {
			return kind switch {
				MesCommandKind.Newline => "linebreak",
				MesCommandKind.NameStart => "name",
				MesCommandKind.NameEnd => "line",
				MesCommandKind.PauseEndLine => "%p",
				MesCommandKind.Color => "color",
				MesCommandKind.E => "%05",

				MesCommandKind.Wait => "unk07",
				MesCommandKind.PauseEndPage => "%e",
				MesCommandKind.RubyStart => "ruby-base",
				MesCommandKind.RubyText => "ruby-text-start",
				MesCommandKind.RubyEnd => "ruby-text-end",
				MesCommandKind.Size => "font",

				MesCommandKind.LineSync => "parallel",
				MesCommandKind.LineCenter => "center",
				//MgsStringCommandKind.LineLight => "",
				MesCommandKind.LineFloat => "margin",
				MesCommandKind.Space => "margin",
				MesCommandKind.PrintHankaku => "hardcoded-value",
				//MgsStringCommandKind.PrintZenkaku => "",

				MesCommandKind.Dictionary => "unk16",

				MesCommandKind.PauseClearPage => "%18",
				MesCommandKind.Auto => "unk19",
				//MgsStringCommandKind.AutoClearPage => "",
				//MgsStringCommandKind.FN => "",

				MesCommandKind.RubyCenter => "ruby-center-per-char",
				MesCommandKind.Newline_1F => "alt-linebreak",

				//MgsStringCommandKind.LineRight => "",

				_ => throw new NotImplementedException(kind.ToString()),
			};
		}
	}

	class Sc3ToolsSyntaxParser {
		TextStream _reader;

		public Sc3ToolsSyntaxParser(TextStream reader) {
			_reader = reader;
		}

		public MstStringPart[] Parse() {
			List<MstStringPart> parts = new();
			bool italic = false;
			while (_reader.Has(0) && _reader.Peek(0) != '\n') {
				if (_reader.Peek(0) == '[') {
					var pos = _reader.Tell();
					try {
						parts.Add(ParseCommand());
						continue;
					} catch (ParsingException) {
						//Console.WriteLine($"Warning: Error occured while trying to parse text tag at {pos}:\n{e}");
						_reader.Seek(pos);
					}
				} else if (ParseUtils.TrySkip(_reader, "<i>")) {
					italic = true;
					continue;
				} else if (ParseUtils.TrySkip(_reader, "</i>")) {
					italic = false;
					continue;
				}
				char ch = _reader.Next();
				parts.Add(new MstStringChunk(ch.ToString(), italic));
			}
			return parts.ToArray();
		}

		MstStringCommand ParseCommand() {
			Debug.Assert(_reader.Peek(0) == '[');
			_reader.Skip(1);

			string name = "";
			while (_reader.Peek(0) is '-' or '%' or (>= '0' and <= '9') or (>= 'a' and <= 'z'))
				name += _reader.Next();
			if (name.Length == 0)
				throw new ParsingException($"Expected tag name");

			string? key = null;
			if (ParseUtils.TrySkip(_reader, ' ')) {
				key = "";
				while (_reader.Peek(0) is >= 'a' and <= 'z')
					key += _reader.Next();
				if (key.Length == 0)
					throw new ParsingException($"Expected attribute key");
				if (!ParseUtils.TrySkip(_reader, '='))
					throw new ParsingException($"Expected '=' or continuation of attribute key");
			}

			var kind = (name, key) switch {
				("linebreak", null) => MesCommandKind.Newline,
				("alt-linebreak", null) => MesCommandKind.Newline_1F,
				("name", null) => MesCommandKind.NameStart,
				("line", null) => MesCommandKind.NameEnd,
				("%p", null) => MesCommandKind.PauseEndLine,
				("%e", null) => MesCommandKind.PauseEndPage,
				("%05", null) => MesCommandKind.E,
				("%18", null) => MesCommandKind.PauseClearPage,
				("color", "index") => MesCommandKind.Color,
				("ruby-base" or "rubybase", null) => MesCommandKind.RubyStart,
				("ruby-text-start" or "rubytextstart", null) => MesCommandKind.RubyText,
				("ruby-text-end" or "rubytextend", null) => MesCommandKind.RubyEnd,
				("ruby-center-per-char", null) => MesCommandKind.RubyCenter,
				("parallel", null) => MesCommandKind.LineSync,
				("center", null) => MesCommandKind.LineCenter,
				("margin", "left") => MesCommandKind.Space,
				("margin", "top") => MesCommandKind.LineFloat,
				("hardcoded-value" or "hardcodedvalue", "index") => MesCommandKind.PrintHankaku,
				("unk19", "index") => MesCommandKind.Auto,
				("unk16", "index") => MesCommandKind.Dictionary,
				("unk07", "unk") => MesCommandKind.Wait,
				//("auto-forward" or "autoforward", null) => MgsStringCommandKind.Auto,
				//("auto-forward-1a", null) => MgsStringCommandKind.AutoAndClose,
				("evaluate", "expr") => MesCommandKind.Evaluate,
				_ => throw new ParsingException($"Unrecognized tag"),
			};

			var operands = kind switch {
				MesCommandKind.Color => ParseCommandOperandsCalc(),
				MesCommandKind.Space => ParseCommandOperandsNumber(),
				MesCommandKind.LineFloat => ParseCommandOperandsNumber(),
				MesCommandKind.PrintHankaku => ParseCommandOperandsNumber(),
				MesCommandKind.Auto => ParseCommandOperandsNumber(),
				MesCommandKind.Dictionary => ParseCommandOperandsNumber(),
				MesCommandKind.Wait => ParseCommandOperandsNumber(),
				MesCommandKind.Evaluate => ParseCommandOperandsCalc(),
				_ => new Expression[0],
			};

			if (!ParseUtils.TrySkip(_reader, ']'))
				throw new ParsingException($"Expected ']' at {_reader.Tell()}");

			MesCommand command = new(kind, operands);
			return new MstStringCommand(command);
		}

		Expression[] ParseCommandOperandsNumber() {
			string s = ParseCommandOperandsString();
			int value = int.Parse(s);
			return new Expression[] {
				new NumberExpression(value),
			};
		}

		Expression[] ParseCommandOperandsCalc() {
			string s = ParseCommandOperandsString();
			byte[] data = Convert.FromHexString(s);
			MemoryStream stream = new(data);
			return new Expression[] {
				CalcExpressionEncoding.Decode(stream),
			};
		}

		string ParseCommandOperandsString() {
			if (!ParseUtils.TrySkip(_reader, '"'))
				throw new ParsingException($"Expected '\"'");
			string value = "";
			while (_reader.Peek(0) is '_' or (>= '0' and <= '9') or (>= 'A' and <= 'Z'))
				value += _reader.Next();
			if (!ParseUtils.TrySkip(_reader, '"'))
				throw new ParsingException($"Expected '\"' or continuation of attribute value");
			return value;
		}
	}
}
