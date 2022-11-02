namespace MgsScriptTools;

public class MesStringSpec {
	Dictionary<MesCommandKind, MesCommandSpec> _kinds = new();
	Dictionary<int, MesCommandSpec> _opcodes = new();

	public MesStringSpec() {
		MesCommandSpec[] specs = {
			new(0x00, MesCommandKind.Newline),
			new(0x01, MesCommandKind.NameStart),
			new(0x02, MesCommandKind.NameEnd),
			new(0x03, MesCommandKind.PauseEndLine),
			new(0x04, MesCommandKind.Color, OperandKind.Calc),
			//new(0x04, MesCommandKind.Color, OperandKind.UInt8), // TODO: move out to the spec bank
			new(0x05, MesCommandKind.E),
			new(0x06, MesCommandKind.Unk_06),
			new(0x07, MesCommandKind.Wait, OperandKind.UInt8),
			new(0x08, MesCommandKind.PauseEndPage),
			new(0x09, MesCommandKind.RubyStart),
			new(0x0A, MesCommandKind.RubyText),
			new(0x0B, MesCommandKind.RubyEnd),
			new(0x0C, MesCommandKind.Size, OperandKind.Int16),
			
			new(0x0E, MesCommandKind.LineSync),
			new(0x0F, MesCommandKind.LineCenter),
			new(0x10, MesCommandKind.LineLight),
			new(0x11, MesCommandKind.LineFloat, OperandKind.Int16),
			new(0x12, MesCommandKind.Space, OperandKind.Int16),
			new(0x13, MesCommandKind.PrintHankaku, OperandKind.Int16),
			new(0x14, MesCommandKind.PrintZenkaku, OperandKind.Int16),
			new(0x15, MesCommandKind.Evaluate, OperandKind.Calc),
			new(0x16, MesCommandKind.Dictionary, OperandKind.Int16),
			
			new(0x18, MesCommandKind.PauseClearPage),
			//new(0x19, MesCommandKind.Auto),
			new(0x19, MesCommandKind.Auto, OperandKind.Int16),
			new(0x1A, MesCommandKind.AutoClearPage, OperandKind.Int16),
			new(0x1B, MesCommandKind.FN, OperandKind.UInt8),

			new(0x1E, MesCommandKind.RubyCenter),
			new(0x1F, MesCommandKind.Newline_1F),

			new(0x31, MesCommandKind.LineRight),
		};
		foreach (var spec in specs) {
			_kinds[spec.Kind] = spec;
			_opcodes[spec.Opcode] = spec;
		}
	}

	public MesCommandSpec GetSpec(MesCommandKind kind) {
		return _kinds[kind];
	}

	public MesCommandSpec GetSpec(int opcode) {
		return _opcodes[opcode];
	}
}

public class MesCommandSpec {
	public int Opcode;
	public MesCommandKind Kind;
	public OperandKind[] Operands;

	public MesCommandSpec(int opcode, MesCommandKind kind, params OperandKind[] operands) {
		Opcode = opcode;
		Kind = kind;
		Operands = operands;
	}
}

public class MesCommand {
	public MesCommandKind Kind;
	public Expression[] Operands;

	public MesCommand(MesCommandKind kind, Expression[] operands) {
		Kind = kind;
		Operands = operands;
	}
}

public enum MesCommandKind {
	Newline, // break line
	NameStart,
	NameEnd,
	PauseEndLine, // wait for key, then end line
	Color, // set the text color to `arg0`
	E, // breaks the line at the end
	Unk_06, // unknown
	Wait, // wait for `arg0` frames
	PauseEndPage, // wait for key, then end page
	RubyStart,
	RubyText,
	RubyEnd,
	Size, // set the font size to `arg0/1000`

	LineSync, // unconfirmed, seems to display lines in parallel
	LineCenter, // seems to align the line to the center
	LineLight, // seems to align the line to the right
	LineFloat, // seems to move the line vertically
	Space, // seems to moves the line horizontally
	PrintHankaku, // display the value of `$W(arg0)`
	PrintZenkaku, // unconfirmed, seems to display the value of `$W(arg0)`
	Evaluate, // evaluate `arg0`
	Dictionary, // trigger the tip #`arg0`

	PauseClearPage, // pause and mark NVL page for clear
	Auto, // wait for key for `arg0` frames, then end line
	AutoClearPage, // wait for key for `arg0` frames and mark NVL page for clear
	FN, // unconfirmed, possibly means "function"

	RubyCenter,
	Newline_1F, // break line

	LineRight, // seems to align the line to the right
}
