using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class ScriptDecompiler {
	readonly InstructionEncoding _instructionEncoding;

	readonly ImmutableArray<byte> _code;
	readonly ImmutableArray<int> _labels;
	readonly ImmutableArray<int> _returnLabels;

	readonly SortedDictionary<int, Chunk> _chunks = [];
	readonly Dictionary<int, Chunk> _chunkMap = [];

	readonly Dictionary<UncompiledScriptElementInstruction, int> _instructionPositions = [];

	public ScriptDecompiler(InstructionEncoding instructionEncoding, CompiledScript compiledScript) {
		_instructionEncoding = instructionEncoding;

		_code = compiledScript.Code;
		_labels = compiledScript.Labels;
		_returnLabels = compiledScript.ReturnLabels;
	}

	public (ImmutableArray<UncompiledScriptElement>, ImmutableDictionary<UncompiledScriptElementInstruction, int>) Decompile() {
		InitializeChunks();
		foreach (Chunk chunk in _chunks.Values) {
			chunk.Preprocess();
		}
		foreach (Chunk chunk in _chunks.Values) {
			AnalyzeChunk(chunk);
		}
		foreach (Chunk chunk in _chunks.Values) {
			chunk.Postprocess();
		}

		List<UncompiledScriptElement> elements = [];
		foreach (Chunk chunk in _chunks.Values) {
			foreach (int index in chunk.Labels) {
				elements.Add(new UncompiledScriptElementLabel(index));
			}
			foreach (int index in chunk.ReturnLabels) {
				elements.Add(new UncompiledScriptElementReturnLabel(index));
			}
			elements.AddRange(chunk.Body);
			if (chunk.Error is not null) {
				elements.Add(new UncompiledScriptElementError(chunk.LastPosition, chunk.Error));
			}
			if (chunk.LastPosition != chunk._end) {
				elements.Add(new UncompiledScriptElementRaw(_code[chunk.LastPosition..chunk._end]));
			}
		}
		return ([..elements], _instructionPositions.ToImmutableDictionary());
	}

	void InitializeChunks() {
		int[] markers = [..new SortedSet<int>(_labels.Concat(_returnLabels))];

		for (int i = 0; i < markers.Length; i++) {
			int endPosition;
			if (i + 1 >= markers.Length) {
				endPosition = _code.Length;
			} else {
				endPosition = markers[i + 1];
			}
			Chunk chunk = new(this, i, markers[i], endPosition);
			_chunks[i] = chunk;
			_chunkMap[markers[i]] = chunk;
		}

		for (int i = 0; i < _labels.Length; i++) {
			_chunkMap[_labels[i]].Labels.Add(i);
		}
		for (int i = 0; i < _returnLabels.Length; i++) {
			_chunkMap[_returnLabels[i]].ReturnLabels.Add(i);
		}
	}

	void AnalyzeChunk(Chunk chunk) {
		foreach (UncompiledScriptElement element in chunk.Body) {
			if (element is UncompiledScriptElementInstruction { Value: Instruction instruction }) {
				AnalyzeInstruction(instruction);
			}
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

		foreach (ExpressionNode operand in instruction.Operands) {
			AnalyzeExpression(operand);
		}
	}

	void AnalyzeExpression(ExpressionNode expression) {
		if (expression is not ExpressionNodeOperation operation) {
			return;
		}
		if (operation.Kind == OperatorKind.FuncLabel) {
			if (operation.Right[0] is ExpressionNodeNumber { Value: int index }) {
				SetLabelKind(index, ChunkKind.Int32Table);
			}
		}
		foreach (ExpressionNode operand in operation.Left) {
			AnalyzeExpression(operand);
		}
		foreach (ExpressionNode operand in operation.Right) {
			AnalyzeExpression(operand);
		}
	}

	void SetLabelKind(int label, ChunkKind kind) {
		if (label < 0 || label > _labels.Length) {
			return;
		}
		int position = _labels[label];
		_chunkMap[position].Kind = kind;
	}

	sealed class Chunk : Stream {
		readonly ScriptDecompiler _parent;
		readonly InstructionEncoding _instructionEncoding;
		readonly ImmutableArray<byte> _code;

		readonly public int _index;
		readonly public int _start;
		readonly public int _end;

		int _position;
		public bool _isIncomplete;

		public List<int> Labels = [];
		public List<int> ReturnLabels = [];
		public ChunkKind Kind = ChunkKind.None;
		public List<UncompiledScriptElement> Body = [];
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

		public Chunk(ScriptDecompiler parent, int index, int start, int end) {
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

			bool dataNext = (_index == _parent._chunks.Count - 1) || (_parent._chunks[_index + 1].Kind != ChunkKind.None);

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
				Instruction instruction = _instructionEncoding.Decode(this);
				AddInstruction(instruction);
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
			while (Position < Length) {
				AddInstruction(new("dw", [DecodeInt16()]));
			}
		}

		void DecodeInt32Table() {
			while (Position < Length) {
				AddInstruction(new("dd", [DecodeInt32()]));
			}
		}

		void DecodeAdrTable() {
			while (Position < Length) {
				AddInstruction(new("Adr", [DecodeInt16()]));
			}
		}

		void DecodeTextTable() {
			while (Position < Length) {
				AddInstruction(new("StringID", [DecodeInt32()]));
			}
		}

		void DecodeNameIdTable() {
			SetIncomplete();
			while (true) {
				ExpressionNode id = DecodeInt16();
				AddInstruction(new("dw", [id]));
				if (id.GetInt() == 0xFFFF) {
					break;
				}

				AddInstruction(new("StringID", [DecodeInt32()]));
				AddInstruction(new("StringID", [DecodeInt32()]));
			}
			SetComplete();
		}

		void DecodeEncycDataTable() {
			SetIncomplete();
			int index = 0;
			while (true) {
				ExpressionNode value0 = DecodeInt16();
				AddInstruction(new("dw", [value0]));
				if (value0.GetInt() == 0xFF) {
					break;
				}

				AddComment($"tip #{index}");
				index++;

				// Category
				AddInstruction(new("StringID", [DecodeInt32()]));

				// Name
				AddInstruction(new("StringID", [DecodeInt32()]));

				// Pronounciation
				AddInstruction(new("StringID", [DecodeInt32()]));

				// Sorting key
				AddInstruction(new("StringID", [DecodeInt32()]));

				// Content
				AddInstruction(new("StringID", [DecodeInt32()]));
			}
			SetComplete();
		}

		void DecodeEncycSortTable() {
			SetIncomplete();

			AddInstruction(new("StringID", [DecodeInt32()]));
			AddInstruction(new("StringID", [DecodeInt32()]));

			while (true) {
				ExpressionNode value0 = DecodeInt16();
				AddInstruction(new("dw", [value0]));
				if (value0.GetInt() == 0xFFFF) {
					break;
				}
			}

			while (true) {
				ExpressionNode value0 = DecodeInt16();
				AddInstruction(new("dw", [value0]));
				if (value0.GetInt() == 0xFFFF) {
					break;
				}
			}

			SetComplete();
		}

		void DecodeMesModeFormatTable() {
			int index = 0;
			while (Position < Length) {
				ExpressionNode value0 = DecodeInt16();
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
				if (comment is not null) {
					AddComment(comment);
				}
				AddInstruction(new("dw", [value0]));
				index++;
			}
		}

		ExpressionNodeNumber DecodeInt16() {
			int value = 0;
			value |= GetByte() << 0;
			value |= GetByte() << 8;
			return new(value);
		}

		ExpressionNodeNumber DecodeInt32() {
			int value = 0;
			value |= GetByte() <<  0;
			value |= GetByte() <<  8;
			value |= GetByte() << 16;
			value |= GetByte() << 24;
			return new(value);
		}

		byte GetByte() {
			int value = ReadByte();
			if (value < 0) {
				throw new EndOfStreamException();
			}
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
			Body.Add(new UncompiledScriptElementComment(text));
		}

		public void AddInstruction(Instruction instruction) {
			UncompiledScriptElementInstruction element = new(instruction);
			_parent._instructionPositions[element] = LastPosition;
			Body.Add(element);
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
			if (_end % 4 != 0) {
				return false;
			}
			int offset = LastPosition;
			int remaining = _end - offset;
			if (remaining is < 1 or >= 4) {
				return false;
			}
			while (offset < _end) {
				if (_code[offset] != 0) {
					return false;
				}
				offset++;
			}
			return true;
		}

		public override void Flush() {}

		public override int Read(byte[] buffer, int offset, int count) {
			int remaining = _end - _position;
			count = Math.Min(count, remaining);
			_code.AsSpan(_position, count).CopyTo(buffer.AsSpan());
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
			if (position < _start) {
				throw new IOException("Seek out of range.");
			}
			if (position > _end) {
				throw new EndOfStreamException();
			}
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
