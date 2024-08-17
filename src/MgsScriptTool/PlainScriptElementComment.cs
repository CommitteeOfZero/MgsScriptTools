namespace MgsScriptTool;

sealed class PlainScriptElementComment : PlainScriptElement {
	public readonly string Text;

	public PlainScriptElementComment(string text) {
		Text = text;
	}
}
