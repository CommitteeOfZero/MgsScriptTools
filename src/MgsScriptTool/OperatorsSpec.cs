using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class OperatorsSpec {
	static Dictionary<int, OperatorSpec> _opcodes = [];
	static Dictionary<OperatorKind, OperatorSpec> _kinds = [];

	static OperatorsSpec() {
		ImmutableArray<OperatorSpec> specs = [
			new(01, OperatorKind.Mul,        precedence: 9, left: 1, right: 1),
			new(02, OperatorKind.Div,        precedence: 9, left: 1, right: 1),
			new(03, OperatorKind.Add,        precedence: 7, left: 1, right: 1),
			new(04, OperatorKind.Sub,        precedence: 7, left: 1, right: 1),
			new(05, OperatorKind.Mod,        precedence: 8, left: 1, right: 1),
			new(06, OperatorKind.Lsh,        precedence: 6, left: 1, right: 1),
			new(07, OperatorKind.Rsh,        precedence: 6, left: 1, right: 1),
			new(08, OperatorKind.And,        precedence: 5, left: 1, right: 1),
			new(09, OperatorKind.Xor,        precedence: 3, left: 1, right: 1),
			new(10, OperatorKind.Or,         precedence: 4, left: 1, right: 1),
			new(11, OperatorKind.Not,        precedence: 2, left: 0, right: 1),
			new(12, OperatorKind.Eq,         precedence: 1, left: 1, right: 1),
			new(13, OperatorKind.Ne,         precedence: 1, left: 1, right: 1),
			new(14, OperatorKind.Le,         precedence: 1, left: 1, right: 1),
			new(15, OperatorKind.Ge,         precedence: 1, left: 1, right: 1),
			new(16, OperatorKind.Lt,         precedence: 1, left: 1, right: 1),
			new(17, OperatorKind.Gt,         precedence: 1, left: 1, right: 1),

			new(20, OperatorKind.Assign,     precedence: 0, left: 1, right: 1),
			new(21, OperatorKind.AssignMul,  precedence: 0, left: 1, right: 1),
			new(22, OperatorKind.AssignDiv,  precedence: 0, left: 1, right: 1),
			new(23, OperatorKind.AssignAdd,  precedence: 0, left: 1, right: 1),
			new(24, OperatorKind.AssignSub,  precedence: 0, left: 1, right: 1),
			new(25, OperatorKind.AssignMod,  precedence: 0, left: 1, right: 1),
			new(26, OperatorKind.AssignLsh,  precedence: 0, left: 1, right: 1),
			new(27, OperatorKind.AssignRsh,  precedence: 0, left: 1, right: 1),
			new(28, OperatorKind.AssignAnd,  precedence: 0, left: 1, right: 1),
			new(29, OperatorKind.AssignOr,   precedence: 0, left: 1, right: 1),
			new(30, OperatorKind.AssignXor,  precedence: 0, left: 1, right: 1),
			new(32, OperatorKind.Incr,       precedence: 0, left: 1, right: 0),
			new(33, OperatorKind.Decr,       precedence: 0, left: 1, right: 0),

			new(40, OperatorKind.FuncWork,   precedence: 10, left: 0, right: 1),
			new(41, OperatorKind.FuncFlag,   precedence: 10, left: 0, right: 1),
			new(42, OperatorKind.FuncMem,    precedence: 10, left: 0, right: 2),
			new(43, OperatorKind.FuncLabel,  precedence: 10, left: 0, right: 1),
			new(45, OperatorKind.FuncThread, precedence: 10, left: 0, right: 1),
			new(51, OperatorKind.FuncRandom, precedence: 10, left: 0, right: 1),
		];
		foreach (OperatorSpec spec in specs) {
			_opcodes[spec.Opcode] = spec;
			_kinds[spec.Kind] = spec;
		}
	}

	public static OperatorSpec GetSpec(int opcode) {
		return _opcodes[opcode];
	}

	public static OperatorSpec GetSpec(OperatorKind kind) {
		return _kinds[kind];
	}
}
