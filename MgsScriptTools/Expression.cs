namespace MgsScriptTools;

public abstract class Expression {
	public virtual int GetInt() {
		// TODO: refactor with visitor pattern
		throw new NotImplementedException();
	}
}

public class OperationExpression : Expression {
	public OperatorKind Kind;
	public Expression[] Left;
	public Expression[] Right;

	public OperationExpression(OperatorKind kind, Expression[] left, Expression[] right) {
		Kind = kind;
		Left = left;
		Right = right;
	}
}

public class NumberExpression : Expression {
	public int Value;

	public NumberExpression(int value) {
		Value = value;
	}

	public override int GetInt() {
		return Value;
	}
}

public class IdentifierExpression : Expression {
	public string Name;

	public IdentifierExpression(string identifier) {
		Name = identifier;
	}
}

public class BlankExpression : Expression {
}

public enum OperatorKind {
	Mul,
	Div,
	Add,
	Sub,
	Mod,
	Lsh,
	Rsh,
	And,
	Xor,
	Or,
	Not,
	Eq,
	Ne,
	Le,
	Ge,
	Lt,
	Gt,

	Assign,
	AssignMul,
	AssignDiv,
	AssignAdd,
	AssignSub,
	AssignMod,
	AssignLsh,
	AssignRsh,
	AssignAnd,
	AssignOr,
	AssignXor,
	Incr,
	Decr,

	FuncWork,
	FuncFlag,
	FuncMem,
	FuncLabel,
	FuncThread,
	FuncRandom,
}
