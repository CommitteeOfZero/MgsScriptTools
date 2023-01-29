namespace MgsScriptTools;

public class MesTagsSpec {
	Dictionary<MesTagKind, StringTagSpec> _kinds = new();
	Dictionary<int, StringTagSpec> _opcodes = new();

	public MesTagsSpec() {
		StringTagSpec[] specs = {
			new(0x00, MesTagKind.Newline),
			new(0x01, MesTagKind.NameStart),
			new(0x02, MesTagKind.NameEnd),
			new(0x03, MesTagKind.PauseEndLine),
			new(0x04, MesTagKind.Color, OperandKind.Calc),
			//new(0x04, MesTagKind.Color, OperandKind.UInt8), // TODO: move out to the spec bank
			new(0x05, MesTagKind.E),
			new(0x06, MesTagKind.K),
			new(0x07, MesTagKind.Wait, OperandKind.UInt8),
			new(0x08, MesTagKind.PauseEndPage),
			new(0x09, MesTagKind.RubyStart),
			new(0x0A, MesTagKind.RubyText),
			new(0x0B, MesTagKind.RubyEnd),
			new(0x0C, MesTagKind.Size, OperandKind.Int16),
			
			new(0x0E, MesTagKind.LineSync),
			new(0x0F, MesTagKind.LineCenter),
			new(0x10, MesTagKind.LineL),
			new(0x11, MesTagKind.LineFloat, OperandKind.Int16),
			new(0x12, MesTagKind.Space, OperandKind.Int16),
			new(0x13, MesTagKind.PrintHankaku, OperandKind.Int16),
			new(0x14, MesTagKind.PrintZenkaku, OperandKind.Int16),
			new(0x15, MesTagKind.Evaluate, OperandKind.Calc),
			new(0x16, MesTagKind.Dictionary, OperandKind.Int16),
			
			new(0x18, MesTagKind.PauseClearPage),
			//new(0x19, MesCommandKind.Auto),
			new(0x19, MesTagKind.Auto, OperandKind.Int16),
			new(0x1A, MesTagKind.AutoClearPage, OperandKind.Int16),
			new(0x1B, MesTagKind.FN, OperandKind.UInt8),

			new(0x1E, MesTagKind.RubyCenter),
			new(0x1F, MesTagKind.Newline_1F),

			new(0x31, MesTagKind.LineR),
		};
		foreach (var spec in specs) {
			_kinds[spec.Kind] = spec;
			_opcodes[spec.Opcode] = spec;
		}
	}

	public StringTagSpec GetSpec(MesTagKind kind) {
		return _kinds[kind];
	}

	public StringTagSpec GetSpec(int opcode) {
		return _opcodes[opcode];
	}
}

public class StringTagSpec {
	public int Opcode;
	public MesTagKind Kind;
	public OperandKind[] Operands;

	public StringTagSpec(int opcode, MesTagKind kind, params OperandKind[] operands) {
		Opcode = opcode;
		Kind = kind;
		Operands = operands;
	}
}
