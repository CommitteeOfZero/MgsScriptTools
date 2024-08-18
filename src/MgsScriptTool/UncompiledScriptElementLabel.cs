namespace MgsScriptTool;

sealed class UncompiledScriptElementLabel : UncompiledScriptElement {
	public readonly int Index;

	public UncompiledScriptElementLabel(int index) {
		Index = index;
	}
}
