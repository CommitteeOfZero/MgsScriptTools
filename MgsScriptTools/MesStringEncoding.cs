namespace MgsScriptTools;

public class MesStringEncoding {
	MesStringSpec _spec = new();

	public MesStringEncoding(MesStringSpec spec) {
		_spec = spec;
	}

	public byte[] EncodeBytes(MesStringToken[] tokens) {
		MemoryStream stream = new();
		Encode(stream, tokens);
		return stream.ToArray();
	}

	public MesStringToken[] DecodeBytes(byte[] data) {
		MemoryStream stream = new(data);
		var result = Decode(stream);
		//if (stream.Position != stream.Length)
		//	throw new Exception("Stray data past the end of string");
		return result;
	}

	public void Encode(Stream stream, MesStringToken[] tokens) {
		new MesStringEncoder(stream, _spec).Encode(tokens);
	}

	public MesStringToken[] Decode(Stream stream) {
		return new MesStringDecoder(stream, _spec).Decode();
	}

	class MesStringEncoder {
		Stream _stream;
		MesStringSpec _spec;

		public MesStringEncoder(Stream stream, MesStringSpec spec) {
			_stream = stream;
			_spec = spec;
		}

		public void Encode(MesStringToken[] tokens) {
			foreach (var token in tokens)
				EncodeToken(token);
			PutByte(0xFF);
		}

		void EncodeToken(MesStringToken token) {
			switch (token) {
				case MesStringCommand command: {
					EncodeCommand(command);
					break;
				}
				case MesStringGlyph glyph: {
					EncodeGlyph(glyph);
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}

		void EncodeCommand(MesStringCommand command) {
			MesCommand value = command.Value;
			var spec = _spec.GetSpec(value.Kind);
			PutByte((byte)spec.Opcode);
			for (int i = 0; i < spec.Operands.Length; i++)
				EncodeOperand(spec.Operands[i], value.Operands[i]);
		}

		void EncodeGlyph(MesStringGlyph glyph) {
			int value = glyph.Value;
			PutByte((byte)((value >> 08) & 0x7F | 0x80));
			PutByte((byte)((value >> 00) & 0xFF));
		}

		void EncodeOperand(OperandKind kind, Expression expression) {
			switch (kind) {
				case OperandKind.Calc: {
					CalcExpressionEncoding.Encode(_stream, expression);
					break;
				}
				case OperandKind.UInt8:
				case OperandKind.Int8: {
					EncodeOperandInt8(expression);
					break;
				}
				case OperandKind.Int16: {
					EncodeOperandInt16(expression);
					break;
				}
				default: {
					throw new NotImplementedException(kind.ToString());
				}
			}
		}

		void EncodeOperandInt8(Expression expression) {
			int value = expression.GetInt();
			PutByte((byte)((value >> 00) & 0xFF));
		}

		void EncodeOperandInt16(Expression expression) {
			int value = expression.GetInt();
			PutByte((byte)((value >> 08) & 0xFF));
			PutByte((byte)((value >> 00) & 0xFF));
		}

		void PutByte(byte value) {
			_stream.WriteByte(value);
		}
	}

	class MesStringDecoder {
		Stream _stream;
		MesStringSpec _spec;

		public MesStringDecoder(Stream stream, MesStringSpec spec) {
			_stream = stream;
			_spec = spec;
		}

		public MesStringToken[] Decode() {
			List<MesStringToken> tokens = new();
			while (true) {
				var token = DecodeToken();
				if (token is null)
					break;
				tokens.Add(token);
			}
			return tokens.ToArray();
		}

		MesStringToken? DecodeToken() {
			byte b = GetByte();
			if (b == 0xFF)
				return null;
			_stream.Position -= 1;
			if ((b & 0x80) == 0)
				return DecodeCommand();
			else
				return DecodeGlyph();
		}

		MesStringCommand DecodeCommand() {
			var opcode = GetByte();
			var spec = _spec.GetSpec(opcode);
			var operands = new Expression[spec.Operands.Length];
			for (int i = 0; i < operands.Length; i++)
				operands[i] = DecodeOperand(spec.Operands[i]);
			var value = new MesCommand(spec.Kind, operands);
			return new(value);
		}

		MesStringGlyph DecodeGlyph() {
			int value = 0;
			value |= (GetByte() & 0x7F) << 08;
			value |= (GetByte() & 0xFF) << 00;
			return new(value);
		}

		Expression DecodeOperand(OperandKind kind) {
			switch (kind) {
				case OperandKind.Calc: {
					return CalcExpressionEncoding.Decode(_stream);
				}
				case OperandKind.UInt8: {
					return DecodeOperandUInt8();
				}
				case OperandKind.Int8: {
					return DecodeOperandInt8();
				}
				case OperandKind.Int16: {
					return DecodeOperandInt16();
				}
				default: {
					throw new NotImplementedException(kind.ToString());
				}
			}
		}

		Expression DecodeOperandUInt8() {
			int value = 0;
			value |= GetByte() << 00;
			return new NumberExpression(value);
		}

		Expression DecodeOperandInt8() {
			int value = 0;
			value |= GetByte() << 00;
			value = SignExtend(value, 8);
			return new NumberExpression(value);
		}

		Expression DecodeOperandInt16() {
			int value = 0;
			value |= GetByte() << 08;
			value |= GetByte() << 00;
			value = SignExtend(value, 16);
			return new NumberExpression(value);
		}

		byte GetByte() {
			int value = _stream.ReadByte();
			if (value < 0)
				throw new EndOfStreamException();
			return (byte)value;
		}

		int SignExtend(int value, int length) {
			int mask = 1 << (length - 1);
			int sign = value & mask;
			return value | ~(sign - 1);
		}
	}
}

public abstract class MesStringToken {
}

public class MesStringGlyph : MesStringToken {
	public int Value;

	public MesStringGlyph(int value) {
		Value = value;
	}
}

public class MesStringCommand : MesStringToken {
	public MesCommand Value;

	public MesStringCommand(MesCommand value) {
		Value = value;
	}
}
