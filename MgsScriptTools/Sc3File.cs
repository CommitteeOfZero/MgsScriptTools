using System.Text;

namespace MgsScriptTools;

public struct Sc3File {
	static readonly byte[] Magic = Encoding.ASCII.GetBytes("SC3\0");

	public byte[] Code;
	public int[] Labels;
	public int[] ReturnAddresses;
	public byte[][] Strings;

	public static void Encode(Stream stream, Sc3File file) {
		new Sc3FileEncoder(stream).Encode(file);
	}

	public static Sc3File Decode(Stream stream) {
		return new Sc3FileDecoder(stream).Decode();
	}

	class Sc3FileEncoder {
		Stream _stream;

		public Sc3FileEncoder(Stream stream) {
			_stream = stream;
		}

		public void Encode(Sc3File file) {
			int codeOffset = 12 + file.Labels.Length * 4;
			int padding = (4 - file.Code.Length % 4) % 4;
			int stringAddressesStart = codeOffset + file.Code.Length + padding;
			int stringAddressesEnd = stringAddressesStart + file.Strings.Length * 4;

			Write(Magic);
			WriteInt(stringAddressesStart);
			WriteInt(stringAddressesEnd);

			foreach (var label in file.Labels)
				WriteInt(codeOffset + label);

			Write(file.Code);
			for (int i = 0; i < padding; i++)
				WriteByte(0);

			int offset = stringAddressesEnd + file.ReturnAddresses.Length * 4;
			foreach (var stringData in file.Strings) {
				WriteInt(offset);
				offset += stringData.Length;
			}

			foreach (var returnAddress in file.ReturnAddresses)
				WriteInt(codeOffset + returnAddress);

			foreach (var stringData in file.Strings)
				Write(stringData);
		}

		void WriteInt(int value) {
			Span<byte> buffer = stackalloc byte[4];
			for (int i = 0; i < 4; i++)
				buffer[i] = (byte)(value >> (i * 8));
			Write(buffer);
		}

		void WriteByte(byte value) {
			_stream.WriteByte(value);
		}

		void Write(Span<byte> data) {
			_stream.Write(data);
		}

		void Seek(int position) {
			_stream.Position = position;
		}
	}

	class Sc3FileDecoder {
		Stream _stream;

		public Sc3FileDecoder(Stream stream) {
			_stream = stream;
		}

		public Sc3File Decode() {
			Span<byte> magic = stackalloc byte[4];
			ReadExact(magic);
			if (!magic.SequenceEqual(Magic))
				throw new Exception($"Invalid magic");

			int stringAddressesStart = ReadInt();
			if (stringAddressesStart < 0)
				throw new Exception("Invalid string addresses start offset");

			int stringAddressesEnd = ReadInt();
			if (stringAddressesEnd < stringAddressesStart || stringAddressesEnd > _stream.Length)
				throw new Exception("Invalid string addresses end offset");

			List<int> labels = new();
			int lowestLabel = stringAddressesStart;
			while (Tell() + 4 <= lowestLabel) {
				int label = ReadInt();
				if (label < Tell() || label > stringAddressesStart)
					break;
				labels.Add(label);
				lowestLabel = Math.Min(lowestLabel, label);
			}

			Seek(lowestLabel);
			var code = new byte[stringAddressesStart];
			ReadExact(code.AsSpan(lowestLabel..));

			List<int> stringAddresses = new();
			Seek(stringAddressesStart);
			int lowestStringAddress = (int)_stream.Length;
			while (Tell() + 4 <= stringAddressesEnd) {
				int address = ReadInt();
				stringAddresses.Add(address);
				lowestStringAddress = Math.Min(lowestStringAddress, address);
			}
			//if (Tell() != stringAddressesEnd)
			//	throw new Exception("Invalid file layout");
			Seek(stringAddressesEnd);

			List<int> returnAddresses = new();
			while (Tell() + 4 <= lowestStringAddress)
				returnAddresses.Add(ReadInt());

			byte[][] strings = new byte[stringAddresses.Count][];
			for (int i = 0; i < stringAddresses.Count; i++) {
				//if (Tell() != stringAddresses[i])
				//	throw new Exception("Invalid file layout");
				Seek(stringAddresses[i]);
				int end;
				if (i + 1 < stringAddresses.Count)
					end = stringAddresses[i + 1];
				else
					end = (int)_stream.Length;
				var buffer = new byte[end - Tell()];
				ReadExact(buffer);
				strings[i] = buffer;
			}

			return new Sc3File {
				Code = code,
				Labels = labels.ToArray(),
				ReturnAddresses = returnAddresses.ToArray(),
				Strings = strings,
			};
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

		int Tell() {
			return (int)_stream.Position;
		}

		void Seek(int position) {
			_stream.Position = position;
		}
	}
}
