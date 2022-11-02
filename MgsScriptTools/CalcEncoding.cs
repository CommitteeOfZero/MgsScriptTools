namespace MgsScriptTools;

public class CalcEncoding {
	public static void Encode(Stream stream, CalcToken[] tokens) {
		new CalcEncoder(stream).Encode(tokens);
	}

	public static CalcToken[] Decode(Stream stream) {
		return new CalcDecoder(stream).Decode();
	}

	class CalcEncoder {
		Stream _stream;

		public CalcEncoder(Stream stream) {
			_stream = stream;
		}

		public void Encode(CalcToken[] tokens) {
			foreach (var token in tokens)
				EncodeToken(token);
			PutByte(0x00);
		}

		void EncodeToken(CalcToken token) {
			switch (token) {
				case CalcOperator @operator: {
					EncodeOperator(@operator);
					break;
				}
				case CalcLiteral literal: {
					EncodeLiteral(literal);
					break;
				}
				default: {
					throw new NotImplementedException(token.GetType().Name);
				}
			}
		}

		void EncodeOperator(CalcOperator @operator) {
			PutByte((byte)@operator.Opcode);
			PutByte((byte)@operator.Priority);
		}

		void EncodeLiteral(CalcLiteral literal) {
			EncodeVarInt(literal.Value);
			//PutByte(0); // priority
			PutByte((byte)literal.Priority);
		}

		void EncodeVarInt(int value) {
			if (value < -0x100000 || value >= 0x100000) {
				PutByte(0xE0);
				PutByte((byte)((value >> 00) & 0xFF));
				PutByte((byte)((value >> 08) & 0xFF));
				PutByte((byte)((value >> 16) & 0xFF));
				PutByte((byte)((value >> 24) & 0xFF));
			} else if (value < -0x1000 || value >= 0x1000) {
				PutByte((byte)(0xC0 | ((value >> 16) & 0x1F)));
				PutByte((byte)((value >> 00) & 0xFF));
				PutByte((byte)((value >> 08) & 0xFF));
			} else if (value < -0x10 || value >= 0x10) {
				PutByte((byte)(0xA0 | ((value >> 8) & 0x1F)));
				PutByte((byte)((value >> 00) & 0xFF));
			} else {
				PutByte((byte)(0x80 | ((value >> 0) & 0x1F)));
			}
		}

		void PutByte(byte value) {
			_stream.WriteByte(value);
		}
	}

	class CalcDecoder {
		Stream _stream;

		public CalcDecoder(Stream stream) {
			_stream = stream;
		}

		public CalcToken[] Decode() {
			List<CalcToken> tokens = new();
			while (true) {
				if (GetByte() == 0x00)
					break;
				_stream.Position -= 1;
				tokens.Add(DecodeToken());
			}
			return tokens.ToArray();
		}

		CalcToken DecodeToken() {
			if ((PeekByte() & 0x80) == 0)
				return DecodeOperator();
			else
				return DecodeLiteral();
		}

		CalcOperator DecodeOperator() {
			byte opcode = GetByte();
			byte priority = GetByte();
			return new(opcode, priority);
		}

		CalcLiteral DecodeLiteral() {
			int value = DecodeVarInt();
			//GetByte(); // skip priority
			int priority = GetByte();
			return new(value, priority);
		}

		int DecodeVarInt() {
			byte b = GetByte();
			int value = 0;
			if ((b & 0x60) == 0x60) {
				value |= GetByte() << 0;
				value |= GetByte() << 8;
				value |= GetByte() << 16;
				value |= GetByte() << 24;
				if ((b & 0x1F) != 0)
					throw new Exception("Invalid variable-length integer");
				if (value >= -0x100000 && value < 0x100000)
					throw new Exception("Variable-length integer was not encoded efficiently");
			} else if ((b & 0x40) == 0x40) {
				value |= (b & 0x1F) << 16;
				value |= GetByte() << 0;
				value |= GetByte() << 8;
				value = SignExtend(value, 21);
				if (value >= -0x1000 && value < 0x1000)
					throw new Exception("Variable-length integer was not encoded efficiently");
			} else if ((b & 0x20) == 0x20) {
				value |= (b & 0x1F) << 8;
				value |= GetByte();
				value = SignExtend(value, 13);
				if (value >= -0x10 && value < 0x10)
					throw new Exception("Variable-length integer was not encoded efficiently");
			} else {
				value |= b & 0x1F;
				value = SignExtend(value, 5);
			}
			return value;
		}

		byte PeekByte() {
			var value = GetByte();
			_stream.Position -= 1;
			return value;
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

public abstract class CalcToken {
	public abstract bool IsLowerThan(int priority);
}

public class CalcLiteral : CalcToken {
	public int Value;
	public int Priority;

	public CalcLiteral(int value, int priority) {
		Value = value;
		Priority = priority;
	}

	public override bool IsLowerThan(int priority) {
		return false;
	}
}

public class CalcOperator : CalcToken {
	public int Opcode;
	public int Priority;

	public CalcOperator(int opcode, int priority) {
		Opcode = opcode;
		Priority = priority;
	}

	public override bool IsLowerThan(int priority) {
		return Priority < priority;
	}
}
