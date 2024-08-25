namespace MagesScriptTool;

sealed class UncompiledScriptElementInstruction : UncompiledScriptElement {
	public readonly Instruction Value;

	public UncompiledScriptElementInstruction(Instruction value) {
		Value = value;
	}
}
