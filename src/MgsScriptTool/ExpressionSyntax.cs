using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace MgsScriptTool;

static class ExpressionSyntax {
	public static void Format(StringBuilder builder, ExpressionNode expression) {
		new ExpressionFormatter(builder, expression).Format();
	}

	public static ExpressionNode Parse(TextStream stream) {
		return new ExpressionParser(stream).Parse();
	}

	sealed class ExpressionFormatter {
		readonly StringBuilder _builder;
		readonly ExpressionNode _expression;

		public ExpressionFormatter(StringBuilder builder, ExpressionNode expression) {
			_builder = builder;
			_expression = expression;
		}

		public void Format() {
			FormatInner(_expression);
		}

		void FormatParentheses(ExpressionNode expression) {
			_builder.Append('(');
			FormatInner(expression);
			_builder.Append(')');
		}

		void FormatBrackets(ExpressionNode expression) {
			_builder.Append('[');
			FormatInner(expression);
			_builder.Append(']');
		}

		void FormatOperand(ExpressionNode expression, int externalPrecedence) {
			if (expression is ExpressionNodeOperation { Kind: OperatorKind kind } && OperatorsSpec.GetSpec(kind).Precedence < externalPrecedence) {
				FormatParentheses(expression);
			} else {
				FormatInner(expression);
			}
		}

		void FormatInner(ExpressionNode expression) {
			switch (expression) {
				case ExpressionNodeNumber literal: {
					_builder.Append(literal.Value);
					break;
				}
				case ExpressionNodeIdentifier identifier: {
					_builder.Append(identifier.Name);
					break;
				}
				case ExpressionNodeOperation operation: {
					FormatOperation(operation);
					break;
				}
				case ExpressionNodeBlank: {
					break;
				}
				default: {
					throw new NotImplementedException(expression.GetType().Name);
				}
			}
		}

		void FormatOperation(ExpressionNodeOperation operation) {
			int precedence = OperatorsSpec.GetSpec(operation.Kind).Precedence;
			foreach (ExpressionNode subExpression in operation.Left) {
				FormatOperand(subExpression, precedence);
				if (subExpression is not ExpressionNodeBlank) {
					_builder.Append(' ');
				}
			}
			_builder.Append(GetSymbol(operation.Kind));
			switch (operation.Kind) {
				case OperatorKind.Not: {
					FormatOperand(operation.Right[0], precedence + 1);
					break;
				}
				case OperatorKind.FuncWork:
				case OperatorKind.FuncFlag:
				case OperatorKind.FuncLabel:
				case OperatorKind.FuncThread:
				case OperatorKind.FuncRandom: {
					FormatParentheses(operation.Right[0]);
					break;
				}
				case OperatorKind.FuncMem: {
					FormatBrackets(operation.Right[0]);
					FormatParentheses(operation.Right[1]);
					break;
				}
				default: {
					foreach (ExpressionNode subExpression in operation.Right) {
						if (subExpression is not ExpressionNodeBlank) {
							_builder.Append(' ');
						}
						FormatOperand(subExpression, precedence + 1);
					}
					break;
				}
			}
		}

		static string GetSymbol(OperatorKind kind) {
			return kind switch {
				OperatorKind.Assign => "=",
				OperatorKind.AssignMul => "*=",
				OperatorKind.AssignDiv => "/=",
				OperatorKind.AssignAdd => "+=",
				OperatorKind.AssignSub => "-=",
				OperatorKind.AssignMod => "%=",
				OperatorKind.AssignLsh => "<<=",
				OperatorKind.AssignRsh => ">>=",
				OperatorKind.AssignAnd => "&=",
				OperatorKind.AssignOr => "|=",
				OperatorKind.AssignXor => "^=",
				OperatorKind.Incr => "++",
				OperatorKind.Decr => "--",

				OperatorKind.Eq => "==",
				OperatorKind.Ne => "!=",
				OperatorKind.Le => "<=",
				OperatorKind.Ge => ">=",
				OperatorKind.Lt => "<",
				OperatorKind.Gt => ">",

				OperatorKind.Not => "!",

				OperatorKind.Xor => "^",

				OperatorKind.Or => "|",

				OperatorKind.And => "&",

				OperatorKind.Lsh => "<<",
				OperatorKind.Rsh => ">>",

				OperatorKind.Add => "+",
				OperatorKind.Sub => "-",

				OperatorKind.Mod => "%",

				OperatorKind.Mul => "*",
				OperatorKind.Div => "/",

				OperatorKind.FuncWork => "$W",
				OperatorKind.FuncFlag => "$F",
				OperatorKind.FuncMem => "$MR",
				OperatorKind.FuncLabel => "$L",
				OperatorKind.FuncThread => "$T",
				OperatorKind.FuncRandom => "$R",

				_ => throw new NotImplementedException(kind.ToString()),
			};
		}
	}

	sealed class ExpressionParser {
		readonly TextStream _stream;

		public ExpressionParser(TextStream stream) {
			_stream = stream;
		}

		public ExpressionNode Parse() {
			return ParseAssign();
		}

		ExpressionNode ParseAssign() {
			ExpressionNode left = ParseCompare();
			while (true) {
				OperatorKind? kind = TryParseAssignOperator();
				if (kind is null) {
					break;
				}
				ParseUtils.SkipHSpaceComments(_stream);
				ImmutableArray<ExpressionNode> right;
				switch (kind) {
					case OperatorKind.Incr or OperatorKind.Decr: {
						right = [];
						break;
					}
					default: {
						right = [ParseCompare()];
						break;
					}
				}
				left = new ExpressionNodeOperation(kind.Value, [left], right);
			}
			return left;
		}

		OperatorKind? TryParseAssignOperator() {
			OperatorKind? result = _stream.Peek(0) switch {
				'=' => _stream.Peek(1) switch {
					'=' => null,
					_ => OperatorKind.Assign,
				},
				'*' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignMul,
					_ => null,
				},
				'/' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignDiv,
					_ => null,
				},
				'+' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignAdd,
					'+' => OperatorKind.Incr,
					_ => null,
				},
				'-' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignSub,
					'-' => _stream.Peek(2) switch {
						< '0' or > '9' => OperatorKind.Decr, // TODO: determine if this is necessary
						_ => null,
					},
					_ => null,
				},
				'%' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignMod,
					_ => null,
				},
				'<' => _stream.Peek(1) switch {
					'<' => _stream.Peek(2) switch {
						'=' => OperatorKind.AssignLsh,
						_ => null,
					},
					_ => null,
				},
				'>' => _stream.Peek(1) switch {
					'>' => _stream.Peek(2) switch {
						'=' => OperatorKind.AssignRsh,
						_ => null,
					},
					_ => null,
				},
				'&' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignAnd,
					_ => null,
				},
				'|' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignOr,
					_ => null,
				},
				'^' => _stream.Peek(1) switch {
					'=' => OperatorKind.AssignXor,
					_ => null,
				},
				_ => null,
			};
			switch (result) {
				case null: {
					break;
				}
				case OperatorKind.Assign: {
					_stream.Skip(1);
					break;
				}
				case OperatorKind.AssignLsh or OperatorKind.AssignRsh: {
					_stream.Skip(3);
					break;
				}
				default: {
					_stream.Skip(2);
					break;
				}
			}
			return result;
		}

		ExpressionNode ParseCompare() {
			ExpressionNode left = ParseNot();
			while (true) {
				OperatorKind? kind = TryParseCompareOperator();
				if (kind is null) {
					break;
				}
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseNot();
				left = new ExpressionNodeOperation(kind.Value, [left], [right]);
			}
			return left;
		}

		OperatorKind? TryParseCompareOperator() {
			OperatorKind? result = _stream.Peek(0) switch {
				'=' => _stream.Peek(1) switch {
					'=' => OperatorKind.Eq,
					_ => null,
				},
				'!' => _stream.Peek(1) switch {
					'=' => OperatorKind.Ne,
					_ => null,
				},
				'<' => _stream.Peek(1) switch {
					'<' => null,
					'=' => OperatorKind.Le,
					_ => OperatorKind.Lt,
				},
				'>' => _stream.Peek(1) switch {
					'>' => null,
					'=' => OperatorKind.Ge,
					_ => OperatorKind.Gt,
				},
				_ => null,
			};
			switch (result) {
				case null: {
					break;
				}
				case OperatorKind.Lt or OperatorKind.Gt: {
					_stream.Skip(1);
					break;
				}
				default: {
					_stream.Skip(2);
					break;
				}
			}
			return result;
		}

		ExpressionNode ParseNot() {
			if (_stream.Peek(0) != '!' || _stream.Peek(1) == '=') {
				return ParseXor();
			}
			_stream.Skip(1);
			ParseUtils.SkipHSpaceComments(_stream);
			ExpressionNode right = ParseNot();
			return new ExpressionNodeOperation(OperatorKind.Not, [], [right]);
		}

		ExpressionNode ParseXor() {
			ExpressionNode left = ParseOr();
			while (true) {
				if (_stream.Peek(0) != '^' || _stream.Peek(1) == '=') {
					break;
				}
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseOr();
				left = new ExpressionNodeOperation(OperatorKind.Xor, [left], [right]);
			}
			return left;
		}

		ExpressionNode ParseOr() {
			ExpressionNode left = ParseAnd();
			while (true) {
				if (_stream.Peek(0) != '|' || _stream.Peek(1) == '=') {
					break;
				}
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseAnd();
				left = new ExpressionNodeOperation(OperatorKind.Or, [left], [right]);
			}
			return left;
		}

		ExpressionNode ParseAnd() {
			ExpressionNode left = ParseShift();
			while (true) {
				if (_stream.Peek(0) != '&' || _stream.Peek(1) == '=') {
					break;
				}
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseShift();
				left = new ExpressionNodeOperation(OperatorKind.And, [left], [right]);
			}
			return left;
		}

		ExpressionNode ParseShift() {
			ExpressionNode left = ParseAddSub();
			while (true) {
				OperatorKind? kind = TryParseShiftOperator();
				if (kind is null) {
					break;
				}
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseAddSub();
				left = new ExpressionNodeOperation(kind.Value, [left], [right]);
			}
			return left;
		}

		OperatorKind? TryParseShiftOperator() {
			OperatorKind? result = _stream.Peek(0) switch {
				'<' => _stream.Peek(1) switch {
					'<' => _stream.Peek(2) switch {
						'=' => null,
						_ => OperatorKind.Lsh,
					},
					_ => null,
				},
				'>' => _stream.Peek(1) switch {
					'>' => _stream.Peek(2) switch {
						'=' => null,
						_ => OperatorKind.Rsh,
					},
					_ => null,
				},
				_ => null,
			};
			if (result is not null) {
				_stream.Skip(2);
			}
			return result;
		}

		ExpressionNode ParseAddSub() {
			ExpressionNode left = ParseMod();
			while (true) {
				OperatorKind? kind = TryParseAddSubOperator();
				if (kind is null) {
					break;
				}
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseMod();
				left = new ExpressionNodeOperation(kind.Value, [left], [right]);
			}
			return left;
		}

		OperatorKind? TryParseAddSubOperator() {
			OperatorKind? result = _stream.Peek(0) switch {
				'+' => _stream.Peek(1) switch {
					'+' => null,
					'=' => null,
					_ => OperatorKind.Add,
				},
				'-' => _stream.Peek(1) switch {
					'-' => null,
					'=' => null,
					_ => OperatorKind.Sub,
				},
				_ => null,
			};
			if (result is not null) {
				_stream.Skip(1);
			}
			return result;
		}

		ExpressionNode ParseMod() {
			ExpressionNode left = ParseMulDiv();
			while (true) {
				if (_stream.Peek(0) != '%' || _stream.Peek(1) == '=') {
					break;
				}
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseMulDiv();
				left = new ExpressionNodeOperation(OperatorKind.Mod, [left], [right]);
			}
			return left;
		}

		ExpressionNode ParseMulDiv() {
			ExpressionNode left = ParseFunc();
			while (true) {
				OperatorKind? kind = TryParseMulDivOperator();
				if (kind is null) {
					break;
				}
				ParseUtils.SkipHSpaceComments(_stream);
				ExpressionNode right = ParseFunc();
				left = new ExpressionNodeOperation(kind.Value, [left], [right]);
			}
			return left;
		}

		OperatorKind? TryParseMulDivOperator() {
			OperatorKind? result = _stream.Peek(0) switch {
				'*' => _stream.Peek(1) switch {
					'=' => null,
					_ => OperatorKind.Mul,
				},
				'/' => _stream.Peek(1) switch {
					'=' => null,
					_ => OperatorKind.Div,
				},
				_ => null,
			};
			if (result is not null) {
				_stream.Skip(1);
			}
			return result;
		}

		ExpressionNode ParseFunc() {
			ExpressionNode result;
			if (_stream.Peek(0) == '(') {
				result = ParseParentheses();
			} else if (DetectNumber()) {
				result = ParseNumber();
			} else if (IsIdentifierStart(_stream.Peek(0))) {
				result = ParseIdentifier();
			} else if (_stream.Peek(0) == '$') {
				OperatorKind kind = ParseFuncOperator();
				List<ExpressionNode> operands = [];
				switch (kind) {
					case OperatorKind.FuncMem: {
						if (_stream.Peek(0) != '[') {
							throw new ParsingException("Expected '['.");
						}
						operands.Add(ParseBrackets());
						if (_stream.Peek(0) != '(') {
							throw new ParsingException("Expected '('.");
						}
						operands.Add(ParseParentheses());
						break;
					}
					default: {
						if (_stream.Peek(0) != '(') {
							throw new ParsingException("Expected '('.");
						}
						operands.Add(ParseParentheses());
						break;
					}
				}
				result = new ExpressionNodeOperation(kind, [], [..operands]);
			} else {
				throw new ParsingException("Expected parenthesis, a number, an identifier, a function or an unary operator.");
			}
			ParseUtils.SkipHSpaceComments(_stream);
			return result;
		}

		OperatorKind ParseFuncOperator() {
			Debug.Assert(_stream.Peek(0) == '$');
			TextStream.Position startPos = _stream.Tell();
			_stream.Skip(1);
			string name = "$";
			while (_stream.Peek(0) is >= 'A' and <= 'Z') {
				name += _stream.Next();
			}
			OperatorKind? kind = name switch {
				"$W" => OperatorKind.FuncWork,
				"$F" => OperatorKind.FuncFlag,
				"$MR" => OperatorKind.FuncMem,
				"$L" => OperatorKind.FuncLabel,
				"$T" => OperatorKind.FuncThread,
				"$R" => OperatorKind.FuncRandom,
				_ => null,
			};
			if (kind is null) {
				_stream.Seek(startPos);
				throw new ParsingException($"Unrecognized function: {name}.");
			}
			return kind.Value;
		}

		ExpressionNodeIdentifier ParseIdentifier() {
			Debug.Assert(IsIdentifierStart(_stream.Peek(0)));
			string name = "";
			while (IsIdentifierPart(_stream.Peek(0))) {
				name += _stream.Next();
			}
			return new(name);
		}

		ExpressionNode ParseParentheses() {
			Debug.Assert(_stream.Peek(0) == '(');
			_stream.Skip(1);
			ParseUtils.SkipHSpaceComments(_stream);
			ExpressionNode result = ParseAssign();
			if (!ParseUtils.TrySkip(_stream, ')')) {
				throw new ParsingException("Expected ')'.");
			}
			return result;
		}

		ExpressionNode ParseBrackets() {
			Debug.Assert(_stream.Peek(0) == '[');
			_stream.Skip(1);
			ParseUtils.SkipHSpaceComments(_stream);
			ExpressionNode result = ParseAssign();
			if (!ParseUtils.TrySkip(_stream, ']')) {
				throw new ParsingException("Expected ']'.");
			}
			return result;
		}

		ExpressionNodeNumber ParseNumber() {
			Debug.Assert(DetectNumber());
			bool sign = ParseUtils.TrySkip(_stream, '-');
			uint value;
			if (ParseUtils.TrySkip(_stream, "0x") || ParseUtils.TrySkip(_stream, "0X")) {
				value = ParseHex();
			} else {
				value = ParseDecimal();
			}
			ParseUtils.SkipHSpaceComments(_stream);
			if (sign) {
				value = unchecked(0U - value);
			}
			return new(unchecked((int)value));
		}

		uint ParseHex() {
			uint result = 0;
			bool success = false;
			while (true) {
				char ch = _stream.Peek(0);
				uint digit;
				if (ch is >= '0' and <= '9') {
					digit = 0x0 + (uint)(ch - '0');
				} else if (ch is >= 'A' and <= 'F') {
					digit = 0xA + (uint)(ch - 'A');
				} else if (ch is >= 'a' and <= 'f') {
					digit = 0xA + (uint)(ch - 'a');
				} else {
					break;
				}
				_stream.Next();
				success = true;
				result = unchecked(result*0x10 + digit);
			}
			if (!success) {
				throw new ParsingException("Expected a hex digit.");
			}
			return result;
		}

		uint ParseDecimal() {
			uint result = 0;
			bool success = false;
			while (true) {
				char ch = _stream.Peek(0);
				uint digit;
				if (ch is >= '0' and <= '9') {
					digit = 0 + (uint)(ch - '0');
				} else {
					break;
				}
				_stream.Next();
				success = true;
				result = unchecked(result*10 + digit);
			}
			if (!success) {
				throw new ParsingException("Expected a decimal digit or \"0x\".");
			}
			return result;
		}

		bool DetectNumber() {
			char ch = _stream.Peek(0);
			if (IsDigit(ch)) {
				return true;
			}
			if (ch is not '-') {
				return false;
			}
			return IsDigit(_stream.Peek(1));
		}

		static bool IsDigit(char ch) {
			return ch is >= '0' and <= '9';
		}

		static bool IsIdentifierStart(char ch) {
			return ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';
		}

		static bool IsIdentifierPart(char ch) {
			return IsIdentifierStart(ch) || IsDigit(ch);
		}
	}
}
