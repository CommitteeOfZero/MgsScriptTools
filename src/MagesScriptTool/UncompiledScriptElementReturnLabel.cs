namespace MagesScriptTool;

sealed class UncompiledScriptElementReturnLabel : UncompiledScriptElement {
	public readonly int Index;

	public UncompiledScriptElementReturnLabel(int index) {
		Index = index;
	}
}
