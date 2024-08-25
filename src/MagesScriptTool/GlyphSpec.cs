namespace MagesScriptTool;

class GlyphSpec {
	public readonly int Index;
	public readonly string Text;
	public readonly bool Regular;
	public readonly bool Italic;

	public GlyphSpec(int index, string text, bool regular, bool italic) {
		Index = index;
		Text = text;
		Regular = regular;
		Italic = italic;
	}
}
