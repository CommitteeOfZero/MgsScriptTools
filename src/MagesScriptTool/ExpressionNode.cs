namespace MagesScriptTool;

abstract class ExpressionNode {
	public virtual int GetInt() {
		// TODO: refactor with visitor pattern
		throw new NotImplementedException();
	}
	
	public virtual string GetString() {
		// TODO: refactor with visitor pattern
		throw new NotImplementedException();
	}
}
