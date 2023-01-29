namespace MgsScriptTools;

public class MesEncoding {
	MesTagsSpec _spec;

	public MesEncoding(MesTagsSpec spec) {
		_spec = spec;
	}

	public byte[] EncodeBytes(MesToken[] tokens) {
		MemoryStream stream = new();
		Encode(stream, tokens);
		return stream.ToArray();
	}

	public MesToken[] DecodeBytes(byte[] data) {
		MemoryStream stream = new(data);
		var result = Decode(stream);
		//if (stream.Position != stream.Length)
		//	throw new Exception("Stray data past the end of string");
		return result;
	}

	public void Encode(Stream stream, MesToken[] tokens) {
		new MesEncoder(stream, _spec).Encode(tokens);
	}

	public MesToken[] Decode(Stream stream) {
		return new MesDecoder(stream, _spec).Decode();
	}

	class MesEncoder {
		Stream _stream;
		MesTagsSpec _spec;

		public MesEncoder(Stream stream, MesTagsSpec spec) {
			_stream = stream;
			_spec = spec;
		}

		public void Encode(MesToken[] tokens) {
			foreach (var token in tokens)
				EncodeToken(token);
			PutByte(0xFF);
		}

		void EncodeToken(MesToken token) {
			switch (token) {
				case MesTag tag: {
					EncodeTag(tag);
					break;
				}
				case MesGlyph glyph: {
					EncodeGlyph(glyph);
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}

		void EncodeTag(MesTag tag) {
			var spec = _spec.GetSpec(tag.Kind);
			PutByte((byte)spec.Opcode);
			for (int i = 0; i < spec.Operands.Length; i++)
				EncodeOperand(spec.Operands[i], tag.Operands[i]);
		}

		void EncodeGlyph(MesGlyph glyph) {
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

	class MesDecoder {
		Stream _stream;
		MesTagsSpec _spec;

		public MesDecoder(Stream stream, MesTagsSpec spec) {
			_stream = stream;
			_spec = spec;
		}

		public MesToken[] Decode() {
			List<MesToken> tokens = new();
			while (true) {
				var token = DecodeToken();
				if (token is null)
					break;
				tokens.Add(token);
			}
			return tokens.ToArray();
		}

		MesToken? DecodeToken() {
			byte b = GetByte();
			if (b == 0xFF)
				return null;
			_stream.Position -= 1;
			if ((b & 0x80) == 0)
				return DecodeTag();
			else
				return DecodeGlyph();
		}

		MesTag DecodeTag() {
			var opcode = GetByte();
			var spec = _spec.GetSpec(opcode);
			var operands = new Expression[spec.Operands.Length];
			for (int i = 0; i < operands.Length; i++)
				operands[i] = DecodeOperand(spec.Operands[i]);
			return new(spec.Kind, operands);
		}

		MesGlyph DecodeGlyph() {
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
