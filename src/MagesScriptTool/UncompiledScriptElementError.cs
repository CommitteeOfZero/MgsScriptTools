namespace MagesScriptTool;

sealed class UncompiledScriptElementError : UncompiledScriptElement {
	public readonly int Position;
	public readonly Exception Error;

	public UncompiledScriptElementError(int position, Exception error) {
		Position = position;
		Error = error;
	}
}
