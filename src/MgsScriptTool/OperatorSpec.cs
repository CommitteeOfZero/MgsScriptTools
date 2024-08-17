namespace MgsScriptTool;

sealed class OperatorSpec {
	public readonly OperatorKind Kind;
	public readonly int Opcode;
	public readonly int Precedence;
	public readonly int Left;
	public readonly int Right;

	public OperatorSpec(OperatorKind kind, int opcode, int precedence, int left, int right) {
		Kind = kind;
		Opcode = opcode;
		Precedence = precedence;
		Left = left;
		Right = right;
	}
}
