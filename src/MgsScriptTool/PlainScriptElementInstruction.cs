namespace MgsScriptTool;

sealed class PlainScriptElementInstruction : PlainScriptElement {
	public readonly Instruction Value;

	public PlainScriptElementInstruction(Instruction value) {
		Value = value;
	}
}
