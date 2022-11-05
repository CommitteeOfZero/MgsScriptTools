using System.Text;

namespace MgsScriptTools;

public struct MesFile {
	static readonly byte[] Magic = Encoding.ASCII.GetBytes("MES\0");

	public MesFileEntry[] Entries;

	public static void Encode(Stream stream, MesFile file) {
		new MesFileEncoder(stream).Encode(file);
	}

	public static MesFile Decode(Stream stream) {
		return new MesFileDecoder(stream).Decode();
	}

	class MesFileEncoder {
		Stream _stream;

		public MesFileEncoder(Stream stream) {
			_stream = stream;
		}

		public void Encode(MesFile file) {
			Write(Magic);
			WriteInt(1); // languages
			WriteInt(file.Entries.Length);
			WriteInt(16 + file.Entries.Length * 8);

			int offset = 0;
			foreach (var entry in file.Entries) {
				WriteInt(entry.Index);
				WriteInt(offset);
				offset += entry.String.Length;
			}

			foreach (var entry in file.Entries)
				Write(entry.String);
		}

		void WriteInt(int value) {
			Span<byte> buffer = stackalloc byte[4];
			for (int i = 0; i < 4; i++)
				buffer[i] = (byte)(value >> (i * 8));
			Write(buffer);
		}

		void Write(Span<byte> data) {
			_stream.Write(data);
		}
	}

	class MesFileDecoder {
		Stream _stream;

		public MesFileDecoder(Stream stream) {
			_stream = stream;
		}

		public MesFile Decode() {
			Span<byte> magic = stackalloc byte[4];
			ReadExact(magic);
			if (!magic.SequenceEqual(Magic))
				throw new Exception($"Invalid magic");

			int languages = ReadInt();
			if (languages < 1)
				throw new Exception($"Invalid languages count");

			int entryCount = ReadInt();
			int stringsStart = ReadInt();

			var table = new MesTableEntry[entryCount];
			for (int i = 0; i < entryCount; i++) {
				int id = ReadInt();
				int offset = ReadInt();
				for (int j = 1; j < languages; j++)
					ReadInt();
				// TODO: handle languages other than 0
				table[i] = new MesTableEntry {
					Id = id,
					Offset = stringsStart + offset,
				};
			}
			var lengthTable = ConstructLengthTable(table);

			var entries = new MesFileEntry[entryCount];
			for (int i = 0; i < table.Length; i++) {
				int offset = table[i].Offset;
				int end = lengthTable[offset];
				Seek(offset);
				var buffer = new byte[end - offset];
				ReadExact(buffer);
				entries[i] = new MesFileEntry {
					Index = table[i].Id,
					String = buffer,
				};
			}

			return new MesFile {
				Entries = entries,
			};
		}

		Dictionary<int, int> ConstructLengthTable(MesTableEntry[] entries) {
			SortedSet<int> offsets = new();
			foreach (var entry in entries)
				offsets.Add(entry.Offset);
			Dictionary<int, int> table = new();
			int lastOffset = -1;
			foreach (var offset in offsets) {
				if (lastOffset >= 0)
					table[lastOffset] = offset;
				lastOffset = offset;
			}
			if (lastOffset >= 0)
				table[lastOffset] = (int)_stream.Length;
			return table;
		}

		int ReadInt() {
			Span<byte> buffer = stackalloc byte[4];
			ReadExact(buffer);
			int result = 0;
			for (int i = 0; i < 4; i++)
				result |= buffer[i] << (i * 8);
			return result;
		}

		void ReadExact(Span<byte> buffer) {
			int position = 0;
			while (position < buffer.Length) {
				int n = _stream.Read(buffer[position..]);
				if (n <= 0)
					throw new EndOfStreamException();
				position += n;
			}
		}

		void Seek(int position) {
			_stream.Position = position;
		}

		int Tell() {
			return (int)_stream.Position;
		}

		struct MesTableEntry {
			public int Id;
			public int Offset;
		}
	}
}

public struct MesFileEntry {
	public int Index;
	public byte[] String;
}
