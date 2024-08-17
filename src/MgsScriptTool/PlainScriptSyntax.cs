using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace MgsScriptTool;

static class PlainScriptSyntax {
	public static void Format(StringBuilder builder, StringBuilder? sdbBuilder, ImmutableDictionary<PlainScriptElementInstruction, int> instructionPositions, ImmutableArray<PlainScriptElement> elements) {
		new PlainScriptFormatter(builder, sdbBuilder, instructionPositions, elements).Format();
	}

	public static ImmutableArray<PlainScriptElement> Parse(TextStream reader) {
		return new PlainScriptParser(reader).Parse();
	}

	sealed class PlainScriptFormatter {
		readonly StringBuilder _builder;
		readonly StringBuilder? _sdbBuilder;
		readonly ImmutableDictionary<PlainScriptElementInstruction, int> _instructionPositions;
		readonly ImmutableArray<PlainScriptElement> _elements;

		int _row = 1;

		public PlainScriptFormatter(StringBuilder builder, StringBuilder? sdbBuilder, ImmutableDictionary<PlainScriptElementInstruction, int> instructionPositions, ImmutableArray<PlainScriptElement> elements) {
			_builder = builder;
			_sdbBuilder = sdbBuilder;
			_instructionPositions = instructionPositions;
			_elements = elements;
		}

		public void Format() {
			foreach (PlainScriptElement element in _elements) {
				FormatElement(element);
			}
		}

		void FormatElement(PlainScriptElement genericElement) {
			switch (genericElement) {
				case PlainScriptElementInstruction { Value: Instruction instruction } element: {
					string s = FormatInstruction(instruction);
					if (_sdbBuilder is not null) {
						int position = _instructionPositions[element];
						_sdbBuilder.Append($"{position,6},{_row,6}, {s}\n");
					}
					Append($"\t{s}\n");
					break;
				}
				case PlainScriptElementLabel { Index: int index }: {
					Append($"{index}");
					Append(":");
					Append("\n");
					break;
				}
				case PlainScriptElementReturnLabel { Index: int index }: {
					Append("*");
					Append($"{index}");
					Append(":");
					Append("\n");
					break;
				}
				case PlainScriptElementError { Position: int position, Error: Exception error }: {
					string message = error.ToString().ReplaceLineEndings("\n");
					message = $"An error has occurred at 0x{position:X}: {message}";
					foreach (string line in message.Split('\n')) {
						Append($"// {line}\n");
					}
					break;
				}
				case PlainScriptElementComment { Text: string text }: {
					foreach (string line in text.ReplaceLineEndings("\n").Split('\n')) {
						Append($"// {line}\n");
					}
					break;
				}
				case PlainScriptElementRaw { Data: ImmutableArray<byte> raw }: {
					for (int i = 0; i < raw.Length; i++) {
						if (i % 16 == 0) {
							if (i > 0) {
								Append("\n");
							}
							Append("\thex ");
						}
						Append($" {raw[i]:X02}");
					}
					if (raw.Length > 0) {
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

	sealed class PlainScriptParser {
		readonly TextStream _reader;

		public PlainScriptParser(TextStream stream) {
			_reader = stream;
		}

		public ImmutableArray<PlainScriptElement> Parse() {
			List<PlainScriptElement> parts = [];
			ParseUtils.SkipSpaceComments(_reader);
			while (_reader.Has(0)) {
				parts.Add(ParsePart());
				ParseUtils.SkipSpaceComments(_reader);
			}
			return [..parts];
		}

		PlainScriptElement ParsePart() {
			char ch = _reader.Peek(0);
			if (IsInstructionNameStart(ch)) {
				string name = ParseInstructionName();
				switch (name) {
					case "hex": {
						return new PlainScriptElementRaw(ParseRaw());
					}
					default: {
						ImmutableArray<ExpressionNode> operands = ParseOperands();
						Instruction instruction = new(name, operands);
						return new PlainScriptElementInstruction(instruction);
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
				return new PlainScriptElementLabel(index);
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
				return new PlainScriptElementReturnLabel(index);
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

		PlainScriptElementInstruction ParseEvalInstruction() {
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
