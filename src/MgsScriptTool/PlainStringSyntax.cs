using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace MgsScriptTool;

sealed class PlainStringSyntax {
	public PlainStringSyntax() { }

	public void Format(StringBuilder builder, ImmutableArray<StringToken> tokens) {
		new PlainStringFormatter(builder, tokens).Format();
	}

	public ImmutableArray<StringToken> Parse(TextStream reader) {
		return new PlainStringParser(reader).Parse();
	}

	sealed class PlainStringFormatter {
		readonly StringBuilder _builder;
		readonly ImmutableArray<StringToken> _tokens;

		public PlainStringFormatter(StringBuilder builder, ImmutableArray<StringToken> tokens) {
			_builder = builder;
			_tokens = tokens;
		}

		public void Format() {
			foreach (StringToken token in _tokens) {
				FormatToken(token);
			}
		}

		void FormatToken(StringToken token) {
			switch (token) {
				case StringTokenChunk { Value: string chunk, Italic: bool italic }: {
					chunk = chunk.Replace("\\", "\\\\").Replace("〔", "\\〔").Replace("〕", "\\〕");
					if (italic) {
						chunk = $"<i>{chunk}</i>";
					}
					_builder.Append(chunk);
					break;
				}
				case StringTokenTag tag: {
					FormatTag(tag);
					break;
				}
				case StringTokenGlyph { Value: int index }: {
					// TODO: find a better solution
					_builder.Append($"<0x{index:X04}>");
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}

		void FormatTag(StringTokenTag tag) {
			switch (tag.Kind) {
				case StringTagKind.NameStart: {
					_builder.Append("〔");
					break;
				}
				case StringTagKind.NameEnd: {
					_builder.Append("〕");
					break;
				}
				case StringTagKind.Evaluate: {
					_builder.Append("\\[");
					FormatOperand(tag.Operands[0]);
					_builder.Append("];");
					break;
				}
				default: {
					_builder.Append('\\');
					_builder.Append(GetSymbol(tag.Kind));
					if (tag.Operands.Length > 1) {
						throw new Exception("Cannot stringify a tag with more than one operand.");
					}
					if (tag.Operands.Length == 1) {
						_builder.Append(':');
						FormatOperand(tag.Operands[0]);
					}
					_builder.Append(';');
					break;
				}
			}
		}

		void FormatOperand(ExpressionNode operand) {
			ExpressionSyntax.Format(_builder, operand);
		}

		static string GetSymbol(StringTagKind kind) {
			return kind switch {
				StringTagKind.Newline => "n",

				StringTagKind.PauseEndLine => "p",
				StringTagKind.Color => "c",
				StringTagKind.E => "e",
				StringTagKind.K => "k",
				StringTagKind.Wait => "w",
				StringTagKind.PauseEndPage => "pe",
				StringTagKind.RubyStart => "rs",
				StringTagKind.RubyText => "rt",
				StringTagKind.RubyEnd => "re",
				StringTagKind.Size => "s",

				StringTagKind.LineSync => "ls",
				StringTagKind.LineCenter => "lc",
				StringTagKind.LineL => "ll",
				StringTagKind.LineFloat => "lf",
				StringTagKind.Space => "sp",
				StringTagKind.PrintHankaku => "ph",
				StringTagKind.PrintZenkaku => "pz",

				StringTagKind.Dictionary => "dic",

				StringTagKind.PauseClearPage => "pnc",
				StringTagKind.Auto => "a",
				StringTagKind.AutoClearPage => "anc",
				StringTagKind.FN => "fn",

				StringTagKind.RubyCenter => "rc", // unconfirmed, possibly also "rm" - mono-ruby, or "rn" - nakatsuke
				StringTagKind.Newline_1F => "unk1F", // unknown

				StringTagKind.LineR => "lr",

				_ => throw new NotImplementedException(kind.ToString()),
			};
		}
	}

	sealed class PlainStringParser {
		readonly TextStream _reader;

		public PlainStringParser(TextStream reader) {
			_reader = reader;
		}

		public ImmutableArray<StringToken> Parse() {
			List<StringToken> tokens = [];
			bool italic = false;
			while (_reader.Has(0) && _reader.Peek(0) != '\n') {
				if (_reader.Peek(0) == '〔') {
					_reader.Next();
					tokens.Add(new StringTokenTag(StringTagKind.NameStart, []));
				} else if (_reader.Peek(0) == '〕') {
					_reader.Next();
					tokens.Add(new StringTokenTag(StringTagKind.NameEnd, []));
				} else if (_reader.Peek(0) == '\\') {
					if (_reader.Has(1) && _reader.Peek(1) is '\\' or '〔' or '〕') {
						_reader.Skip(1);
						tokens.Add(new StringTokenChunk(_reader.Next().ToString(), italic));
					} else {
						tokens.Add(ParseTag());
					}
				} else if (ParseUtils.TrySkip(_reader, "<i>")) {
					italic = true;
				} else if (ParseUtils.TrySkip(_reader, "</i>")) {
					italic = false;
				} else {
					char ch = _reader.Next();
					tokens.Add(new StringTokenChunk(ch.ToString(), italic));
				}
			}
			return [..tokens];
		}

		StringTokenTag ParseTag() {
			Debug.Assert(_reader.Peek(0) == '\\');
			TextStream.Position startPos = _reader.Tell();
			_reader.Skip(1);

			string name = "";
			while (_reader.Peek(0) is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_') {
				name += _reader.Next();
			}

			ImmutableArray<ExpressionNode> operands;
			if (ParseUtils.TrySkip(_reader, ':')) {
				operands = [ExpressionSyntax.Parse(_reader)];
				if (!ParseUtils.TrySkip(_reader, ';')) {
					throw new ParsingException("Expected ';'.");
				}
			} else if (ParseUtils.TrySkip(_reader, ';')) {
				operands = [];
			} else {
				throw new ParsingException("Expected ':' or ';'.");
			}

			StringTagKind? kind = name.ToString() switch {
				"n" => StringTagKind.Newline,

				"p" => StringTagKind.PauseEndLine,
				"c" => StringTagKind.Color,
				"e" => StringTagKind.E,
				"k" => StringTagKind.K,
				"w" => StringTagKind.Wait,
				"pe" => StringTagKind.PauseEndPage,
				"rs" => StringTagKind.RubyStart,
				"rt" => StringTagKind.RubyText,
				"re" => StringTagKind.RubyEnd,
				"s" => StringTagKind.Size,

				"ls" => StringTagKind.LineSync,
				"lc" => StringTagKind.LineCenter,
				"ll" => StringTagKind.LineL,
				"lf" => StringTagKind.LineFloat,
				"sp" => StringTagKind.Space,
				"ph" => StringTagKind.PrintHankaku,
				"pz" => StringTagKind.PrintZenkaku,

				"dic" => StringTagKind.Dictionary,

				"pnc" => StringTagKind.PauseClearPage,
				"a" => StringTagKind.Auto,
				"anc" => StringTagKind.AutoClearPage,
				"fn" => StringTagKind.FN,

				"rc" => StringTagKind.RubyCenter,
				"unk1F" => StringTagKind.Newline_1F,

				"lr" => StringTagKind.LineR,

				_ => null,
			};
			if (kind is null) {
				_reader.Seek(startPos);
				throw new ParsingException($"Unrecognized command: {name}.");
			}
			return new(kind.Value, operands);
		}
	}
}
