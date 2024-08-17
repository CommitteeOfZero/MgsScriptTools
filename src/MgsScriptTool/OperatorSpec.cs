namespace MgsScriptTool;

sealed class OperatorSpec {
	public readonly int Opcode;
	public readonly OperatorKind Kind;
	public readonly int Precedence;
	public readonly int Left;
	public readonly int Right;

	public OperatorSpec(int opcode, OperatorKind kind, int precedence, int left, int right) {
		Opcode = opcode;
		Kind = kind;
		Precedence = precedence;
		Left = left;
		Right = right;
	}
}
