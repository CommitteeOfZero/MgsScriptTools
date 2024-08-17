using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class StringTagsSpec {
	readonly ImmutableDictionary<StringTagKind, StringTagSpec> _kinds;
	readonly ImmutableDictionary<int, StringTagSpec> _opcodes;

	public StringTagsSpec() {
		ImmutableArray<StringTagSpec> specs = [
			new(0x00, StringTagKind.Newline, []),
			new(0x01, StringTagKind.NameStart, []),
			new(0x02, StringTagKind.NameEnd, []),
			new(0x03, StringTagKind.PauseEndLine, []),
			new(0x04, StringTagKind.Color, [OperandKind.Expr]),
			//new(0x04, MesTagKind.Color, [OperandKind.UInt8]), // TODO: move out to the spec bank
			new(0x05, StringTagKind.E, []),
			new(0x06, StringTagKind.K, []),
			new(0x07, StringTagKind.Wait, [OperandKind.UInt8]),
			new(0x08, StringTagKind.PauseEndPage, []),
			new(0x09, StringTagKind.RubyStart, []),
			new(0x0A, StringTagKind.RubyText, []),
			new(0x0B, StringTagKind.RubyEnd, []),
			new(0x0C, StringTagKind.Size, [OperandKind.Int16]),
			
			new(0x0E, StringTagKind.LineSync, []),
			new(0x0F, StringTagKind.LineCenter, []),
			new(0x10, StringTagKind.LineL, []),
			new(0x11, StringTagKind.LineFloat, [OperandKind.Int16]),
			new(0x12, StringTagKind.Space, [OperandKind.Int16]),
			new(0x13, StringTagKind.PrintHankaku, [OperandKind.Int16]),
			new(0x14, StringTagKind.PrintZenkaku, [OperandKind.Int16]),
			new(0x15, StringTagKind.Evaluate, [OperandKind.Expr]),
			new(0x16, StringTagKind.Dictionary, [OperandKind.Int16]),
			
			new(0x18, StringTagKind.PauseClearPage, []),
			//new(0x19, MesCommandKind.Auto, []),
			new(0x19, StringTagKind.Auto, [OperandKind.Int16]),
			new(0x1A, StringTagKind.AutoClearPage, [OperandKind.Int16]),
			new(0x1B, StringTagKind.FN, [OperandKind.UInt8]),

			new(0x1E, StringTagKind.RubyCenter, []),
			new(0x1F, StringTagKind.Newline_1F, []),

			new(0x31, StringTagKind.LineR, []),
		];

		Dictionary<StringTagKind, StringTagSpec> kinds = [];
		Dictionary<int, StringTagSpec> opcodes = [];
		foreach (var spec in specs) {
			kinds[spec.Kind] = spec;
			opcodes[spec.Opcode] = spec;
		}
		_kinds = kinds.ToImmutableDictionary();
		_opcodes = opcodes.ToImmutableDictionary();
	}

	public StringTagSpec GetSpec(StringTagKind kind) {
		return _kinds[kind];
	}

	public StringTagSpec GetSpec(int opcode) {
		return _opcodes[opcode];
	}
}
