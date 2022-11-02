namespace MgsScriptTools;

class ScsCompiler {
	InstructionEncoding _instructionEncoding;
	MemoryStream _stream;
	SortedDictionary<int, int> _labelTable;
	SortedDictionary<int, int> _returnAddressTable;

	public ScsCompiler(InstructionEncoding instructionEncoding) {
		_instructionEncoding = instructionEncoding;
		_stream = new();
		_labelTable = new();
		_returnAddressTable = new();
	}

	public Sc3File Compile(ScsPart[] parts) {
		foreach (var part in parts)
			ProcessPart(part);

		List<int> labels = new();
		foreach (var (index, offset) in _labelTable) {
			while (index < labels.Count)
				labels.Add((int)_stream.Length);
			labels.Add(offset);
		}

		List<int> returnAddresses = new();
		foreach (var (index, offset) in _returnAddressTable) {
			while (index < returnAddresses.Count)
				returnAddresses.Add((int)_stream.Length);
			returnAddresses.Add(offset);
		}

		return new Sc3File {
			Code = _stream.ToArray(),
			Labels = labels.ToArray(),
			ReturnAddresses = returnAddresses.ToArray(),
		};
	}

	void ProcessPart(ScsPart part) {
		switch (part) {
			case ScsInstruction { Value: var instruction }: {
				ProcessInstruction(instruction);
				break;
			}
			case ScsLabel { Value: var index }: {
				int offset = (int)_stream.Position;
				if (_labelTable.ContainsKey(index))
					throw new Exception($"Conflicting label: {index}");
				_labelTable[index] = offset;
				break;
			}
			case ScsReturnAddress { Value: var index }: {
				int offset = (int)_stream.Position;
				if (_returnAddressTable.ContainsKey(index))
					throw new Exception($"Conflicting return address: {index}");
				_returnAddressTable[index] = offset;
				break;
			}
			case ScsRaw { Value: byte[] raw }: {
				_stream.Write(raw);
				break;
			}
		}
	}

	void ProcessInstruction(Instruction instruction) {
		switch (instruction.Name.ToLowerInvariant()) {
			case "dw" or "adr": {
				foreach (var operand in instruction.Operands)
					EncodeInt16(operand);
				break;
			}
			case "dd" or "stringid": {
				foreach (var operand in instruction.Operands)
					EncodeInt32(operand);
				break;
			}
			default: {
				_instructionEncoding.Encode(_stream, instruction);
				break;
			}
		}
	}

	void EncodeInt16(Expression expression) {
		int value = expression.GetInt();
		PutByte((byte)((value >> 00) & 0xFF));
		PutByte((byte)((value >> 08) & 0xFF));
	}

	void EncodeInt32(Expression expression) {
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
