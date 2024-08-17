namespace MgsScriptTool;

sealed class ExpressionTokenOperator : ExpressionToken {
	public readonly int Opcode;
	public readonly int Priority;

	public ExpressionTokenOperator(int opcode, int priority) {
		Opcode = opcode;
		Priority = priority;
	}

	public override bool IsLowerThan(int priority) {
		return Priority < priority;
	}
}
