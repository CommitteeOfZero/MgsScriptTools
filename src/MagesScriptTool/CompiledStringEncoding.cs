using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class CompiledStringEncoding {
	readonly StringTagsSpec _spec;

	public CompiledStringEncoding(StringTagsSpec spec) {
		_spec = spec;
	}

	public void Encode(Stream stream, ImmutableArray<StringToken> tokens) {
		new CompiledStringEncoder(stream, _spec, tokens).Encode();
	}

	public ImmutableArray<StringToken> Decode(Stream stream) {
		return new CompiledStringDecoder(stream, _spec).Decode();
	}

	sealed class CompiledStringEncoder {
		readonly Stream _stream;
		readonly StringTagsSpec _spec;
		readonly ImmutableArray<StringToken> _tokens;

		public CompiledStringEncoder(Stream stream, StringTagsSpec spec, ImmutableArray<StringToken> tokens) {
			_stream = stream;
			_spec = spec;
			_tokens = tokens;
		}

		public void Encode() {
			foreach (StringToken token in _tokens) {
				EncodeToken(token);
			}
			PutByte(0xFF);
		}

		void EncodeToken(StringToken genericToken) {
			switch (genericToken) {
				case StringTokenTag token: {
					EncodeTag(token);
					break;
				}
				case StringTokenGlyph token: {
					EncodeGlyph(token);
					break;
				}
				default: {
					throw new NotImplementedException(genericToken.GetType().Name);
				}
			}
		}

		void EncodeTag(StringTokenTag tag) {
			StringTagSpec? spec = _spec.GetSpec(tag.Name);
			if (spec is null) {
				throw new Exception($"Unrecognized string tag name: {tag.Name}.");
			}
			PutByte((byte)spec.Opcode);
			for (int i = 0; i < spec.Operands.Length; i++) {
				EncodeOperand(spec.Operands[i], tag.Operands[i]);
			}
		}

		void EncodeGlyph(StringTokenGlyph glyph) {
			int value = glyph.Value;
			PutByte((byte)((value >> 8) & 0x7F | 0x80));
			PutByte((byte)((value >> 0) & 0xFF));
		}

		void EncodeOperand(OperandKind kind, ExpressionNode expression) {
			switch (kind) {
				case OperandKind.Expr: {
					ExpressionEncoding.Encode(_stream, expression);
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

		void EncodeOperandInt8(ExpressionNode expression) {
			int value = expression.GetInt();
			PutByte((byte)((value >> 0) & 0xFF));
		}

		void EncodeOperandInt16(ExpressionNode expression) {
			int value = expression.GetInt();
			PutByte((byte)((value >> 8) & 0xFF));
			PutByte((byte)((value >> 0) & 0xFF));
		}

		void PutByte(byte value) {
			_stream.WriteByte(value);
		}
	}

	sealed class CompiledStringDecoder {
		readonly Stream _stream;
		readonly StringTagsSpec _spec;

		public CompiledStringDecoder(Stream stream, StringTagsSpec spec) {
			_stream = stream;
			_spec = spec;
		}

		public ImmutableArray<StringToken> Decode() {
			List<StringToken> tokens = [];
			while (true) {
				StringToken? token = DecodeToken();
				if (token is null) {
					break;
				}
				tokens.Add(token);
			}
			return [..tokens];
		}

		StringToken? DecodeToken() {
			byte b = GetByte();
			if (b == 0xFF) {
				return null;
			}
			_stream.Position -= 1;
			if ((b & 0x80) == 0) {
				return DecodeTag();
			} else {
				return DecodeGlyph();
			}
		}

		StringTokenTag DecodeTag() {
			byte opcode = GetByte();
			StringTagSpec? spec = _spec.GetSpec(opcode);
			if (spec is null) {
				throw new Exception($"Unrecognized string tag opcode: 0x{opcode:X02}.");
			}
			List<ExpressionNode> operands = [];
			for (int i = 0; i < spec.Operands.Length; i++) {
				operands.Add(DecodeOperand(spec.Operands[i]));
			}
			return new(spec.Name, [..operands]);
		}

		StringTokenGlyph DecodeGlyph() {
			int value = 0;
			value |= (GetByte() & 0x7F) << 8;
			value |= (GetByte() & 0xFF) << 0;
			return new(value);
		}

		ExpressionNode DecodeOperand(OperandKind kind) {
			switch (kind) {
				case OperandKind.Expr: {
					return ExpressionEncoding.Decode(_stream);
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

		ExpressionNodeNumber DecodeOperandUInt8() {
			int value = 0;
			value |= GetByte() << 0;
			return new(value);
		}

		ExpressionNodeNumber DecodeOperandInt8() {
			int value = 0;
			value |= GetByte() << 0;
			value = SignExtend(value, 8);
			return new(value);
		}

		ExpressionNodeNumber DecodeOperandInt16() {
			int value = 0;
			value |= GetByte() << 8;
			value |= GetByte() << 0;
			value = SignExtend(value, 16);
			return new(value);
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
