namespace MgsScriptTools;

public class Instruction {
	public string Name = null!;
	public Expression[] Operands = null!;
}

public class InstructionEncoding {
	Tree<byte, InstructionSpec> _tree;
	Dictionary<string, InstructionSpec> _table;

	InstructionEncoding(Tree<byte, InstructionSpec> tree, Dictionary<string, InstructionSpec> table) {
		_tree = tree;
		_table = table;
	}

	public void Encode(Stream stream, Instruction instruction) {
		var spec = _table[instruction.Name];
		PutBytes(stream, spec.Opcode);
		var kinds = spec.Operands;
		var operands = instruction.Operands;
		if (operands.Length != kinds.Length)
			throw new Exception($"Expected {kinds.Length} arguments, got {operands.Length}");
		for (int i = 0; i < kinds.Length; i++)
			EncodeOperand(stream, kinds[i], operands[i]);
	}

	public Instruction Decode(Stream stream) {
		var spec = DecodeOpcode(stream);
		var kinds = spec.Operands;
		var operands = new Expression[kinds.Length];
		for (int i = 0; i < kinds.Length; i++)
			operands[i] = DecodeOperand(stream, kinds[i]);
		return new Instruction {
			Name = spec.Name,
			Operands = operands,
		};
	}

	void EncodeOperand(Stream stream, OperandKind kind, Expression operand) {
		switch (kind) {
			case OperandKind.Calc: {
				CalcExpressionEncoding.Encode(stream, operand);
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

	void EncodeOperandInt8(Stream stream, Expression expression) {
		int value = expression.GetInt();
		PutByte(stream, (byte)(value >> 00));
	}

	void EncodeOperandInt16(Stream stream, Expression expression) {
		int value = expression.GetInt();
		PutByte(stream, (byte)(value >> 00));
		PutByte(stream, (byte)(value >> 08));
	}

	void EncodeOperandInt32(Stream stream, Expression expression) {
		int value = expression.GetInt();
		PutByte(stream, (byte)(value >> 00));
		PutByte(stream, (byte)(value >> 08));
		PutByte(stream, (byte)(value >> 16));
		PutByte(stream, (byte)(value >> 24));
	}

	void PutByte(Stream stream, byte value) {
		stream.WriteByte(value);
	}

	void PutBytes(Stream stream, byte[] value) {
		stream.Write(value);
	}

	InstructionSpec DecodeOpcode(Stream stream) {
		long start = stream.Position;
		Tree<byte, InstructionSpec> cursor = _tree;
		while (true) {
			var b = GetByte(stream);
			var next = cursor[b];
			if (next is null)
				throw new Exception($"Unrecognized instruction at {start}");
			cursor = next;
			if (cursor.Value is InstructionSpec opcodeSpec)
				return opcodeSpec;
		}
	}

	Expression DecodeOperand(Stream stream, OperandKind kind) {
		return kind switch {
			OperandKind.Calc => CalcExpressionEncoding.Decode(stream),
			OperandKind.Int8 => DecodeOperandInt8(stream),
			OperandKind.Int16 => DecodeOperandInt16(stream),
			OperandKind.Int32 => DecodeOperandInt32(stream),
			_ => throw new NotImplementedException(kind.ToString()),
		};
	}

	Expression DecodeOperandInt8(Stream stream) {
		int value = 0;
		value |= GetByte(stream);
		value = SignExtend(value, 8);
		return new NumberExpression(value);
	}

	Expression DecodeOperandInt16(Stream stream) {
		int value = 0;
		value |= GetByte(stream) << 00;
		value |= GetByte(stream) << 08;
		value = SignExtend(value, 16);
		return new NumberExpression(value);
	}

	Expression DecodeOperandInt32(Stream stream) {
		int value = 0;
		value |= GetByte(stream) << 00;
		value |= GetByte(stream) << 08;
		value |= GetByte(stream) << 16;
		value |= GetByte(stream) << 24;
		return new NumberExpression(value);
	}

	byte GetByte(Stream stream) {
		var value = stream.ReadByte();
		if (value < 0)
			throw new EndOfStreamException();
		return (byte)value;
	}

	int SignExtend(int value, int length) {
		int mask = 1 << (length - 1);
		int sign = value & mask;
		return value | ~(sign - 1);
	}

	public static InstructionEncoding BuildFrom(InstructionSpec[] opcodeSpecs) {
		var tree = BuildOpcodeTree(opcodeSpecs);
		Dictionary<string, InstructionSpec> table = new();
		foreach (var spec in opcodeSpecs) {
			if (table.ContainsKey(spec.Name))
				throw new Exception($"Duplicate instruction name: {spec.Name}");
			table[spec.Name] = spec;
		}
		return new(tree, table);
	}

	static Tree<byte, InstructionSpec> BuildOpcodeTree(InstructionSpec[] specs) {
		Tree<byte, InstructionSpec> tree = new();
		foreach (var spec in specs) {
			byte[] opcode = spec.Opcode;
			if (opcode.Length == 0)
				throw new Exception($"Empty opcode: {spec.Name}");
			var cursor = tree;
			for (int i = 0; i < opcode.Length; i++) {
				if (cursor.HasValue)
					throw new Exception($"Duplicate opcode prefix: {Convert.ToHexString(opcode[..i])}");
				cursor = cursor.CreateBranch(opcode[i]);
			}
			if (cursor.HasBranches)
				throw new Exception($"Duplicate opcode prefix: {Convert.ToHexString(opcode)}");
			if (cursor.HasValue)
				throw new Exception($"Duplicate opcode: {Convert.ToHexString(opcode)}");
			cursor.Value = spec;
		}
		return tree;
	}

	public class InstructionOpcodeTree {
		Dictionary<byte, InstructionOpcodeTree> _branches = new();
		InstructionSpec? _leaf;

		InstructionOpcodeTree() { }

		public InstructionOpcodeTree? GetBranch(byte b) {
			return _branches.GetValueOrDefault(b);
		}

		public InstructionSpec? GetLeaf() {
			return _leaf;
		}

		public static InstructionOpcodeTree BuildFrom(InstructionSpec[] opcodeSpecs) {
			InstructionOpcodeTree tree = new();
			foreach (var opcodeSpec in opcodeSpecs) {
				var node = tree;
				byte[] opcode = opcodeSpec.Opcode;
				if (opcode.Length == 0)
					throw new Exception($"Empty opcode: {opcodeSpec.Name}");
				for (int i = 0; i < opcode.Length; i++) {
					if (node._leaf is not null)
						throw new Exception($"Duplicate opcode prefix: {Convert.ToHexString(opcode[..i])}");
					byte b = opcode[i];
					if (!node._branches.ContainsKey(b))
						node._branches[b] = new();
					node = node._branches[b];
				}
				if (node._branches.Count != 0)
					throw new Exception($"Duplicate opcode prefix: {Convert.ToHexString(opcode)}");
				if (node._leaf is not null)
					throw new Exception($"Duplicate opcode: {Convert.ToHexString(opcode)}");
				node._leaf = opcodeSpec;
			}
			return tree;
		}
	}
}

public class InstructionSpec {
	public string Name = null!;
	public byte[] Opcode = null!;
	public OperandKind[] Operands = null!;
}
