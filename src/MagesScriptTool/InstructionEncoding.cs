using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class InstructionEncoding {
	readonly ImmutableTree<byte, InstructionSpec> _tree;
	readonly ImmutableDictionary<string, InstructionSpec> _table;

	InstructionEncoding(ImmutableTree<byte, InstructionSpec> tree, ImmutableDictionary<string, InstructionSpec> table) {
		_tree = tree;
		_table = table;
	}

	public void Encode(Stream stream, Instruction instruction) {
		InstructionSpec spec = _table[instruction.Name];
		PutBytes(stream, spec.Opcode.AsSpan());
		ImmutableArray<OperandKind> kinds = spec.Operands;
		ImmutableArray<ExpressionNode> operands = instruction.Operands;
		if (operands.Length != kinds.Length) {
			throw new Exception($"Expected {kinds.Length} arguments, got {operands.Length}.");
		}
		for (int i = 0; i < kinds.Length; i++) {
			EncodeOperand(stream, kinds[i], operands[i]);
		}
	}

	public Instruction Decode(Stream stream) {
		InstructionSpec spec = DecodeOpcode(stream);
		ImmutableArray<OperandKind> operandSpecs = spec.Operands;
		List<ExpressionNode> operands = [];
		foreach (OperandKind operandSpec in operandSpecs) {
			operands.Add(DecodeOperand(stream, operandSpec));
		}
		return new(spec.Name, [..operands]);
	}

	static void EncodeOperand(Stream stream, OperandKind kind, ExpressionNode operand) {
		switch (kind) {
			case OperandKind.Expr: {
				ExpressionEncoding.Encode(stream, operand);
				break;
			}
			case OperandKind.Int8: {
				EncodeOperandInt8(stream, operand);
				break;
			}
			case OperandKind.Int16: {
				EncodeOperandInt16(stream, operand);
				break;
			}
			case OperandKind.Int32: {
				EncodeOperandInt32(stream, operand);
				break;
			}
			default: {
				throw new NotImplementedException(kind.ToString());
			}
		};
	}

	static void EncodeOperandInt8(Stream stream, ExpressionNode expression) {
		int value = expression.GetInt();
		PutByte(stream, (byte)(value >> 0));
	}

	static void EncodeOperandInt16(Stream stream, ExpressionNode expression) {
		int value = expression.GetInt();
		PutByte(stream, (byte)(value >> 0));
		PutByte(stream, (byte)(value >> 8));
	}

	static void EncodeOperandInt32(Stream stream, ExpressionNode expression) {
		int value = expression.GetInt();
		PutByte(stream, (byte)(value >>  0));
		PutByte(stream, (byte)(value >>  8));
		PutByte(stream, (byte)(value >> 16));
		PutByte(stream, (byte)(value >> 24));
	}

	static void PutByte(Stream stream, byte value) {
		stream.WriteByte(value);
	}

	static void PutBytes(Stream stream, ReadOnlySpan<byte> data) {
		stream.Write(data);
	}

	InstructionSpec DecodeOpcode(Stream stream) {
		long start = stream.Position;
		ImmutableTree<byte, InstructionSpec> cursor = _tree;
		while (true) {
			byte b = GetByte(stream);
			ImmutableTree<byte, InstructionSpec>? next = cursor[b];
			if (next is null) {
				throw new Exception($"Unrecognized instruction at {start}.");
			}
			cursor = next;
			if (cursor.Value is InstructionSpec opcodeSpec) {
				return opcodeSpec;
			}
		}
	}

	static ExpressionNode DecodeOperand(Stream stream, OperandKind kind) {
		return kind switch {
			OperandKind.Expr => ExpressionEncoding.Decode(stream),
			OperandKind.Int8 => DecodeOperandInt8(stream),
			OperandKind.Int16 => DecodeOperandInt16(stream),
			OperandKind.Int32 => DecodeOperandInt32(stream),
			_ => throw new NotImplementedException(kind.ToString()),
		};
	}

	static ExpressionNodeNumber DecodeOperandInt8(Stream stream) {
		int value = 0;
		value |= GetByte(stream);
		value = SignExtend(value, 8);
		return new(value);
	}

	static ExpressionNodeNumber DecodeOperandInt16(Stream stream) {
		int value = 0;
		value |= GetByte(stream) << 0;
		value |= GetByte(stream) << 8;
		value = SignExtend(value, 16);
		return new(value);
	}

	static ExpressionNodeNumber DecodeOperandInt32(Stream stream) {
		int value = 0;
		value |= GetByte(stream) <<  0;
		value |= GetByte(stream) <<  8;
		value |= GetByte(stream) << 16;
		value |= GetByte(stream) << 24;
		return new(value);
	}

	static byte GetByte(Stream stream) {
		int value = stream.ReadByte();
		if (value < 0) {
			throw new EndOfStreamException();
		}
		return (byte)value;
	}

	static int SignExtend(int value, int length) {
		int mask = 1 << (length - 1);
		int sign = value & mask;
		return value | ~(sign - 1);
	}

	public static InstructionEncoding BuildFrom(ImmutableArray<InstructionSpec> opcodeSpecs) {
		ImmutableTree<byte, InstructionSpec> tree = BuildOpcodeTree(opcodeSpecs);
		Dictionary<string, InstructionSpec> table = [];
		foreach (InstructionSpec spec in opcodeSpecs) {
			if (table.ContainsKey(spec.Name)) {
				throw new Exception($"Duplicate instruction name: {spec.Name}.");
			}
			table[spec.Name] = spec;
		}
		return new(tree, table.ToImmutableDictionary());
	}

	static ImmutableTree<byte, InstructionSpec> BuildOpcodeTree(ImmutableArray<InstructionSpec> specs) {
		Tree<byte, InstructionSpec> tree = new();
		foreach (InstructionSpec spec in specs) {
			ImmutableArray<byte> opcode = spec.Opcode;
			if (opcode.Length == 0) {
				throw new Exception($"Empty opcode: {spec.Name}.");
			}
			Tree<byte, InstructionSpec> cursor = tree;
			for (int i = 0; i < opcode.Length; i++) {
				if (cursor.HasValue) {
					throw new Exception($"Duplicate opcode prefix: {Convert.ToHexString(opcode.AsSpan()[..i])}.");
				}
				cursor = cursor.EnsureBranch(opcode[i]);
			}
			if (cursor.HasBranches) {
				throw new Exception($"Duplicate opcode prefix: {Convert.ToHexString(opcode.AsSpan())}.");
			}
			if (cursor.HasValue) {
				throw new Exception($"Duplicate opcode: {Convert.ToHexString(opcode.AsSpan())}.");
			}
			cursor.Value = spec;
		}
		return tree.ToImmutableTree();
	}
}
