namespace MgsScriptTool;

sealed class PlainScriptElementLabel : PlainScriptElement {
	public readonly int Index;

	public PlainScriptElementLabel(int index) {
		Index = index;
	}
}
