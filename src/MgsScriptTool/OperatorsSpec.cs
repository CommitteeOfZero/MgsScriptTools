using System.Collections.Immutable;

namespace MgsScriptTool;

sealed class OperatorsSpec {
	static readonly Dictionary<OperatorKind, OperatorSpec> _byKind = [];
	static readonly Dictionary<int, OperatorSpec> _byOpcode = [];

	static OperatorsSpec() {
		ImmutableArray<OperatorSpec> specs = [
			new(OperatorKind.Mul,        01, precedence: 9, left: 1, right: 1),
			new(OperatorKind.Div,        02, precedence: 9, left: 1, right: 1),
			new(OperatorKind.Add,        03, precedence: 7, left: 1, right: 1),
			new(OperatorKind.Sub,        04, precedence: 7, left: 1, right: 1),
			new(OperatorKind.Mod,        05, precedence: 8, left: 1, right: 1),
			new(OperatorKind.Lsh,        06, precedence: 6, left: 1, right: 1),
			new(OperatorKind.Rsh,        07, precedence: 6, left: 1, right: 1),
			new(OperatorKind.And,        08, precedence: 5, left: 1, right: 1),
			new(OperatorKind.Xor,        09, precedence: 3, left: 1, right: 1),
			new(OperatorKind.Or,         10, precedence: 4, left: 1, right: 1),
			new(OperatorKind.Not,        11, precedence: 2, left: 0, right: 1),
			new(OperatorKind.Eq,         12, precedence: 1, left: 1, right: 1),
			new(OperatorKind.Ne,         13, precedence: 1, left: 1, right: 1),
			new(OperatorKind.Le,         14, precedence: 1, left: 1, right: 1),
			new(OperatorKind.Ge,         15, precedence: 1, left: 1, right: 1),
			new(OperatorKind.Lt,         16, precedence: 1, left: 1, right: 1),
			new(OperatorKind.Gt,         17, precedence: 1, left: 1, right: 1),

			new(OperatorKind.Assign,     20, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignMul,  21, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignDiv,  22, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignAdd,  23, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignSub,  24, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignMod,  25, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignLsh,  26, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignRsh,  27, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignAnd,  28, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignOr,   29, precedence: 0, left: 1, right: 1),
			new(OperatorKind.AssignXor,  30, precedence: 0, left: 1, right: 1),
			new(OperatorKind.Incr,       32, precedence: 0, left: 1, right: 0),
			new(OperatorKind.Decr,       33, precedence: 0, left: 1, right: 0),

			new(OperatorKind.FuncWork,   40, precedence: 10, left: 0, right: 1),
			new(OperatorKind.FuncFlag,   41, precedence: 10, left: 0, right: 1),
			new(OperatorKind.FuncMem,    42, precedence: 10, left: 0, right: 2),
			new(OperatorKind.FuncLabel,  43, precedence: 10, left: 0, right: 1),
			new(OperatorKind.FuncThread, 45, precedence: 10, left: 0, right: 1),
			new(OperatorKind.FuncRandom, 51, precedence: 10, left: 0, right: 1),
		];

		foreach (OperatorSpec spec in specs) {
			_byKind[spec.Kind] = spec;
			_byOpcode[spec.Opcode] = spec;
		}
	}

	public static OperatorSpec GetSpec(OperatorKind kind) {
		return _byKind[kind];
	}

	public static OperatorSpec GetSpec(int opcode) {
		return _byOpcode[opcode];
	}
}
