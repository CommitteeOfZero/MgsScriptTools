namespace MgsScriptTool;

sealed class ExpressionNodeIdentifier : ExpressionNode {
	public readonly string Name;

	public ExpressionNodeIdentifier(string identifier) {
		Name = identifier;
	}
}
