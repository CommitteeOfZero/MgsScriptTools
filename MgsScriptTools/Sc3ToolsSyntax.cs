using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

public class Sc3ToolsSyntax : MesSyntax {
	public Sc3ToolsSyntax() { }

	public override void Stringify(StringBuilder builder, MesToken[] tokens) {
		new Sc3ToolsStringifier(builder).Stringify(tokens);
	}

	public override MesToken[] Parse(TextStream reader) {
		return new Sc3ToolsParser(reader).Parse();
	}

	class Sc3ToolsStringifier {
		StringBuilder _builder;

		public Sc3ToolsStringifier(StringBuilder builder) {
			_builder = builder;
		}

		public void Stringify(MesToken[] tokens) {
			foreach (var token in tokens)
				StringifyToken(token);
		}

		void StringifyToken(MesToken token) {
			switch (token) {
				case MesChunk { Value: string chunk, Italic: bool italic }: {
					if (italic)
						chunk = $"<i>{chunk}</i>";
					_builder.Append(chunk);
					break;
				}
				case MesTag tag: {
					StringifyTag(tag);
					break;
				}
				case MesGlyph { Value: int index }: {
					// TODO: find a better solution
					_builder.Append($"<0x{index:X04}>");
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}

		void StringifyTag(MesTag tag) {
			_builder.Append('[');
			_builder.Append(GetTagName(tag.Kind));
			switch (tag.Kind) {
				case MesTagKind.Space: {
					StringifyNumberAttribute("left", tag.Operands[0]);
					break;
				}
				case MesTagKind.LineFloat: {
					StringifyNumberAttribute("top", tag.Operands[0]);
					break;
				}
				case MesTagKind.Color: {
					StringifyCalcAttribute("index", tag.Operands[0]);
					break;
				}
				case MesTagKind.Wait: {
					StringifyNumberAttribute("unk", tag.Operands[0]);
					break;
				}
				case MesTagKind.Size: {
					StringifyNumberAttribute("size", tag.Operands[0]);
					break;
				}
				case MesTagKind.PrintHankaku: {
					StringifyNumberAttribute("index", tag.Operands[0]);
					break;
				}
				case MesTagKind.Dictionary: {
					StringifyNumberAttribute("index", tag.Operands[0]);
					break;
				}
				case MesTagKind.Auto: {
					StringifyNumberAttribute("index", tag.Operands[0]);
					break;
				}
				case MesTagKind.Evaluate: {
					StringifyCalcAttribute("expr", tag.Operands[0]);
					break;
				}
				default: {
					if (tag.Operands.Length != 0)
						throw new NotImplementedException(tag.Kind.ToString());
					break;
				}
			}
			_builder.Append(']');
		}

		void StringifyNumberAttribute(string name, Expression operand) {
			int value = operand.GetInt();
			_builder.Append($" {name}=\"{value}\"");
		}

		void StringifyCalcAttribute(string name, Expression operand) {
			MemoryStream stream = new();
			CalcExpressionEncoding.Encode(stream, operand);
			string value = Convert.ToHexString(stream.ToArray());
			_builder.Append($" {name}=\"{value}\"");
		}

		string GetTagName(MesTagKind kind) {
			return kind switch {
				MesTagKind.Newline => "linebreak",
				MesTagKind.NameStart => "name",
				MesTagKind.NameEnd => "line",
				MesTagKind.PauseEndLine => "%p",
				MesTagKind.Color => "color",
				MesTagKind.E => "%05",

				MesTagKind.Wait => "unk07",
				MesTagKind.PauseEndPage => "%e",
				MesTagKind.RubyStart => "ruby-base",
				MesTagKind.RubyText => "ruby-text-start",
				MesTagKind.RubyEnd => "ruby-text-end",
				MesTagKind.Size => "font",

				MesTagKind.LineSync => "parallel",
				MesTagKind.LineCenter => "center",
				//MesTagKind.LineLight => "",
				MesTagKind.LineFloat => "margin",
				MesTagKind.Space => "margin",
				MesTagKind.PrintHankaku => "hardcoded-value",
				//MesTagKind.PrintZenkaku => "",

				MesTagKind.Dictionary => "unk16",

				MesTagKind.PauseClearPage => "%18",
				MesTagKind.Auto => "unk19",
				//MesTagKind.AutoClearPage => "",
				//MesTagKind.FN => "",

				MesTagKind.RubyCenter => "ruby-center-per-char",
				MesTagKind.Newline_1F => "alt-linebreak",

				//MesTagKind.LineRight => "",

				_ => throw new NotImplementedException(kind.ToString()),
			};
		}
	}

	class Sc3ToolsParser {
		TextStream _reader;

		public Sc3ToolsParser(TextStream reader) {
			_reader = reader;
		}

		public MesToken[] Parse() {
			List<MesToken> tokens = new();
			bool italic = false;
			while (_reader.Has(0) && _reader.Peek(0) != '\n') {
				if (_reader.Peek(0) == '[') {
					var pos = _reader.Tell();
					try {
						tokens.Add(ParseTag());
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
				tokens.Add(new MesChunk(ch.ToString(), italic));
			}
			return tokens.ToArray();
		}

		MesTag ParseTag() {
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
				("linebreak", null) => MesTagKind.Newline,
				("alt-linebreak", null) => MesTagKind.Newline_1F,
				("name", null) => MesTagKind.NameStart,
				("line", null) => MesTagKind.NameEnd,
				("%p", null) => MesTagKind.PauseEndLine,
				("%e", null) => MesTagKind.PauseEndPage,
				("%05", null) => MesTagKind.E,
				("%18", null) => MesTagKind.PauseClearPage,
				("color", "index") => MesTagKind.Color,
				("ruby-base" or "rubybase", null) => MesTagKind.RubyStart,
				("ruby-text-start" or "rubytextstart", null) => MesTagKind.RubyText,
				("ruby-text-end" or "rubytextend", null) => MesTagKind.RubyEnd,
				("ruby-center-per-char", null) => MesTagKind.RubyCenter,
				("parallel", null) => MesTagKind.LineSync,
				("center", null) => MesTagKind.LineCenter,
				("margin", "left") => MesTagKind.Space,
				("margin", "top") => MesTagKind.LineFloat,
				("hardcoded-value" or "hardcodedvalue", "index") => MesTagKind.PrintHankaku,
				("unk19", "index") => MesTagKind.Auto,
				("unk16", "index") => MesTagKind.Dictionary,
				("unk07", "unk") => MesTagKind.Wait,
				//("auto-forward" or "autoforward", null) => MesTagKind.Auto,
				//("auto-forward-1a", null) => MesTagKind.AutoAndClose,
				("evaluate", "expr") => MesTagKind.Evaluate,
				_ => throw new ParsingException($"Unrecognized tag"),
			};

			var operands = kind switch {
				MesTagKind.Color => ParseCalcAttribute(),
				MesTagKind.Space => ParseNumberAttribute(),
				MesTagKind.LineFloat => ParseNumberAttribute(),
				MesTagKind.PrintHankaku => ParseNumberAttribute(),
				MesTagKind.Auto => ParseNumberAttribute(),
				MesTagKind.Dictionary => ParseNumberAttribute(),
				MesTagKind.Wait => ParseNumberAttribute(),
				MesTagKind.Evaluate => ParseCalcAttribute(),
				_ => new Expression[0],
			};

			if (!ParseUtils.TrySkip(_reader, ']'))
				throw new ParsingException($"Expected ']' at {_reader.Tell()}");

			return new(kind, operands);
		}

		Expression[] ParseNumberAttribute() {
			string s = ParseStringAttribute();
			int value = int.Parse(s);
			return new Expression[] {
				new NumberExpression(value),
			};
		}

		Expression[] ParseCalcAttribute() {
			string s = ParseStringAttribute();
			byte[] data = Convert.FromHexString(s);
			MemoryStream stream = new(data);
			return new Expression[] {
				CalcExpressionEncoding.Decode(stream),
			};
		}

		string ParseStringAttribute() {
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
