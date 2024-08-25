using System.Collections.Immutable;
using System.Diagnostics;

namespace MagesScriptTool;

static class ExpressionEncoding {
	public static void Encode(Stream stream, ExpressionNode expression) {
		ImmutableArray<ExpressionToken> tokens = new ExpressionEncoder(expression).Encode();
		ExpressionTokenListEncoding.Encode(stream, tokens);
	}

	public static ExpressionNode Decode(Stream stream) {
		ImmutableArray<ExpressionToken> tokens = ExpressionTokenListEncoding.Decode(stream);
		return new ExpressionDecoder(tokens).Decode();
	}

	sealed class ExpressionEncoder {
		readonly ExpressionNode _expression;
		readonly List<ExpressionToken> _tokens = [];

		public ExpressionEncoder(ExpressionNode expression) {
			_expression = expression;
		}

		public ImmutableArray<ExpressionToken> Encode() {
			Encode(_expression, 0);
			return [.._tokens];
		}

		void Encode(ExpressionNode expression, int precedence) {
			switch (expression) {
				case ExpressionNodeNumber literal: {
					_tokens.Add(new ExpressionTokenLiteral(literal.Value, precedence));
					break;
				}
				case ExpressionNodeOperation operation: {
					EncodeOperation(operation, precedence);
					break;
				}
				default: {
					throw new NotImplementedException(expression.GetType().Name);
				}
			}
		}

		void EncodeOperation(ExpressionNodeOperation operation, int precedence) {
			OperatorSpec spec = OperatorsSpec.GetSpec(operation.Kind);
			Debug.Assert(operation.Left.Length == spec.Left);
			Debug.Assert(operation.Right.Length == spec.Right);
			foreach (ExpressionNode subexpression in operation.Left) {
				Encode(subexpression, precedence + 20);
			}
			_tokens.Add(new ExpressionTokenOperator(spec.Opcode, precedence + spec.Precedence));
			foreach (ExpressionNode subexpression in operation.Right) {
				Encode(subexpression, precedence + 20);
			}
		}
	}

	sealed class ExpressionDecoder {
		readonly ImmutableArray<ExpressionToken> _tokens;
		int _offset = 0;

		public ExpressionDecoder(ImmutableArray<ExpressionToken> tokens) {
			_tokens = tokens;
		}

		public ExpressionNode Decode() {
			List<ExpressionNode> result = DecodeExpressions(0);
			if (result.Count != 1) {
				throw new Exception("Invalid expression.");
			}
			return result[0];
		}

		List<ExpressionNode> DecodeExpressions(int minPriority) {
			List<ExpressionNode> stack = [];
			while (_offset < _tokens.Length) {
				ExpressionToken token = _tokens[_offset];
				if (token.IsLowerThan(minPriority)) {
					break;
				}
				_offset++;
				switch (token) {
					case ExpressionTokenOperator { Opcode: int opcode, Priority: int priority }: {
						OperatorSpec spec = OperatorsSpec.GetSpec(opcode);
						List<ExpressionNode> left = [];
						for (int i = 0; i < spec.Left; i++) {
							if (stack.Count > 0) {
								left.Add(stack[^1]);
								stack.RemoveAt(stack.Count - 1);
							} else {
								left.Add(new ExpressionNodeBlank());
							}
						}
						List<ExpressionNode> extra = DecodeExpressions(priority + 1);
						List<ExpressionNode> right = [];
						for (int i = 0; i < spec.Right; i++) {
							if (extra.Count > 0) {
								right.Add(extra[0]);
								extra.RemoveAt(0);
							} else {
								right.Add(new ExpressionNodeBlank());
							}
						}
						stack.Add(new ExpressionNodeOperation(spec.Kind, [..left], [..right]));
						stack.AddRange(extra);
						break;
					}
					case ExpressionTokenLiteral { Value: int value }: {
						stack.Add(new ExpressionNodeNumber(value));
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
