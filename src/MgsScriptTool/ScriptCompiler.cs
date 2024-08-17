using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class ScriptCompiler {
	readonly InstructionEncoding _instructionEncoding;

	readonly MemoryStream _stream = new();
	readonly SortedDictionary<int, int> _labelTable = [];
	readonly SortedDictionary<int, int> _returnLabelTable = [];

	public ScriptCompiler(InstructionEncoding instructionEncoding) {
		_instructionEncoding = instructionEncoding;
	}

	public RawScript Compile(ImmutableArray<PlainScriptElement> elements) {
		foreach (PlainScriptElement element in elements) {
			ProcessElement(element);
		}

		List<int> labels = [];
		foreach ((int index, int offset) in _labelTable) {
			while (labels.Count < index) {
				labels.Add(checked((int)_stream.Length));
			}
			labels.Add(offset);
		}

		List<int> returnLabels = [];
		foreach ((int index, int offset) in _returnLabelTable) {
			while (returnLabels.Count < index) {
				returnLabels.Add(checked((int)_stream.Length));
			}
			returnLabels.Add(offset);
		}

		return new([.._stream.ToArray()], [..labels], [..returnLabels]);
	}

	void ProcessElement(PlainScriptElement part) {
		switch (part) {
			case PlainScriptElementInstruction { Value: Instruction instruction }: {
				ProcessInstruction(instruction);
				break;
			}
			case PlainScriptElementLabel { Index: int index }: {
				int offset = (int)_stream.Position;
				if (_labelTable.ContainsKey(index)) {
					throw new Exception($"Conflicting label: {index}.");
				}
				_labelTable[index] = offset;
				break;
			}
			case PlainScriptElementReturnLabel { Index: int index }: {
				int offset = (int)_stream.Position;
				if (_returnLabelTable.ContainsKey(index)) {
					throw new Exception($"Conflicting return label: {index}.");
				}
				_returnLabelTable[index] = offset;
				break;
			}
			case PlainScriptElementRaw { Data: ImmutableArray<byte> raw }: {
				_stream.Write(raw.AsSpan());
				break;
			}
			default: {
				throw new NotImplementedException(part.GetType().Name);
			}
		}
	}

	void ProcessInstruction(Instruction instruction) {
		switch (instruction.Name.ToLowerInvariant()) {
			case "dw" or "adr": {
				foreach (ExpressionNode operand in instruction.Operands) {
					EncodeInt16(operand);
				}
				break;
			}
			case "dd" or "stringid": {
				foreach (ExpressionNode operand in instruction.Operands) {
					EncodeInt32(operand);
				}
				break;
			}
			default: {
				_instructionEncoding.Encode(_stream, instruction);
				break;
			}
		}
	}

	void EncodeInt16(ExpressionNode expression) {
		int value = expression.GetInt();
		PutByte((byte)((value >> 00) & 0xFF));
		PutByte((byte)((value >> 08) & 0xFF));
	}

	void EncodeInt32(ExpressionNode expression) {
		int value = expression.GetInt();
		PutByte((byte)((value >> 00) & 0xFF));
		PutByte((byte)((value >> 08) & 0xFF));
		PutByte((byte)((value >> 16) & 0xFF));
		PutByte((byte)((value >> 24) & 0xFF));
	}

	void PutByte(byte value) {
		_stream.WriteByte(value);
	}
}
