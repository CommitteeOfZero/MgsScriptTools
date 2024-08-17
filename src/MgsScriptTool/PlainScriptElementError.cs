namespace MgsScriptTool;

sealed class PlainScriptElementError : PlainScriptElement {
	public readonly int Position;
	public readonly Exception Error;

	public PlainScriptElementError(int position, Exception error) {
		Position = position;
		Error = error;
	}
}
