using System.Collections.Immutable;

namespace MagesScriptTool;

sealed class ScriptCompiler {
	readonly InstructionEncoding _instructionEncoding;
	readonly DataDirectiveEncoding _dataDirectiveEncoding;
	readonly MemoryStream _stream = new();
	readonly SortedDictionary<int, int> _labelTable = [];
	readonly SortedDictionary<int, int> _returnLabelTable = [];

	public ScriptCompiler(InstructionEncoding instructionEncoding, DataDirectiveEncoding dataDirectiveEncoding) {
		_instructionEncoding = instructionEncoding;
		_dataDirectiveEncoding = dataDirectiveEncoding;
	}

	public CompiledScript Compile(ImmutableArray<UncompiledScriptElement> elements) {
		foreach (UncompiledScriptElement element in elements) {
			ProcessElement(element);
		}

		List<int> labels = [];
		foreach ((int index, int offset) in _labelTable) {
			while (labels.Count < index) {
				labels.Add(checked((int)_stream.Length));
			}
			labels.Add(offset);
		}

		List<int> returnLabels = [];
		foreach ((int index, int offset) in _returnLabelTable) {
			while (returnLabels.Count < index) {
				returnLabels.Add(checked((int)_stream.Length));
			}
			returnLabels.Add(offset);
		}

		return new([.._stream.ToArray()], [..labels], [..returnLabels]);
	}

	void ProcessElement(UncompiledScriptElement element) {
		switch (element) {
			case UncompiledScriptElementInstruction { Value: Instruction instruction }: {
				_instructionEncoding.Encode(_stream, instruction);
				break;
			}
			case UncompiledScriptElementDataDirective { Value: DataDirective dataDirective }: {
				_dataDirectiveEncoding.Encode(_stream, dataDirective);
				break;
			}
			case UncompiledScriptElementLabel { Index: int index }: {
				int offset = (int)_stream.Position;
				if (_labelTable.ContainsKey(index)) {
					throw new Exception($"Conflicting label: {index}.");
				}
				_labelTable[index] = offset;
				break;
			}
			case UncompiledScriptElementReturnLabel { Index: int index }: {
				int offset = (int)_stream.Position;
				if (_returnLabelTable.ContainsKey(index)) {
					throw new Exception($"Conflicting return label: {index}.");
				}
				_returnLabelTable[index] = offset;
				break;
			}
			case UncompiledScriptElementRaw { Data: ImmutableArray<byte> data }: {
				_stream.Write(data.AsSpan());
				break;
			}
			default: {
				throw new NotImplementedException(element.GetType().Name);
			}
		}
	}

}
