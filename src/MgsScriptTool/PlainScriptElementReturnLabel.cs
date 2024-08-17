namespace MgsScriptTool;

sealed class PlainScriptElementReturnLabel : PlainScriptElement {
	public readonly int Index;

	public PlainScriptElementReturnLabel(int index) {
		Index = index;
	}
}
