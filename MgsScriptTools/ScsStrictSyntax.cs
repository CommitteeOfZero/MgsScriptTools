using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

class ScsStrictSyntax : MesSyntax {
	public ScsStrictSyntax() { }

	public override void Stringify(StringBuilder builder, MesToken[] parts) {
		new ScsStrictStringifier(builder).Stringify(parts);
	}

	public override MesToken[] Parse(TextStream reader) {
		return new ScsStrictParser(reader).Parse();
	}

	class ScsStrictStringifier {
		StringBuilder _builder;

		public ScsStrictStringifier(StringBuilder builder) {
			_builder = builder;
		}

		public void Stringify(MesToken[] tokens) {
			foreach (var token in tokens)
				StringifyToken(token);
		}

		void StringifyToken(MesToken token) {
			switch (token) {
				case MesChunk { Value: string chunk, Italic: bool italic }: {
					chunk = chunk.Replace("\\", "\\\\").Replace("〔", "\\〔").Replace("〕", "\\〕");
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
			switch (tag.Kind) {
				case MesTagKind.NameStart: {
					_builder.Append("〔");
					break;
				}
				case MesTagKind.NameEnd: {
					_builder.Append("〕");
					break;
				}
				case MesTagKind.Evaluate: {
					_builder.Append("\\[");
					StringifyOperand(tag.Operands[0]);
					_builder.Append("];");
					break;
				}
				default: {
					_builder.Append('\\');
					_builder.Append(GetSymbol(tag.Kind));
					if (tag.Operands.Length > 1)
						throw new Exception("Cannot stringify a tag with more than one operand");
					if (tag.Operands.Length == 1) {
						_builder.Append(':');
						StringifyOperand(tag.Operands[0]);
					}
					_builder.Append(';');
					break;
				}
			}
		}

		void StringifyOperand(Expression operand) {
			ExpressionSyntax.Stringify(_builder, operand);
		}

		static string GetSymbol(MesTagKind kind) {
			return kind switch {
				MesTagKind.Newline => "n",

				MesTagKind.PauseEndLine => "p",
				MesTagKind.Color => "c",
				MesTagKind.E => "e",
				MesTagKind.K => "k",
				MesTagKind.Wait => "w",
				MesTagKind.PauseEndPage => "pe",
				MesTagKind.RubyStart => "rs",
				MesTagKind.RubyText => "rt",
				MesTagKind.RubyEnd => "re",
				MesTagKind.Size => "s",

				MesTagKind.LineSync => "ls",
				MesTagKind.LineCenter => "lc",
				MesTagKind.LineL => "ll",
				MesTagKind.LineFloat => "lf",
				MesTagKind.Space => "sp",
				MesTagKind.PrintHankaku => "ph",
				MesTagKind.PrintZenkaku => "pz",

				MesTagKind.Dictionary => "dic",

				MesTagKind.PauseClearPage => "pnc",
				MesTagKind.Auto => "a",
				MesTagKind.AutoClearPage => "anc",
				MesTagKind.FN => "fn",

				MesTagKind.RubyCenter => "rc", // unconfirmed, possibly also "rm" - mono-ruby, or "rn" - nakatsuke
				MesTagKind.Newline_1F => "unk1F", // unknown

				MesTagKind.LineR => "lr",

				_ => throw new NotImplementedException(kind.ToString()),
			};
		}
	}

	class ScsStrictParser {
		TextStream _reader;

		public ScsStrictParser(TextStream reader) {
			_reader = reader;
		}

		public MesToken[] Parse() {
			List<MesToken> tokens = new();
			bool italic = false;
			while (_reader.Has(0) && _reader.Peek(0) != '\n') {
				if (_reader.Peek(0) == '〔') {
					_reader.Next();
					tokens.Add(new MesTag(MesTagKind.NameStart, new Expression[0]));
				} else if (_reader.Peek(0) == '〕') {
					_reader.Next();
					tokens.Add(new MesTag(MesTagKind.NameEnd, new Expression[0]));
				} else if (_reader.Peek(0) == '\\') {
					if (_reader.Has(1) && _reader.Peek(1) is '\\' or '〔' or '〕') {
						_reader.Skip(1);
						tokens.Add(new MesChunk(_reader.Next().ToString(), italic));
					} else {
						tokens.Add(ParseTag());
					}
				} else if (ParseUtils.TrySkip(_reader, "<i>")) {
					italic = true;
				} else if (ParseUtils.TrySkip(_reader, "</i>")) {
					italic = false;
				} else {
					char ch = _reader.Next();
					tokens.Add(new MesChunk(ch.ToString(), italic));
				}
			}
			return tokens.ToArray();
		}

		MesTag ParseTag() {
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

			MesTagKind? kind = name.ToString() switch {
				"n" => MesTagKind.Newline,

				"p" => MesTagKind.PauseEndLine,
				"c" => MesTagKind.Color,
				"e" => MesTagKind.E,
				"unk06" => MesTagKind.K,
				"w" => MesTagKind.Wait,
				"pe" => MesTagKind.PauseEndPage,
				"rs" => MesTagKind.RubyStart,
				"rt" => MesTagKind.RubyText,
				"re" => MesTagKind.RubyEnd,
				"s" => MesTagKind.Size,

				"ls" => MesTagKind.LineSync,
				"lc" => MesTagKind.LineCenter,
				"ll" => MesTagKind.LineL,
				"lf" => MesTagKind.LineFloat,
				"sp" => MesTagKind.Space,
				"ph" => MesTagKind.PrintHankaku,
				"pz" => MesTagKind.PrintZenkaku,

				"dic" => MesTagKind.Dictionary,

				"pnc" => MesTagKind.PauseClearPage,
				"a" => MesTagKind.Auto,
				"anc" => MesTagKind.AutoClearPage,
				"fn" => MesTagKind.FN,

				"rc" => MesTagKind.RubyCenter,
				"unk1F" => MesTagKind.Newline_1F,

				"lr" => MesTagKind.LineR,

				_ => null,
			};
			if (kind is null) {
				_reader.Seek(startPos);
				throw new ParsingException($"Unrecognized command: {name}");
			}
			return new(kind.Value, operands);
		}
	}
}
