namespace MgsScriptTools;

public class ScsDecompiler {
	InstructionEncoding _instructionEncoding;

	byte[] _code;
	int[] _labels;
	int[] _returnAddresses;

	Chunk[] _chunks;
	Dictionary<int, Chunk> _chunkMap;

	public ScsDecompiler(Sc3File sc3, InstructionEncoding instructionEncoding) {
		_instructionEncoding = instructionEncoding;

		_code = sc3.Code;
		_labels = sc3.Labels;
		_returnAddresses = sc3.ReturnAddresses;

		_chunks = null!;
		_chunkMap = null!;
	}

	public ScsPart[] Decompile() {
		InitializeChunks();
		foreach (var chunk in _chunks)
			chunk.Preprocess();
		foreach (var chunk in _chunks)
			AnalyzeChunk(chunk);
		foreach (var chunk in _chunks)
			chunk.Postprocess();

		List<ScsPart> parts = new();
		foreach (var chunk in _chunks) {
			foreach (var index in chunk.Labels)
				parts.Add(new ScsLabel(index));
			foreach (var index in chunk.ReturnAddresses)
				parts.Add(new ScsReturnAddress(index));
			parts.AddRange(chunk.Body);
			if (chunk.Error is not null)
				parts.Add(new ScsError(chunk.LastPosition, chunk.Error));
			if (chunk.LastPosition != chunk._end)
				parts.Add(new ScsRaw(_code[chunk.LastPosition..chunk._end]));
		}
		return parts.ToArray();
	}

	void InitializeChunks() {
		int[] markers = new SortedSet<int>(_labels.Concat(_returnAddresses)).ToArray();

		_chunks = new Chunk[markers.Length];
		_chunkMap = new();

		for (int i = 0; i < markers.Length; i++) {
			int endOffset;
			if (i + 1 >= markers.Length)
				endOffset = _code.Length;
			else
				endOffset = markers[i + 1];
			Chunk chunk = new(this, i, markers[i], endOffset);
			_chunks[i] = chunk;
			_chunkMap[markers[i]] = chunk;
		}

		for (int i = 0; i < _labels.Length; i++)
			_chunkMap[_labels[i]].Labels.Add(i);
		for (int i = 0; i < _returnAddresses.Length; i++)
			_chunkMap[_returnAddresses[i]].ReturnAddresses.Add(i);
	}

	void AnalyzeChunk(Chunk chunk) {
		foreach (ScsPart part in chunk.Body) {
			if (part is ScsInstruction { Value: var instruction })
				AnalyzeInstruction(instruction);
		}
	}

	void AnalyzeInstruction(Instruction instruction) {
		switch (instruction.Name) {
			case "SetMesModeFormat": {
				SetLabelKind(instruction.Operands[1].GetInt(), ChunkKind.MesModeFormatTable);
				break;
			}
			case "JumpTable": {
				SetLabelKind(instruction.Operands[1].GetInt(), ChunkKind.AdrTable);
				break;
			}
			case "SetTextTable": {
				SetLabelKind(instruction.Operands[1].GetInt(), ChunkKind.TextTable);
				break;
			}
			case "InitNameID": {
				SetLabelKind(instruction.Operands[0].GetInt(), ChunkKind.NameIdTable);
				break;
			}
			case "EncycDataInit": {
				SetLabelKind(instruction.Operands[0].GetInt(), ChunkKind.EncycDataTable);
				SetLabelKind(instruction.Operands[1].GetInt(), ChunkKind.EncycSortTable);
				break;
			}
		}

		foreach (var operand in instruction.Operands)
			AnalyzeExpression(operand);
	}

	void AnalyzeExpression(Expression expression) {
		if (expression is not OperationExpression operation)
			return;
		if (operation.Kind == OperatorKind.FuncLabel) {
			if (operation.Right[0] is NumberExpression { Value: int index })
				SetLabelKind(index, ChunkKind.Int32Table);
		}
		foreach (var operand in operation.Left)
			AnalyzeExpression(operand);
		foreach (var operand in operation.Right)
			AnalyzeExpression(operand);
	}

	void SetLabelKind(int label, ChunkKind kind) {
		if (label < 0 || label > _labels.Length)
			return;
		int address = _labels[label];
		_chunkMap[address].Kind = kind;
	}

	class Chunk : Stream {
		ScsDecompiler _parent;
		InstructionEncoding _instructionEncoding;
		byte[] _code;
		int _position;

		public int _index;
		public int _start;
		public int _end;
		public bool _isIncomplete;

		public List<int> Labels = new();
		public List<int> ReturnAddresses = new();
		public ChunkKind Kind = ChunkKind.None;
		public List<ScsPart> Body = new();
		public Exception? Error;
		public int LastPosition;

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => false;

		public override long Length => _end;
		public override long Position {
			get => _position;
			set => Seek(value, SeekOrigin.Begin);
		}

		public Chunk(ScsDecompiler parent, int index, int start, int end) {
			_parent = parent;
			_instructionEncoding = parent._instructionEncoding;
			_code = parent._code;

			_index = index;
			_start = start;
			_end = end;
		}

		public void Preprocess() {
			Reset();
			try {
				Disassemble();
			} catch (Exception e) {
				Error = e;
			}
		}

		public void Postprocess() {
			if (Kind != ChunkKind.None && Labels.Count == 1) {
				Reset();
				try {
					Decode();
				} catch (Exception e) {
					Error = e;
				}
			}

			bool dataNext = (_index == _parent._chunks.Length - 1) || (_parent._chunks[_index + 1].Kind != ChunkKind.None);

			if (!_isIncomplete && dataNext) {
				if (CheckChunkPadding()) {
					Error = null;
					_position = _end;
					UpdateLastPosition();
				}
			}
		}

		void Disassemble() {
			while (Position < Length) {
				var instruction = _instructionEncoding.Decode(this);
				AddInsn(instruction);
			}
		}

		void Decode() {
			switch (Kind) {
				case ChunkKind.Int16Table: {
					DecodeInt16Table();
					break;
				}
				case ChunkKind.Int32Table: {
					DecodeInt32Table();
					break;
				}
				case ChunkKind.AdrTable: {
					DecodeAdrTable();
					break;
				}
				case ChunkKind.TextTable: {
					DecodeTextTable();
					break;
				}
				case ChunkKind.NameIdTable: {
					DecodeNameIdTable();
					break;
				}
				case ChunkKind.EncycDataTable: {
					DecodeEncycDataTable();
					break;
				}
				case ChunkKind.EncycSortTable: {
					DecodeEncycSortTable();
					break;
				}
				case ChunkKind.MesModeFormatTable: {
					DecodeMesModeFormatTable();
					break;
				}
				default: {
					throw new NotImplementedException(Kind.ToString());
				}
			}
		}

		void DecodeInt16Table() {
			while (Position < Length)
				AddInsn("dw", DecodeInt16());
		}

		void DecodeInt32Table() {
			while (Position < Length)
				AddInsn("dd", DecodeInt32());
		}

		void DecodeAdrTable() {
			while (Position < Length)
				AddInsn("Adr", DecodeInt16());
		}

		void DecodeTextTable() {
			while (Position < Length)
				AddInsn("StringID", DecodeInt32());
		}

		void DecodeNameIdTable() {
			SetIncomplete();
			while (true) {
				var id = DecodeInt16();
				AddInsn("dw", id);
				if (id.GetInt() == 0xFFFF)
					break;

				AddInsn("StringID", DecodeInt32());
				AddInsn("StringID", DecodeInt32());
			}
			SetComplete();
		}

		void DecodeEncycDataTable() {
			SetIncomplete();
			int index = 0;
			while (true) {
				var value0 = DecodeInt16();
				AddInsn("dw", value0);
				if (value0.GetInt() == 0xFF)
					break;

				AddComment($"tip #{index}");
				index++;

				// Category
				AddInsn("StringID", DecodeInt32());

				// Name
				AddInsn("StringID", DecodeInt32());

				// Pronounciation
				AddInsn("StringID", DecodeInt32());

				// Sorting key
				AddInsn("StringID", DecodeInt32());

				// Content
				AddInsn("StringID", DecodeInt32());
			}
			SetComplete();
		}

		void DecodeEncycSortTable() {
			SetIncomplete();

			AddInsn("StringID", DecodeInt32());
			AddInsn("StringID", DecodeInt32());

			while (true) {
				var value0 = DecodeInt16();
				AddInsn("dw", value0);
				if (value0.GetInt() == 0xFFFF)
					break;
			}

			while (true) {
				var value0 = DecodeInt16();
				AddInsn("dw", value0);
				if (value0.GetInt() == 0xFFFF)
					break;
			}

			SetComplete();
		}

		void DecodeMesModeFormatTable() {
			int index = 0;
			while (Position < Length) {
				var value0 = DecodeInt16();
				string? comment = index switch {
					 0 => "display mode",
					 1 => "message window ID",
					 2 => "message window position X",
					 3 => "message window position Y",
					 4 => "name display mode",
					 5 => "max name width",
					 6 => "name fixed position X",
					 7 => "name fixed position Y",
					 8 => "name character width",
					 9 => "name character height",
					10 => "max line width",
					11 => "line icon mode",
					12 => "line icon fixed position X",
					13 => "line icon fixed position Y",
					14 => "text character width",
					15 => "text character height",
					16 => "ruby character width",
					17 => "ruby character height",
					18 => "text line spacing",
					19 => "ruby line spacing",
					_ => null,
				};
				if (comment is not null)
					AddComment(comment);
				AddInsn("dw", value0);
				index++;
			}
		}

		Expression DecodeInt16() {
			int value = 0;
			value |= GetByte() << 00;
			value |= GetByte() << 08;
			return new NumberExpression(value);
		}

		Expression DecodeInt32() {
			int value = 0;
			value |= GetByte() << 00;
			value |= GetByte() << 08;
			value |= GetByte() << 16;
			value |= GetByte() << 24;
			return new NumberExpression(value);
		}

		byte GetByte() {
			var value = ReadByte();
			if (value < 0)
				throw new EndOfStreamException();
			return (byte)value;
		}

		void Reset() {
			Error = null;
			Body.Clear();

			Position = _start;
			UpdateLastPosition();
			SetComplete();
		}

		public void AddComment(string text) {
			Body.Add(new ScsComment(text));
		}

		public void AddInsn(string name, params Expression[] operands) {
			AddInsn(new Instruction {
				Name = name,
				Operands = operands,
			});
		}

		public void AddInsn(Instruction instruction) {
			ScsInstruction scsInsn = new(instruction);
			scsInsn.Offset = LastPosition;
			Body.Add(scsInsn);
			UpdateLastPosition();
		}

		public void UpdateLastPosition() {
			LastPosition = _position;
		}

		void SetIncomplete() {
			_isIncomplete = true;
		}

		void SetComplete() {
			_isIncomplete = false;
		}

		bool CheckChunkPadding() {
			if (_end % 4 != 0)
				return false;
			int offset = LastPosition;
			int remaining = _end - offset;
			if (remaining is < 1 or >= 4)
				return false;
			while (offset < _end) {
				if (_code[offset] != 0)
					return false;
				offset++;
			}
			return true;
		}

		public override void Flush() { }

		public override int Read(byte[] buffer, int offset, int count) {
			int remaining = _end - _position;
			count = Math.Min(count, remaining);
			Array.Copy(_code, _position, buffer, offset, count);
			_position += count;
			return count;
		}

		public override long Seek(long offset, SeekOrigin origin) {
			long originPosition = origin switch {
				SeekOrigin.Begin => 0,
				SeekOrigin.Current => _position,
				SeekOrigin.End => _end,
				_ => throw new ArgumentException(),
			};
			long position = originPosition + offset;
			if (position < _start)
				throw new IOException("Seek out of range");
			if (position > _end)
				throw new EndOfStreamException();
			_position = (int)position;
			return position;
		}

		public override void SetLength(long value) {
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count) {
			throw new NotSupportedException();
		}
	}

	enum ChunkKind {
		None,
		Int16Table,
		Int32Table,
		AdrTable,
		TextTable,
		NameIdTable,
		EncycDataTable,
		EncycSortTable,
		MesModeFormatTable,
	}
}
