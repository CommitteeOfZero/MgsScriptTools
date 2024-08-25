namespace MagesScriptTool;

sealed class StringTokenChunk : StringToken {
	public readonly string Value;
	public readonly bool Italic;

	public StringTokenChunk(string value, bool italic) {
		Value = value;
		Italic = italic;
	}
}
