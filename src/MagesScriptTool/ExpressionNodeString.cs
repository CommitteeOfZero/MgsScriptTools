namespace MagesScriptTool;

sealed class ExpressionNodeString : ExpressionNode {
	public readonly string Value;

	public ExpressionNodeString(string value) {
		Value = value;
	}

    public override string GetString() {
		return Value;
    }
}
