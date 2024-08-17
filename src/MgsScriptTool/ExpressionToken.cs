namespace MgsScriptTool;

abstract class ExpressionToken {
	public abstract bool IsLowerThan(int priority);
}
