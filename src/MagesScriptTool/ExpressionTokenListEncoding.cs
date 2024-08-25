using System.Collections.Immutable;

namespace MagesScriptTool;

static class ExpressionTokenListEncoding {
	public static void Encode(Stream stream, ImmutableArray<ExpressionToken> tokens) {
		new ExpressionTokenListEncoder(stream, tokens).Encode();
	}

	public static ImmutableArray<ExpressionToken> Decode(Stream stream) {
		return new ExpressionTokenListDecoder(stream).Decode();
	}

	sealed class ExpressionTokenListEncoder {
		readonly Stream _stream;
		readonly ImmutableArray<ExpressionToken> _tokens;

		public ExpressionTokenListEncoder(Stream stream, ImmutableArray<ExpressionToken> tokens) {
			_stream = stream;
			_tokens = tokens;
		}

		public void Encode() {
			foreach (ExpressionToken token in _tokens) {
				EncodeToken(token);
			}
			PutByte(0x00);
		}

		void EncodeToken(ExpressionToken genericToken) {
			switch (genericToken) {
				case ExpressionTokenOperator token: {
					EncodeOperator(token);
					break;
				}
				case ExpressionTokenLiteral token: {
					EncodeLiteral(token);
					break;
				}
				default: {
					throw new NotImplementedException(genericToken.GetType().Name);
				}
			}
		}

		void EncodeOperator(ExpressionTokenOperator @operator) {
			PutByte((byte)@operator.Opcode);
			PutByte((byte)@operator.Priority);
		}

		void EncodeLiteral(ExpressionTokenLiteral literal) {
			EncodeVarInt(literal.Value);
			//PutByte(0); // priority
			PutByte((byte)literal.Priority);
		}

		void EncodeVarInt(int value) {
			if (value < -0x100000 || value >= 0x100000) {
				PutByte(0xE0);
				PutByte((byte)((value >> 0) & 0xFF));
				PutByte((byte)((value >> 8) & 0xFF));
				PutByte((byte)((value >> 16) & 0xFF));
				PutByte((byte)((value >> 24) & 0xFF));
			} else if (value < -0x1000 || value >= 0x1000) {
				PutByte((byte)(0xC0 | ((value >> 16) & 0x1F)));
				PutByte((byte)((value >> 0) & 0xFF));
				PutByte((byte)((value >> 8) & 0xFF));
			} else if (value < -0x10 || value >= 0x10) {
				PutByte((byte)(0xA0 | ((value >> 8) & 0x1F)));
				PutByte((byte)((value >> 0) & 0xFF));
			} else {
				PutByte((byte)(0x80 | ((value >> 0) & 0x1F)));
			}
		}

		void PutByte(byte value) {
			_stream.WriteByte(value);
		}
	}

	sealed class ExpressionTokenListDecoder {
		readonly Stream _stream;

		public ExpressionTokenListDecoder(Stream stream) {
			_stream = stream;
		}

		public ImmutableArray<ExpressionToken> Decode() {
			List<ExpressionToken> tokens = [];
			while (true) {
				if (GetByte() == 0x00) {
					break;
				}
				_stream.Position -= 1;
				tokens.Add(DecodeToken());
			}
			return [..tokens];
		}

		ExpressionToken DecodeToken() {
			if ((PeekByte() & 0x80) == 0) {
				return DecodeOperator();
			} else {
				return DecodeLiteral();
			}
		}

		ExpressionTokenOperator DecodeOperator() {
			byte opcode = GetByte();
			byte priority = GetByte();
			return new(opcode, priority);
		}

		ExpressionTokenLiteral DecodeLiteral() {
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
				if ((b & 0x1F) != 0) {
					throw new Exception("Invalid variable-length integer.");
				}
				if (value >= -0x100000 && value < 0x100000) {
					throw new Exception("Invalid variable-length integer.");
				}
			} else if ((b & 0x40) == 0x40) {
				value |= (b & 0x1F) << 16;
				value |= GetByte() << 0;
				value |= GetByte() << 8;
				value = SignExtend(value, 21);
				if (value >= -0x1000 && value < 0x1000) {
					throw new Exception("Invalid variable-length integer.");
				}
			} else if ((b & 0x20) == 0x20) {
				value |= (b & 0x1F) << 8;
				value |= GetByte();
				value = SignExtend(value, 13);
				if (value >= -0x10 && value < 0x10) {
					throw new Exception("Invalid variable-length integer.");
				}
			} else {
				value |= b & 0x1F;
				value = SignExtend(value, 5);
			}
			return value;
		}

		byte PeekByte() {
			byte value = GetByte();
			_stream.Position -= 1;
			return value;
		}

		byte GetByte() {
			int value = _stream.ReadByte();
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

	}
}
