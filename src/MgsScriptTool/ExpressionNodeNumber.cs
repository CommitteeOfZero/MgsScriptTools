namespace MgsScriptTool;

sealed class ExpressionNodeNumber : ExpressionNode {
	public readonly int Value;

	public ExpressionNodeNumber(int value) {
		Value = value;
	}

	public override int GetInt() {
		return Value;
	}
}
