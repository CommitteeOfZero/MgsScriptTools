namespace MgsScriptTool;

sealed class UncompiledScriptElementComment : UncompiledScriptElement {
	public readonly string Text;

	public UncompiledScriptElementComment(string text) {
		Text = text;
	}
}
