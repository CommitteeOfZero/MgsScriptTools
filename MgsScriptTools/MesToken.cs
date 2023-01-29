namespace MgsScriptTools;

public abstract class MesToken { }

public class MesChunk : MesToken {
	public string Value;
	public bool Italic;

	public MesChunk(string value, bool italic) {
		Value = value;
		Italic = italic;
	}
}

public class MesGlyph : MesToken {
	public int Value;

	public MesGlyph(int value) {
		Value = value;
	}
}

public class MesTag : MesToken {
	public MesTagKind Kind;
	public Expression[] Operands;

	public MesTag(MesTagKind kind, Expression[] operands) {
		Kind = kind;
		Operands = operands;
	}
}

public enum MesTagKind {
	Newline, // break line
	NameStart,
	NameEnd,
	PauseEndLine, // wait for key, then end line
	Color, // set the text color to `arg0`
	E, // breaks the line at the end
	K, // unknown
	Wait, // wait for `arg0` frames
	PauseEndPage, // wait for key, then end page
	RubyStart,
	RubyText,
	RubyEnd,
	Size, // set the font size to `arg0/1000`

	LineSync, // unconfirmed, seems to display lines in parallel
	LineCenter, // seems to align the line to the center
	LineL, // seems to align the line to the right
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

	LineR, // seems to align the line to the right
}
