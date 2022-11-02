using System.Diagnostics;

namespace MgsScriptTools;

public class CalcExpressionEncoding {
	public static void Encode(Stream stream, Expression expression) {
		CalcToken[] tokens = Encode(expression);
		CalcEncoding.Encode(stream, tokens);
	}

	public static Expression Decode(Stream stream) {
		CalcToken[] tokens = CalcEncoding.Decode(stream);
		return new CalcExpressionDecoder(tokens).Decode();
	}

	public static CalcToken[] Encode(Expression expression) {
		return new CalcExpressionEncoder().Encode(expression);
	}

	public static Expression Decode(CalcToken[] tokens) {
		return new CalcExpressionDecoder(tokens).Decode();
	}

	class CalcExpressionEncoder {
		List<CalcToken> _tokens;

		public CalcExpressionEncoder() {
			_tokens = new();
		}

		public CalcToken[] Encode(Expression expression) {
			Encode(expression, 0);
			return _tokens.ToArray();
		}

		void Encode(Expression expression, int precedence) {
			switch (expression) {
				case NumberExpression literal: {
					_tokens.Add(new CalcLiteral(literal.Value, precedence));
					break;
				}
				case OperationExpression operation: {
					EncodeOperation(operation, precedence);
					break;
				}
				default: {
					throw new NotImplementedException(expression.GetType().Name);
				}
			}
		}

		void EncodeOperation(OperationExpression operation, int precedence) {
			var spec = CalcExpressionSpec.GetSpec(operation.Kind);
			Debug.Assert(operation.Left.Length == spec.Left);
			Debug.Assert(operation.Right.Length == spec.Right);
			foreach (Expression subexpression in operation.Left)
				Encode(subexpression, precedence + 20);
			_tokens.Add(new CalcOperator(spec.Opcode, precedence + spec.Precedence));
			foreach (Expression subexpression in operation.Right)
				Encode(subexpression, precedence + 20);
		}
	}

	class CalcExpressionDecoder {
		CalcToken[] _tokens;
		int _offset;

		public CalcExpressionDecoder(CalcToken[] tokens) {
			_tokens = tokens;
			_offset = 0;
		}

		public Expression Decode() {
			var result = DecodeExpressions(0);
			if (result.Count != 1)
				throw new Exception($"Invalid expression");
			return result[0];
		}

		List<Expression> DecodeExpressions(int minPriority) {
			List<Expression> stack = new();
			while (_offset < _tokens.Length) {
				var token = _tokens[_offset];
				if (token.IsLowerThan(minPriority))
					break;
				_offset++;
				switch (token) {
					case CalcOperator { Opcode: var opcode, Priority: var priority }: {
						var spec = CalcExpressionSpec.GetSpec(opcode);
						var left = new Expression[spec.Left];
						for (int i = 0; i < left.Length; i++) {
							if (stack.Count > 0) {
								left[i] = stack[stack.Count - 1];
								stack.RemoveAt(stack.Count - 1);
							} else {
								left[i] = new BlankExpression();
							}
						}
						var extra = DecodeExpressions(priority + 1);
						var right = new Expression[spec.Right];
						for (int i = 0; i < right.Length; i++) {
							if (extra.Count > 0) {
								right[i] = extra[0];
								extra.RemoveAt(0);
							} else {
								right[i] = new BlankExpression();
							}
						}
						stack.Add(new OperationExpression(spec.Kind, left, right));
						stack.AddRange(extra);
						break;
					}
					case CalcLiteral { Value: var value }: {
						stack.Add(new NumberExpression(value));
						break;
					}
					default: {
						throw new NotImplementedException(token.GetType().Name);
					}
				}
			}
			return stack;
		}
	}
}
