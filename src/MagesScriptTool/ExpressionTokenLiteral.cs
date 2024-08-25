namespace MagesScriptTool;

sealed class ExpressionTokenLiteral : ExpressionToken {
	public readonly int Value;
	public readonly int Priority;

	public ExpressionTokenLiteral(int value, int priority) {
		Value = value;
		Priority = priority;
	}

	public override bool IsLowerThan(int priority) {
		return false;
	}
}
