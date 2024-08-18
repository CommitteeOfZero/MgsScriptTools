using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace MgsScriptTool;

static class UncompiledScriptSyntax {
	public static void Format(StringBuilder builder, StringBuilder? sdbBuilder, ImmutableDictionary<UncompiledScriptElementInstruction, int> instructionPositions, ImmutableArray<UncompiledScriptElement> elements) {
		new UncompiledScriptFormatter(builder, sdbBuilder, instructionPositions, elements).Format();
	}

	public static ImmutableArray<UncompiledScriptElement> Parse(TextStream reader) {
		return new UncompiledScriptParser(reader).Parse();
	}

	sealed class UncompiledScriptFormatter {
		readonly StringBuilder _builder;
		readonly StringBuilder? _sdbBuilder;
		readonly ImmutableDictionary<UncompiledScriptElementInstruction, int> _instructionPositions;
		readonly ImmutableArray<UncompiledScriptElement> _elements;

		int _row = 1;

		public UncompiledScriptFormatter(StringBuilder builder, StringBuilder? sdbBuilder, ImmutableDictionary<UncompiledScriptElementInstruction, int> instructionPositions, ImmutableArray<UncompiledScriptElement> elements) {
			_builder = builder;
			_sdbBuilder = sdbBuilder;
			_instructionPositions = instructionPositions;
			_elements = elements;
		}

		public void Format() {
			foreach (UncompiledScriptElement element in _elements) {
				FormatElement(element);
			}
		}

		void FormatElement(UncompiledScriptElement genericElement) {
			switch (genericElement) {
				case UncompiledScriptElementInstruction { Value: Instruction instruction } element: {
					string s = FormatInstruction(instruction);
					if (_sdbBuilder is not null) {
						int position = _instructionPositions[element];
						_sdbBuilder.Append($"{position,6},{_row,6}, {s}\n");
					}
					Append($"\t{s}\n");
					break;
				}
				case UncompiledScriptElementLabel { Index: int index }: {
					Append($"{index}");
					Append(":");
					Append("\n");
					break;
				}
				case UncompiledScriptElementReturnLabel { Index: int index }: {
					Append("*");
					Append($"{index}");
					Append(":");
					Append("\n");
					break;
				}
				case UncompiledScriptElementError { Position: int position, Error: Exception error }: {
					string message = error.ToString().ReplaceLineEndings("\n");
					message = $"An error has occurred at 0x{position:X}: {message}";
					foreach (string line in message.Split('\n')) {
						Append($"// {line}\n");
					}
					break;
				}
				case UncompiledScriptElementComment { Text: string text }: {
					foreach (string line in text.ReplaceLineEndings("\n").Split('\n')) {
						Append($"// {line}\n");
					}
					break;
				}
				case UncompiledScriptElementRaw { Data: ImmutableArray<byte> data }: {
					for (int i = 0; i < data.Length; i++) {
						if (i % 16 == 0) {
							if (i > 0) {
								Append("\n");
							}
							Append("\thex ");
						}
						Append($" {data[i]:X02}");
					}
					if (data.Length > 0) {
						Append("\n");
					}
					break;
				}
				default: {
					throw new NotImplementedException(genericElement.GetType().Name);
				}
			}
		}

		string FormatInstruction(Instruction instruction) {
			StringBuilder sb = new();
			if (instruction.Name == "Eval") {
				ExpressionSyntax.Format(sb, instruction.Operands[0]);
				sb.Append(";");
			} else {
				sb.Append(instruction.Name);
				for (int i = 0; i < instruction.Operands.Length; i++) {
					if (i != 0) {
						sb.Append(",");
					}
					sb.Append(" ");
					ExpressionSyntax.Format(sb, instruction.Operands[i]);
				}
			}
			return sb.ToString();
		}

		void Append(string s) {
			foreach (char c in s) {
				if (c == '\n') {
					_row++;
				}
			}
			_builder.Append(s);
		}
	}

	sealed class UncompiledScriptParser {
		readonly TextStream _reader;

		public UncompiledScriptParser(TextStream stream) {
			_reader = stream;
		}

		public ImmutableArray<UncompiledScriptElement> Parse() {
			List<UncompiledScriptElement> elements = [];
			ParseUtils.SkipSpaceComments(_reader);
			while (_reader.Has(0)) {
				elements.Add(ParseElement());
				ParseUtils.SkipSpaceComments(_reader);
			}
			return [..elements];
		}

		UncompiledScriptElement ParseElement() {
			char ch = _reader.Peek(0);
			if (IsInstructionNameStart(ch)) {
				string name = ParseInstructionName();
				switch (name) {
					case "hex": {
						return new UncompiledScriptElementRaw(ParseRaw());
					}
					default: {
						ImmutableArray<ExpressionNode> operands = ParseOperands();
						Instruction instruction = new(name, operands);
						return new UncompiledScriptElementInstruction(instruction);
					}
				}
			} else if (IsDigit(ch)) {
				int index = ParseNumber();
				if (!ParseUtils.TrySkip(_reader, ':')) {
					throw new ParsingException("Expected ':'.");
				}
				ParseUtils.SkipHSpaceComments(_reader);
				if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}.");
				}
				return new UncompiledScriptElementLabel(index);
			} else if (ParseUtils.TrySkip(_reader, '*')) {
				if (!IsDigit(_reader.Peek(0))) {
					throw new ParsingException("Expected a number.");
				}
				int index = ParseNumber();
				if (!ParseUtils.TrySkip(_reader, ':')) {
					throw new ParsingException("Expected ':'.");
				}
				ParseUtils.SkipHSpaceComments(_reader);
				if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
					throw new ParsingException("Unexpected character: {_reader.Peek(0)}.");
				}
				return new UncompiledScriptElementReturnLabel(index);
			} else if (ch == '$') {
				return ParseEvalInstruction();
			} else {
				throw new ParsingException("Unexpected character: {ch}.");
			}
		}

		ImmutableArray<ExpressionNode> ParseOperands() {
			List<ExpressionNode> operands = [];
			if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
				if (!ParseUtils.SkipHSpaceComments(_reader)) {
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}.");
				}
				while (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
					if (operands.Count > 0) {
						if (!ParseUtils.TrySkip(_reader, ',')) {
							throw new ParsingException($"Unexpected character: {_reader.Peek(0)}.");
						}
						ParseUtils.SkipHSpaceComments(_reader);
					}
					operands.Add(ExpressionSyntax.Parse(_reader));
					ParseUtils.SkipHSpaceComments(_reader);
				}
			}
			return [..operands];
		}

		ImmutableArray<byte> ParseRaw() {
			MemoryStream buffer = new();
			if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
				if (!ParseUtils.SkipHSpaceComments(_reader)) {
					throw new ParsingException($"Unexpected character: {_reader.Peek(0)}.");
				}
				while (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
					buffer.WriteByte(ParseHexByte());
					ParseUtils.SkipHSpaceComments(_reader);
				}
			}
			return [..buffer.ToArray()];
		}

		UncompiledScriptElementInstruction ParseEvalInstruction() {
			ExpressionNode expression = ExpressionSyntax.Parse(_reader);
			ParseUtils.SkipHSpaceComments(_reader);
			if (!ParseUtils.TrySkip(_reader, ';')) {
				throw new ParsingException("Expected ';'.");
			}
			ParseUtils.SkipHSpaceComments(_reader);
			if (_reader.Has(0) && !ParseUtils.TrySkip(_reader, '\n')) {
				throw new ParsingException($"Unexpected character: {_reader.Peek(0)}.");
			}
			Instruction instruction = new("Eval", [expression]);
			return new(instruction);
		}

		string ParseInstructionName() {
			Debug.Assert(IsInstructionNameStart(_reader.Peek(0)));
			string s = "";
			while (IsInstructionNamePart(_reader.Peek(0))) {
				s += _reader.Next();
			}
			return s;
		}

		int ParseNumber() {
			Debug.Assert(IsDigit(_reader.Peek(0)));
			string s = "";
			while (IsDigit(_reader.Peek(0))) {
				s += _reader.Next();
			}
			return int.Parse(s);
		}

		byte ParseHexByte() {
			int high = ParseNibble();
			int low = ParseNibble();
			return (byte)((high << 4) | low);
		}

		int ParseNibble() {
			char ch = _reader.Peek(0);
			return ch switch {
				>= '0' and <= '9' => _reader.Next() - '0' + 0x0,
				>= 'A' and <= 'Z' => _reader.Next() - 'A' + 0xA,
				>= 'a' and <= 'z' => _reader.Next() - 'a' + 0xA,
				_ => throw new ParsingException($"Unexpected character: {ch}."),
			};
		}

		static bool IsInstructionNameStart(char c) {
			return c is '_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
		}

		static bool IsInstructionNamePart(char c) {
			return IsInstructionNameStart(c) || IsDigit(c);
		}

		static bool IsDigit(char c) {
			return c is >= '0' and <= '9';
		}
	}
}
