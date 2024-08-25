using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace MagesScriptTool;

sealed class UncompiledStringSyntax {
	public UncompiledStringSyntax() { }

	public void Format(StringBuilder builder, ImmutableArray<StringToken> tokens) {
		new UncompiledStringFormatter(builder, tokens).Format();
	}

	public ImmutableArray<StringToken> Parse(TextStream reader) {
		return new UncompiledStringParser(reader).Parse();
	}

	sealed class UncompiledStringFormatter {
		readonly StringBuilder _builder;
		readonly ImmutableArray<StringToken> _tokens;

		public UncompiledStringFormatter(StringBuilder builder, ImmutableArray<StringToken> tokens) {
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
					_builder.Append($"\\glyph:0x{index:X04};");
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}

		void FormatTag(StringTokenTag tag) {
			switch (tag.Name) {
				case "nameStart": {
					_builder.Append("〔");
					break;
				}
				case "nameEnd": {
					_builder.Append("〕");
					break;
				}
				default: {
					_builder.Append('\\');
					_builder.Append(tag.Name);
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
	}

	sealed class UncompiledStringParser {
		readonly TextStream _reader;

		public UncompiledStringParser(TextStream reader) {
			_reader = reader;
		}

		public ImmutableArray<StringToken> Parse() {
			List<StringToken> tokens = [];
			bool italic = false;
			while (_reader.Has(0) && _reader.Peek(0) != '\n') {
				if (_reader.Peek(0) == '〔') {
					_reader.Next();
					tokens.Add(new StringTokenTag("nameStart", []));
				} else if (_reader.Peek(0) == '〕') {
					_reader.Next();
					tokens.Add(new StringTokenTag("nameEnd", []));
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

		StringToken ParseTag() {
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

			if (name == "glyph") {
				if (operands.Length != 1) {
					throw new ParsingException("Expected a single operand.");
				}
				return new StringTokenGlyph(operands[0].GetInt());
			}

			return new StringTokenTag(name, operands);
		}
	}
}
