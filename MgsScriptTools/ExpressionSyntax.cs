using System.Diagnostics;
using System.Text;

namespace MgsScriptTools;

public class ExpressionSyntax {
	public static void Stringify(StringBuilder builder, Expression expression) {
		new ExpressionSyntaxStringifier(builder).Stringify(expression);
	}

	public static Expression Parse(TextStream stream) {
		return new ExpressionSyntaxParser(stream).Parse();
	}

	class ExpressionSyntaxStringifier {
		StringBuilder _builder;

		public ExpressionSyntaxStringifier(StringBuilder builder) {
			_builder = builder;
		}

		public void Stringify(Expression expression) {
			Inner(expression);
		}

		void Parentheses(Expression expression) {
			_builder.Append('(');
			Inner(expression);
			_builder.Append(')');
		}

		void Brackets(Expression expression) {
			_builder.Append('[');
			Inner(expression);
			_builder.Append(']');
		}

		void Operand(Expression expression, int externalPrecedence) {
			if (expression is OperationExpression { Kind: var kind } && CalcExpressionSpec.GetSpec(kind).Precedence < externalPrecedence) {
				Parentheses(expression);
			} else {
				Inner(expression);
			}
		}

		void Inner(Expression expression) {
			switch (expression) {
				case NumberExpression literal: {
					_builder.Append(literal.Value);
					break;
				}
				case IdentifierExpression identifier: {
					_builder.Append(identifier.Name);
					break;
				}
				case OperationExpression operation: {
					Operation(operation);
					break;
				}
				case BlankExpression: {
					break;
				}
				default: {
					throw new NotImplementedException(expression.GetType().Name);
				}
			}
		}

		void Operation(OperationExpression operation) {
			var precedence = CalcExpressionSpec.GetSpec(operation.Kind).Precedence;
			foreach (var subExpression in operation.Left) {
				Operand(subExpression, precedence);
				if (subExpression is not BlankExpression)
					_builder.Append(' ');
			}
			_builder.Append(GetSymbol(operation.Kind));
			switch (operation.Kind) {
				case OperatorKind.Not: {
					Operand(operation.Right[0], precedence + 1);
					break;
				}
				case OperatorKind.FuncWork:
				case OperatorKind.FuncFlag:
				case OperatorKind.FuncLabel:
				case OperatorKind.FuncThread:
				case OperatorKind.FuncRandom: {
					Parentheses(operation.Right[0]);
					break;
				}
				case OperatorKind.FuncMem: {
					Brackets(operation.Right[0]);
					Parentheses(operation.Right[1]);
					break;
				}
				default: {
					foreach (var subExpression in operation.Right) {
						if (subExpression is not BlankExpression)
							_builder.Append(' ');
						Operand(subExpression, precedence + 1);
					}
					break;
				}
			}
		}

		string GetSymbol(OperatorKind kind) {
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

	class ExpressionSyntaxParser {
		TextStream _stream;

		public ExpressionSyntaxParser(TextStream stream) {
			_stream = stream;
		}

		public Expression Parse() {
			return ParseAssign();
		}

		Expression ParseAssign() {
			var left = ParseCompare();
			while (true) {
				var kind = TryParseAssignOperator();
				if (kind is null)
					break;
				ParseUtils.SkipHSpaceComments(_stream);
				Expression[] right;
				switch (kind) {
					case OperatorKind.Incr or OperatorKind.Decr: {
						right = new Expression[0];
						break;
					}
					default: {
						right = new Expression[1];
						right[0] = ParseCompare();
						break;
					}
				}
				left = new OperationExpression(kind.Value, new Expression[] { left }, right);
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

		Expression ParseCompare() {
			var left = ParseNot();
			while (true) {
				var kind = TryParseCompareOperator();
				if (kind is null)
					break;
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseNot();
				left = new OperationExpression(kind.Value, new Expression[] { left }, new Expression[] { right });
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

		Expression ParseNot() {
			if (_stream.Peek(0) != '!' || _stream.Peek(1) == '=')
				return ParseXor();
			_stream.Skip(1);
			ParseUtils.SkipHSpaceComments(_stream);
			var right = ParseNot();
			return new OperationExpression(OperatorKind.Not, new Expression[0], new Expression[] { right });
		}

		Expression ParseXor() {
			var left = ParseOr();
			while (true) {
				if (_stream.Peek(0) != '^' || _stream.Peek(1) == '=')
					break;
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseOr();
				left = new OperationExpression(OperatorKind.Xor, new Expression[] { left }, new Expression[] { right });
			}
			return left;
		}

		Expression ParseOr() {
			var left = ParseAnd();
			while (true) {
				if (_stream.Peek(0) != '|' || _stream.Peek(1) == '=')
					break;
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseAnd();
				left = new OperationExpression(OperatorKind.Or, new Expression[] { left }, new Expression[] { right });
			}
			return left;
		}

		Expression ParseAnd() {
			var left = ParseShift();
			while (true) {
				if (_stream.Peek(0) != '&' || _stream.Peek(1) == '=')
					break;
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseShift();
				left = new OperationExpression(OperatorKind.And, new Expression[] { left }, new Expression[] { right });
			}
			return left;
		}

		Expression ParseShift() {
			var left = ParseAddSub();
			while (true) {
				var kind = TryParseShiftOperator();
				if (kind is null)
					break;
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseAddSub();
				left = new OperationExpression(kind.Value, new Expression[] { left }, new Expression[] { right });
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
			if (result is not null)
				_stream.Skip(2);
			return result;
		}

		Expression ParseAddSub() {
			var left = ParseMod();
			while (true) {
				var kind = TryParseAddSubOperator();
				if (kind is null)
					break;
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseMod();
				left = new OperationExpression(kind.Value, new Expression[] { left }, new Expression[] { right });
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
			if (result is not null)
				_stream.Skip(1);
			return result;
		}

		Expression ParseMod() {
			var left = ParseMulDiv();
			while (true) {
				if (_stream.Peek(0) != '%' || _stream.Peek(1) == '=')
					break;
				_stream.Skip(1);
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseMulDiv();
				left = new OperationExpression(OperatorKind.Mod, new Expression[] { left }, new Expression[] { right });
			}
			return left;
		}

		Expression ParseMulDiv() {
			var left = ParseFunc();
			while (true) {
				var kind = TryParseMulDivOperator();
				if (kind is null)
					break;
				ParseUtils.SkipHSpaceComments(_stream);
				var right = ParseFunc();
				left = new OperationExpression(kind.Value, new Expression[] { left }, new Expression[] { right });
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
			if (result is not null)
				_stream.Skip(1);
			return result;
		}

		Expression ParseFunc() {
			Expression result;
			if (_stream.Peek(0) == '(') {
				result = ParseParentheses();
			} else if (DetectNumber()) {
				result = ParseNumber();
			} else if (IsIdentifierStart(_stream.Peek(0))) {
				result = ParseIdentifier();
			} else if (_stream.Peek(0) == '$') {
				var kind = ParseFuncOperator();
				Expression[] operands;
				switch (kind) {
					case OperatorKind.FuncMem: {
						operands = new Expression[2];
						if (_stream.Peek(0) != '[')
							throw new ParsingException($"Expected '['");
						operands[0] = ParseBrackets();
						if (_stream.Peek(0) != '(')
							throw new ParsingException($"Expected '('");
						operands[1] = ParseParentheses();
						break;
					}
					default: {
						operands = new Expression[1];
						if (_stream.Peek(0) != '(')
							throw new ParsingException($"Expected '('");
						operands[0] = ParseParentheses();
						break;
					}
				}
				result = new OperationExpression(kind, new Expression[0], operands);
			} else {
				throw new ParsingException($"Expected parenthesis, a number, an identifier, a function or an unary operator");
			}
			ParseUtils.SkipHSpaceComments(_stream);
			return result;
		}

		OperatorKind ParseFuncOperator() {
			Debug.Assert(_stream.Peek(0) == '$');
			var startPos = _stream.Tell();
			_stream.Skip(1);
			string name = "$";
			while (_stream.Peek(0) is >= 'A' and <= 'Z')
				name += _stream.Next();
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
				throw new ParsingException($"Unrecognized function: {name}");
			}
			return kind.Value;
		}

		Expression ParseIdentifier() {
			Debug.Assert(IsIdentifierStart(_stream.Peek(0)));
			string name = "";
			while (IsIdentifierPart(_stream.Peek(0)))
				name += _stream.Next();
			return new IdentifierExpression(name);
		}

		Expression ParseParentheses() {
			Debug.Assert(_stream.Peek(0) == '(');
			_stream.Skip(1);
			ParseUtils.SkipHSpaceComments(_stream);
			var result = ParseAssign();
			if (!ParseUtils.TrySkip(_stream, ')'))
				throw new ParsingException($"Expected ')'");
			return result;
		}

		Expression ParseBrackets() {
			Debug.Assert(_stream.Peek(0) == '[');
			_stream.Skip(1);
			ParseUtils.SkipHSpaceComments(_stream);
			var result = ParseAssign();
			if (!ParseUtils.TrySkip(_stream, ']'))
				throw new ParsingException($"Expected ']'");
			return result;
		}

		NumberExpression ParseNumber() {
			Debug.Assert(DetectNumber());
			string s = "";
			if (_stream.Peek(0) == '-')
				s += _stream.Next();
			while (IsDigit(_stream.Peek(0)))
				s += _stream.Next();
			ParseUtils.SkipHSpaceComments(_stream);
			int value = int.Parse(s);
			return new(value);
		}

		bool DetectNumber() {
			char ch = _stream.Peek(0);
			if (IsDigit(ch))
				return true;
			if (ch is not '-')
				return false;
			return IsDigit(_stream.Peek(1));
		}

		bool IsDigit(char ch) {
			return ch is (>= '0' and <= '9');
		}

		bool IsIdentifierStart(char ch) {
			return ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';
		}

		bool IsIdentifierPart(char ch) {
			return IsIdentifierStart(ch) || IsDigit(ch);
		}
	}
}
