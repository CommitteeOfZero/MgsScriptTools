namespace MagesScriptTool;

sealed class UncompiledScriptElementDataDirective : UncompiledScriptElement {
	public readonly DataDirective Value;

	public UncompiledScriptElementDataDirective(DataDirective value) {
		Value = value;
	}
}
