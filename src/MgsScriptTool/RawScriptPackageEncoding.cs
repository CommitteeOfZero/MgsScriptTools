using System.Collections.Immutable;
using System.Text;

namespace MgsScriptTool;

sealed class RawScriptPackageEncoding {
	static readonly ImmutableArray<byte> Magic = [..Encoding.ASCII.GetBytes("SC3\0")];

	readonly RawStringEncoding _stringEncoding;

	public RawScriptPackageEncoding(RawStringEncoding stringEncoding) {
		_stringEncoding = stringEncoding;
	}

	public void Encode(Stream stream, RawScriptPackage package) {
		new RawScriptPackageEncoder(_stringEncoding, stream).Encode(package);
	}

	public RawScriptPackage Decode(Stream stream) {
		return new RawScriptPackageDecoder(_stringEncoding, stream).Decode();
	}

	sealed class RawScriptPackageEncoder {
		readonly RawStringEncoding _stringEncoding;
		readonly Stream _stream;

		public RawScriptPackageEncoder(RawStringEncoding stringEncoding, Stream stream) {
			_stringEncoding = stringEncoding;
			_stream = stream;
		}

		public void Encode(RawScriptPackage package) {
			int codeOffset = 12 + package.Script.Labels.Length * 4;
			int padding = (4 - package.Script.Code.Length % 4) % 4;
			int stringAddressesStart = codeOffset + package.Script.Code.Length + padding;
			int stringAddressesEnd = stringAddressesStart + package.Strings.Length * 4;

			Write(Magic.AsSpan());
			WriteInt(stringAddressesStart);
			WriteInt(stringAddressesEnd);

			foreach (int label in package.Script.Labels) {
				WriteInt(codeOffset + label);
			}

			Write(package.Script.Code.AsSpan());
			for (int i = 0; i < padding; i++) {
				WriteByte(0);
			}

			List<ImmutableArray<byte>> stringsData = [];
			foreach (ImmutableArray<StringToken> tokens in package.Strings) {
				MemoryStream stream = new();
				_stringEncoding.Encode(stream, tokens);
				stringsData.Add([..stream.ToArray()]);
			}

			int offset = stringAddressesEnd + package.Script.ReturnLabels.Length * 4;
			foreach (ImmutableArray<byte> data in stringsData) {
				WriteInt(offset);
				offset += data.Length;
			}

			foreach (int returnLabel in package.Script.ReturnLabels) {
				WriteInt(codeOffset + returnLabel);
			}

			foreach (ImmutableArray<byte> data in stringsData) {
				Write(data.AsSpan());
			}
		}

		void WriteInt(int value) {
			Span<byte> buffer = stackalloc byte[4];
			for (int i = 0; i < 4; i++) {
				buffer[i] = (byte)(value >> (i * 8));
			}
			Write(buffer);
		}

		void WriteByte(byte value) {
			_stream.WriteByte(value);
		}

		void Write(ReadOnlySpan<byte> data) {
			_stream.Write(data);
		}

		void Seek(int position) {
			_stream.Position = position;
		}
	}

	sealed class RawScriptPackageDecoder {
		readonly RawStringEncoding _stringEncoding;
		readonly Stream _stream;

		public RawScriptPackageDecoder(RawStringEncoding stringEncoding, Stream stream) {
			_stringEncoding = stringEncoding;
			_stream = stream;
		}

		public RawScriptPackage Decode() {
			Span<byte> magic = stackalloc byte[Magic.Length];
			Read(magic);
			if (!magic.SequenceEqual(Magic.AsSpan())) {
				throw new Exception("Invalid magic.");
			}

			int stringPositionsStart = ReadInt();
			if (stringPositionsStart < 0) {
				throw new Exception("Invalid string positions start offset.");
			}

			int stringPositionsEnd = ReadInt();
			if (stringPositionsEnd < stringPositionsStart || stringPositionsEnd > _stream.Length) {
				throw new Exception("Invalid string positions end offset.");
			}

			List<int> labels = [];
			int lowestLabel = stringPositionsStart;
			while (Tell() + 4 <= lowestLabel) {
				int label = ReadInt();
				if (label < Tell() || label > stringPositionsStart) {
					break;
				}
				labels.Add(label);
				lowestLabel = Math.Min(lowestLabel, label);
			}

			Seek(lowestLabel);
			byte[] code = new byte[stringPositionsStart];
			Read(code.AsSpan(lowestLabel..));

			List<int> stringPositions = [];
			Seek(stringPositionsStart);
			int lowestStringPosition = (int)_stream.Length;
			while (Tell() + 4 <= stringPositionsEnd) {
				int position = ReadInt();
				stringPositions.Add(position);
				lowestStringPosition = Math.Min(lowestStringPosition, position);
			}
			Seek(stringPositionsEnd);

			List<int> returnLabels = [];
			while (Tell() + 4 <= lowestStringPosition) {
				returnLabels.Add(ReadInt());
			}

			List<ImmutableArray<StringToken>> strings = [];
			for (int i = 0; i < stringPositions.Count; i++) {
				Seek(stringPositions[i]);
				strings.Add(_stringEncoding.Decode(_stream));
			}

			RawScript script = new([..code], [..labels], [..returnLabels]);
			return new(script, [..strings]);
		}

		int ReadInt() {
			Span<byte> buffer = stackalloc byte[4];
			Read(buffer);
			int result = 0;
			for (int i = 0; i < 4; i++) {
				result |= buffer[i] << (i * 8);
			}
			return result;
		}

		void Read(Span<byte> buffer) {
			_stream.ReadExactly(buffer);
		}

		int Tell() {
			return checked((int)_stream.Position);
		}

		void Seek(int position) {
			_stream.Position = position;
		}
	}
}
