namespace MagesScriptTool;

abstract class ExpressionToken {
	public abstract bool IsLowerThan(int priority);
}
