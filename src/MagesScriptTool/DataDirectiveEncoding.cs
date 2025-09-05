using System.Collections.Immutable;
using System.Diagnostics;

namespace MagesScriptTool;

sealed class DataDirectiveEncoding {
	readonly DataDirectivesSpec _spec;

	public DataDirectiveEncoding(DataDirectivesSpec spec) {
		_spec = spec;
	}

	public void Encode(Stream stream, DataDirective dataDirective) {
		DataDirectiveSpec? spec = _spec.GetSpec(dataDirective.Name);
		if (spec is null) {
			throw new Exception($"Unrecognized data directive name: {dataDirective.Name}.");
		}
		ImmutableArray<OperandKind> kinds = spec.Operands;
		ImmutableArray<ExpressionNode> operands = dataDirective.Operands;
		if (operands.Length != kinds.Length) {
			throw new Exception($"Expected {kinds.Length} arguments, got {operands.Length}.");
		}
		for (int i = 0; i < kinds.Length; i++) {
			EncodeOperand(stream, kinds[i], operands[i]);
		}
	}

	public DataDirective Decode(Stream stream, string name) {
		DataDirectiveSpec? spec = _spec.GetSpec(name);
		Debug.Assert(spec is not null);

		ImmutableArray<OperandKind> operandSpecs = spec.Operands;
		List<ExpressionNode> operands = [];
		foreach (OperandKind operandSpec in operandSpecs) {
			operands.Add(DecodeOperand(stream, operandSpec));
		}
		return new(spec.Name, [..operands]);
	}

	static void EncodeOperand(Stream stream, OperandKind kind, ExpressionNode operand) {
		switch (kind) {
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

	static ExpressionNode DecodeOperand(Stream stream, OperandKind kind) {
		return kind switch {
			OperandKind.Int16 => DecodeOperandInt16(stream),
			OperandKind.Int32 => DecodeOperandInt32(stream),
			_ => throw new NotImplementedException(kind.ToString()),
		};
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
		value = SignExtend(value, 32);
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
	static void PutByte(Stream stream, byte value) {
		stream.WriteByte(value);
	}
}