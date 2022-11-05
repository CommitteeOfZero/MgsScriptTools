using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

class ScsStrictSyntax : MstStringSyntax {
	public ScsStrictSyntax() { }

	public override void Stringify(StringBuilder builder, MstStringPart[] parts) {
		new ScsStrictSyntaxStringifier(builder).Stringify(parts);
	}

	public override MstStringPart[] Parse(TextStream reader) {
		return new ScsStrictSyntaxParser(reader).Parse();
	}

	class ScsStrictSyntaxStringifier {
		StringBuilder _builder;

		public ScsStrictSyntaxStringifier(StringBuilder builder) {
			_builder = builder;
		}

		public void Stringify(MstStringPart[] parts) {
			foreach (var part in parts)
				StringifyPart(part);
		}

		void StringifyPart(MstStringPart part) {
			switch (part) {
				case MstStringChunk { Value: string chunk, Italic: bool italic }: {
					chunk = chunk.Replace("\\", "\\\\").Replace("〔", "\\〔").Replace("〕", "\\〕");
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
			switch (command.Kind) {
				case MesCommandKind.NameStart: {
					_builder.Append("〔");
					break;
				}
				case MesCommandKind.NameEnd: {
					_builder.Append("〕");
					break;
				}
				case MesCommandKind.Evaluate: {
					_builder.Append("\\[");
					StringifyOperand(command.Operands[0]);
					_builder.Append("];");
					break;
				}
				default: {
					_builder.Append('\\');
					_builder.Append(GetSymbol(command.Kind));
					if (command.Operands.Length > 1)
						throw new Exception("Cannot stringify a command with more than one operand");
					if (command.Operands.Length == 1) {
						_builder.Append(':');
						StringifyOperand(command.Operands[0]);
					}
					_builder.Append(';');
					break;
				}
			}
		}

		void StringifyOperand(Expression operand) {
			ExpressionSyntax.Stringify(_builder, operand);
		}

		static string GetSymbol(MesCommandKind kind) {
			return kind switch {
				MesCommandKind.Newline => "n",

				MesCommandKind.PauseEndLine => "p",
				MesCommandKind.Color => "c",
				MesCommandKind.E => "e",
				MesCommandKind.K => "k",
				MesCommandKind.Wait => "w",
				MesCommandKind.PauseEndPage => "pe",
				MesCommandKind.RubyStart => "rs",
				MesCommandKind.RubyText => "rt",
				MesCommandKind.RubyEnd => "re",
				MesCommandKind.Size => "s",

				MesCommandKind.LineSync => "ls",
				MesCommandKind.LineCenter => "lc",
				MesCommandKind.LineL => "ll",
				MesCommandKind.LineFloat => "lf",
				MesCommandKind.Space => "sp",
				MesCommandKind.PrintHankaku => "ph",
				MesCommandKind.PrintZenkaku => "pz", // unconfirmed

				MesCommandKind.Dictionary => "dic",

				MesCommandKind.PauseClearPage => "pnc",
				MesCommandKind.Auto => "a",
				MesCommandKind.AutoClearPage => "anc",
				MesCommandKind.FN => "fn",

				MesCommandKind.RubyCenter => "rc", // unconfirmed, possibly also "rm" - mono-ruby, or "rn" - nakatsuke
				MesCommandKind.Newline_1F => "unk1F", // unknown

				MesCommandKind.LineR => "lr",

				_ => throw new NotImplementedException(kind.ToString()),
			};
		}
	}

	class ScsStrictSyntaxParser {
		TextStream _reader;

		public ScsStrictSyntaxParser(TextStream reader) {
			_reader = reader;
		}

		public MstStringPart[] Parse() {
			List<MstStringPart> tokens = new();
			bool italic = false;
			while (_reader.Has(0) && _reader.Peek(0) != '\n') {
				if (_reader.Peek(0) == '〔') {
					_reader.Next();
					MesCommand command = new(MesCommandKind.NameStart, new Expression[0]);
					tokens.Add(new MstStringCommand(command));
				} else if (_reader.Peek(0) == '〕') {
					_reader.Next();
					MesCommand command = new(MesCommandKind.NameEnd, new Expression[0]);
					tokens.Add(new MstStringCommand(command));
				} else if (_reader.Peek(0) == '\\') {
					if (_reader.Has(1) && _reader.Peek(1) is '\\' or '〔' or '〕') {
						_reader.Skip(1);
						tokens.Add(new MstStringChunk(_reader.Next().ToString(), italic));
					} else {
						tokens.Add(ParseCommand());
					}
				} else if (ParseUtils.TrySkip(_reader, "<i>")) {
					italic = true;
				} else if (ParseUtils.TrySkip(_reader, "</i>")) {
					italic = false;
				} else {
					char ch = _reader.Next();
					tokens.Add(new MstStringChunk(ch.ToString(), italic));
				}
			}
			return tokens.ToArray();
		}

		MstStringCommand ParseCommand() {
			Debug.Assert(_reader.Peek(0) == '\\');
			var startPos = _reader.Tell();
			_reader.Skip(1);

			string name = "";
			while (_reader.Peek(0) is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))
				name += _reader.Next();

			Expression[] operands;
			if (ParseUtils.TrySkip(_reader, ':')) {
				operands = new Expression[] {
					ExpressionSyntax.Parse(_reader),
				};
				if (!ParseUtils.TrySkip(_reader, ';'))
					throw new ParsingException($"Expected ';'");
			} else if (ParseUtils.TrySkip(_reader, ';')) {
				operands = new Expression[0];
			} else {
				throw new ParsingException($"Expected ':' or ';'");
			}

			MesCommandKind? kind = name.ToString() switch {
				"n" => MesCommandKind.Newline,

				"p" => MesCommandKind.PauseEndLine,
				"c" => MesCommandKind.Color,
				"e" => MesCommandKind.E,
				"unk06" => MesCommandKind.K,
				"w" => MesCommandKind.Wait,
				"pe" => MesCommandKind.PauseEndPage,
				"rs" => MesCommandKind.RubyStart,
				"rt" => MesCommandKind.RubyText,
				"re" => MesCommandKind.RubyEnd,
				"s" => MesCommandKind.Size,

				"ls" => MesCommandKind.LineSync,
				"lc" => MesCommandKind.LineCenter,
				"ll" => MesCommandKind.LineL,
				"lf" => MesCommandKind.LineFloat,
				"sp" => MesCommandKind.Space,
				"ph" => MesCommandKind.PrintHankaku,
				"pz" => MesCommandKind.PrintZenkaku,

				"dic" => MesCommandKind.Dictionary,

				"pnc" => MesCommandKind.PauseClearPage,
				"a" => MesCommandKind.Auto,
				"anc" => MesCommandKind.AutoClearPage,
				"fn" => MesCommandKind.FN,

				"rc" => MesCommandKind.RubyCenter,
				"unk1F" => MesCommandKind.Newline_1F,

				"lr" => MesCommandKind.LineR,

				_ => null,
			};
			if (kind is null) {
				_reader.Seek(startPos);
				throw new ParsingException($"Unrecognized command: {name}");
			}
			MesCommand command = new(kind.Value, operands);
			return new MstStringCommand(command);
		}
	}
}
