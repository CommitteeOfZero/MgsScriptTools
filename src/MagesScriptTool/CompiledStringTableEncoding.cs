using System.Collections.Immutable;
using System.Text;

namespace MagesScriptTool;

sealed class CompiledStringTableEncoding {
	static readonly ImmutableArray<byte> Magic = [..Encoding.Latin1.GetBytes("MES\0")];

	readonly CompiledStringEncoding _stringEncoding;

	public CompiledStringTableEncoding(CompiledStringEncoding stringEncoding) {
		_stringEncoding = stringEncoding;
	}

	public void Encode(Stream stream, ImmutableArray<StringTableEntry> entries) {
		new CompiledStringTableEncoder(_stringEncoding, stream).Encode(entries);
	}

	public ImmutableArray<StringTableEntry> Decode(Stream stream) {
		return new CompiledStringTableDecoder(_stringEncoding, stream).Decode();
	}

	sealed class CompiledStringTableEncoder {
		readonly CompiledStringEncoding _stringEncoding;
		readonly Stream _stream;

		public CompiledStringTableEncoder(CompiledStringEncoding stringEncoding, Stream stream) {
			_stringEncoding = stringEncoding;
			_stream = stream;
		}

		public void Encode(ImmutableArray<StringTableEntry> entries) {
			Write(Magic.AsSpan());
			WriteInt(1); // languages
			WriteInt(entries.Length);
			WriteInt(16 + entries.Length * 8);

			List<ImmutableArray<byte>> stringsData = [];
			for (int i = 0; i < entries.Length; i++) {
				MemoryStream stream = new();
				_stringEncoding.Encode(stream, entries[i].Tokens);
				stringsData.Add([..stream.ToArray()]);
			}

			int offset = 0;
			for (int i = 0; i < entries.Length; i++) {
				WriteInt(entries[i].Index);
				WriteInt(offset);
				offset += stringsData[i].Length;
			}

			for (int i = 0; i < entries.Length; i++) {
				Write(stringsData[i].AsSpan());
			}
		}

		void WriteInt(int value) {
			Span<byte> buffer = stackalloc byte[4];
			for (int i = 0; i < 4; i++) {
				buffer[i] = (byte)(value >> (i * 8));
			}
			Write(buffer);
		}

		void Write(ReadOnlySpan<byte> data) {
			_stream.Write(data);
		}
	}

	sealed class CompiledStringTableDecoder {
		readonly CompiledStringEncoding _stringEncoding;
		readonly Stream _stream;

		public CompiledStringTableDecoder(CompiledStringEncoding stringEncoding, Stream stream) {
			_stringEncoding = stringEncoding;
			_stream = stream;
		}

		public ImmutableArray<StringTableEntry> Decode() {
			Span<byte> magic = stackalloc byte[Magic.Length];
			ReadExact(magic);
			if (!magic.SequenceEqual(Magic.AsSpan())) {
				throw new Exception("Invalid magic.");
			}

			int languages = ReadInt();
			if (languages < 1) {
				throw new Exception("Invalid languages count.");
			}

			int entryCount = ReadInt();
			int stringsStart = ReadInt();

			List<CompiledStringTableEntryHeader> headers = [];
			for (int i = 0; i < entryCount; i++) {
				int index = ReadInt();
				int offset = ReadInt();
				for (int j = 1; j < languages; j++) {
					ReadInt();
				}
				// TODO: handle languages other than 0
				headers.Add(new(index, stringsStart + offset));
			}

			List<StringTableEntry> entries = [];
			for (int i = 0; i < entryCount; i++) {
				int offset = headers[i].Offset;
				Seek(offset);
				ImmutableArray<StringToken> tokens = _stringEncoding.Decode(_stream);
				entries.Add(new(headers[i].Index, tokens));
			}

			return [..entries];
		}

		int ReadInt() {
			Span<byte> buffer = stackalloc byte[4];
			ReadExact(buffer);
			int result = 0;
			for (int i = 0; i < 4; i++) {
				result |= buffer[i] << (i * 8);
			}
			return result;
		}

		void ReadExact(Span<byte> buffer) {
			int position = 0;
			while (position < buffer.Length) {
				int n = _stream.Read(buffer[position..]);
				if (n <= 0) {
					throw new EndOfStreamException();
				}
				position += n;
			}
		}

		void Seek(int position) {
			_stream.Position = position;
		}

		int Tell() {
			return (int)_stream.Position;
		}

		sealed class CompiledStringTableEntryHeader {
			public readonly int Index;
			public readonly int Offset;

			public CompiledStringTableEntryHeader(int id, int offset) {
				Index = id;
				Offset = offset;
			}
		}
	}
}
