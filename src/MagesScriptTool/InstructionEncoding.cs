using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace MagesScriptTool;

sealed class InstructionEncoding {
	readonly ImmutableTree<byte, VmInstructionSpec> _opcodeTree;
	readonly ImmutableDictionary<string, InstructionSpec> _specTable;

	InstructionEncoding(ImmutableTree<byte, VmInstructionSpec> opcodeTree, ImmutableDictionary<string, InstructionSpec> specTable) {
		_opcodeTree = opcodeTree;
		_specTable = specTable;
	}

	public void Encode(Stream stream, Instruction instruction) {
		InstructionSpec spec = _specTable[instruction.Name];
		if (spec is VmInstructionSpec vmInstructionSpec) {
			PutBytes(stream, vmInstructionSpec.Opcode.AsSpan());
		}
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
		return new (spec.Name, [..operands]);
	}

	public Instruction Decode(Stream stream, string dataDirective) {
		DataDirectiveSpec spec = _specTable.GetValueOrDefault(dataDirective) switch {
			DataDirectiveSpec dataDirectiveSpec => dataDirectiveSpec,
			_ => throw new Exception($"Unrecognized data directive name: {dataDirective}")
		};
		
		ImmutableArray<OperandKind> operandSpecs = spec.Operands;
		List<ExpressionNode> operands = [];
		foreach (OperandKind operandSpec in operandSpecs) {
			operands.Add(DecodeOperand(stream, operandSpec));
		}
		return new (spec.Name, [..operands]);
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
			case OperandKind.UInt16:
			case OperandKind.Int16: {
				EncodeOperandInt16(stream, operand);
				break;
			}
			case OperandKind.Int32: {
				EncodeOperandInt32(stream, operand);
				break;
			}
			case OperandKind.Str: {
				EncodeOperandStr(stream, operand);
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
	
	static void EncodeOperandStr(Stream stream, ExpressionNode expression) {
		string value = expression.GetString();
		PutBytes(stream, new UTF8Encoding(false, true).GetBytes(value + '\0').AsSpan());
	}

	static void PutByte(Stream stream, byte value) {
		stream.WriteByte(value);
	}

	static void PutBytes(Stream stream, ReadOnlySpan<byte> data) {
		stream.Write(data);
	}

	VmInstructionSpec DecodeOpcode(Stream stream) {
		long start = stream.Position;
		ImmutableTree<byte, VmInstructionSpec> cursor = _opcodeTree;
		while (true) {
			byte b = GetByte(stream);
			ImmutableTree<byte, VmInstructionSpec>? next = cursor[b];
			if (next is null) {
				throw new Exception($"Unrecognized instruction at {start}.");
			}
			cursor = next;
			if (cursor.Value is VmInstructionSpec opcodeSpec) {
				return opcodeSpec;
			}
		}
	}

	static ExpressionNode DecodeOperand(Stream stream, OperandKind kind) {
		return kind switch {
			OperandKind.Expr => ExpressionEncoding.Decode(stream),
			OperandKind.Int8 => DecodeOperandInt8(stream),
			OperandKind.UInt16 => DecodeOperandUInt16(stream),
			OperandKind.Int16 => DecodeOperandInt16(stream),
			OperandKind.Int32 => DecodeOperandInt32(stream),
			OperandKind.Str => DecodeOperandStr(stream),
			_ => throw new NotImplementedException(kind.ToString()),
		};
	}
	static ExpressionNodeString DecodeOperandStr(Stream stream) {
		List<byte> buffer = [];
		while (true) {
			byte b = GetByte(stream);
			if (b == 0) break;
			buffer.Add(b);
		}
		return new(new UTF8Encoding(false, true).GetString([.. buffer]));
	}
	
	static ExpressionNodeNumber DecodeOperandInt8(Stream stream) {
		int value = 0;
		value |= GetByte(stream);
		value = SignExtend(value, 8);
		return new(value);
	}

	static ExpressionNodeNumber DecodeOperandUInt16(Stream stream) {
		int value = 0;
		value |= GetByte(stream) << 0;
		value |= GetByte(stream) << 8;
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

	public static InstructionEncoding BuildFrom(ImmutableArray<InstructionSpec> specs) {
		ImmutableTree<byte, VmInstructionSpec> tree = BuildOpcodeTree(specs);
		Dictionary<string, InstructionSpec> table = [];
		foreach (InstructionSpec spec in specs) {
			if (table.ContainsKey(spec.Name)) {
				throw new Exception($"Duplicate instruction name: {spec.Name}.");
			}
			table[spec.Name] = spec;
		}
		return new(tree, table.ToImmutableDictionary());
	}

	static ImmutableTree<byte, VmInstructionSpec> BuildOpcodeTree(ImmutableArray<InstructionSpec> specs) {
		Tree<byte, VmInstructionSpec> tree = new();
		foreach (InstructionSpec spec in specs) {
			if (spec is not VmInstructionSpec) continue;
			VmInstructionSpec vmInstructionSpec = (VmInstructionSpec)spec;
			ImmutableArray<byte> opcode = vmInstructionSpec.Opcode;
			if (opcode.Length == 0) {
				throw new Exception($"Empty opcode: {spec.Name}.");
			}
			Tree<byte, VmInstructionSpec> cursor = tree;
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
			cursor.Value = vmInstructionSpec;
		}
		return tree.ToImmutableTree();
	}
}
