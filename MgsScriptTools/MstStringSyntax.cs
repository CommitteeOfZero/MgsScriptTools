using System.Text;

namespace MgsScriptTools;

public abstract class MstStringSyntax {
	public abstract void Stringify(StringBuilder builder, MstStringPart[] parts);
	public abstract MstStringPart[] Parse(TextStream reader);
}

public abstract class MstStringPart { }

public class MstStringChunk : MstStringPart {
	public string Value;
	public bool Italic;

	public MstStringChunk(string value, bool italic) {
		Value = value;
		Italic = italic;
	}
}

public class MstStringGlyph : MstStringPart {
	public int Value;

	public MstStringGlyph(int value) {
		Value = value;
	}
}

public class MstStringCommand : MstStringPart {
	public MesCommand Value;

	public MstStringCommand(MesCommand value) {
		Value = value;
	}
}
