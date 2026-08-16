using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Expressions;

namespace SkeletonKey.Evaluation;

/// <summary>
/// Evaluates the existing safe workflow expression language deterministically.
/// </summary>
/// <remarks>
/// The evaluator is stateless, thread-safe, culture-invariant, side-effect free, and performs no I/O, host access, reflection,
/// dynamic invocation, resource access, locator access, function registration, current-time access, or randomness.
/// </remarks>
public sealed class WorkflowExpressionEvaluator : IWorkflowExpressionEvaluator
{
    private readonly WorkflowExpressionParser _parser = new();

    /// <inheritdoc />
    public WorkflowValueResult Evaluate(
        string expression,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null)
    {
        return Evaluate(_parser.Parse(expression), context, limits);
    }

    /// <inheritdoc />
    public WorkflowValueResult Evaluate(
        WorkflowExpressionDocument expression,
        WorkflowValueResolutionContext context,
        WorkflowValueProcessingLimits? limits = null)
    {
        if (!expression.IsValid)
        {
            WorkflowExpressionDiagnostic diagnostic = expression.Diagnostics[0];
            return WorkflowValueResult.Failure(new WorkflowValueError(
                WorkflowValueErrorCode.InvalidExpression,
                diagnostic.Message,
                string.Empty,
                diagnostic.SourceSpan.Offset,
                diagnostic.SourceSpan.Length));
        }

        try
        {
            Evaluator evaluator = new(expression.OriginalText, context, limits ?? WorkflowValueProcessingLimits.Default);
            WorkflowValueResult result = evaluator.Evaluate();
            if (!result.IsSuccess)
            {
                return result;
            }

            WorkflowValueResult sizeResult = ResultLimit.Enforce(result.Value, limits ?? WorkflowValueProcessingLimits.Default, string.Empty);
            return sizeResult.IsSuccess ? result : sizeResult;
        }
        catch (OverflowException)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.NumericOverflow, "Expression numeric operation overflowed.", string.Empty));
        }
        catch (EvaluationLimitException exception)
        {
            return WorkflowValueResult.Failure(new WorkflowValueError(
                WorkflowValueErrorCode.EvaluationOperationLimitExceeded,
                "Expression evaluation operation limit was exceeded.",
                string.Empty,
                exception.Offset,
                exception.Length));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            JsonObject metadata = new() { ["type"] = exception.GetType().FullName };
            return WorkflowValueResult.Failure(new WorkflowValueError(WorkflowValueErrorCode.InternalEvaluationFailure, "Expression evaluation failed internally.", string.Empty, metadata: metadata));
        }
    }

    private sealed class Evaluator
    {
        private readonly string _text;
        private readonly WorkflowValueResolutionContext _context;
        private readonly WorkflowValueProcessingLimits _limits;
        private readonly IReadOnlyList<Token> _tokens;
        private int _position;
        private int _operations;

        public Evaluator(string text, WorkflowValueResolutionContext context, WorkflowValueProcessingLimits limits)
        {
            _text = text;
            _context = context;
            _limits = limits;
            _tokens = new Lexer(text).Lex();
        }

        public WorkflowValueResult Evaluate()
        {
            ValueResult value = ParseExpression();
            if (!value.IsSuccess)
            {
                return WorkflowValueResult.Failure(value.Error!);
            }

            return WorkflowValueResult.Success(value.Value!.ToJson());
        }

        private ValueResult ParseExpression()
        {
            return ParseConditional();
        }

        private ValueResult ParseConditional()
        {
            ValueResult condition = ParseNullCoalescing();
            if (!condition.IsSuccess)
            {
                return condition;
            }

            if (Match(TokenKind.Question))
            {
                if (condition.Value!.Kind != ValueKind.Boolean)
                {
                    return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Conditional expression requires a boolean condition.", Previous);
                }

                if (condition.Value.Boolean)
                {
                    ValueResult selected = ParseExpression();
                    if (!selected.IsSuccess)
                    {
                        return selected;
                    }

                    Expect(TokenKind.Colon);
                    SkipExpression();
                    return selected;
                }

                SkipExpressionUntilColon();
                Expect(TokenKind.Colon);
                return ParseExpression();
            }

            return condition;
        }

        private ValueResult ParseNullCoalescing()
        {
            ValueResult left = ParseLogicalOr();
            if (!left.IsSuccess)
            {
                return left;
            }

            if (Match(TokenKind.QuestionQuestion))
            {
                if (left.Value!.Kind == ValueKind.Null)
                {
                    return ParseNullCoalescing();
                }

                SkipExpression();
            }

            return left;
        }

        private ValueResult ParseLogicalOr()
        {
            ValueResult left = ParseLogicalAnd();
            while (Match(TokenKind.BarBar))
            {
                Token op = Previous;
                if (!left.IsSuccess)
                {
                    return left;
                }

                if (left.Value!.Kind != ValueKind.Boolean)
                {
                    return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Logical OR requires boolean operands.", op);
                }

                if (left.Value.Boolean)
                {
                    SkipLogicalAnd();
                    left = ValueResult.Success(Value.BooleanValue(true));
                    continue;
                }

                ValueResult right = ParseLogicalAnd();
                if (!right.IsSuccess)
                {
                    return right;
                }

                if (right.Value!.Kind != ValueKind.Boolean)
                {
                    return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Logical OR requires boolean operands.", op);
                }

                left = ValueResult.Success(Value.BooleanValue(right.Value.Boolean));
            }

            return left;
        }

        private ValueResult ParseLogicalAnd()
        {
            ValueResult left = ParseEquality();
            while (Match(TokenKind.AmpAmp))
            {
                Token op = Previous;
                if (!left.IsSuccess)
                {
                    return left;
                }

                if (left.Value!.Kind != ValueKind.Boolean)
                {
                    return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Logical AND requires boolean operands.", op);
                }

                if (!left.Value.Boolean)
                {
                    SkipEquality();
                    left = ValueResult.Success(Value.BooleanValue(false));
                    continue;
                }

                ValueResult right = ParseEquality();
                if (!right.IsSuccess)
                {
                    return right;
                }

                if (right.Value!.Kind != ValueKind.Boolean)
                {
                    return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Logical AND requires boolean operands.", op);
                }

                left = ValueResult.Success(Value.BooleanValue(right.Value.Boolean));
            }

            return left;
        }

        private ValueResult ParseEquality()
        {
            ValueResult left = ParseComparison();
            while (Current.Kind is TokenKind.EqualsEquals or TokenKind.BangEquals)
            {
                Token op = Advance();
                ValueResult right = ParseComparison();
                if (!left.IsSuccess)
                {
                    return left;
                }

                if (!right.IsSuccess)
                {
                    return right;
                }

                bool equals = DeepEquals(left.Value!, right.Value!);
                left = ValueResult.Success(Value.BooleanValue(op.Kind == TokenKind.EqualsEquals ? equals : !equals));
            }

            return left;
        }

        private ValueResult ParseComparison()
        {
            ValueResult left = ParseAdditive();
            while (Current.Kind is TokenKind.Less or TokenKind.LessEquals or TokenKind.Greater or TokenKind.GreaterEquals)
            {
                Token op = Advance();
                ValueResult right = ParseAdditive();
                if (!left.IsSuccess)
                {
                    return left;
                }

                if (!right.IsSuccess)
                {
                    return right;
                }

                left = Compare(left.Value!, right.Value!, op);
            }

            return left;
        }

        private ValueResult ParseAdditive()
        {
            ValueResult left = ParseMultiplicative();
            while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                Token op = Advance();
                ValueResult right = ParseMultiplicative();
                if (!left.IsSuccess)
                {
                    return left;
                }

                if (!right.IsSuccess)
                {
                    return right;
                }

                left = Additive(left.Value!, right.Value!, op);
            }

            return left;
        }

        private ValueResult ParseMultiplicative()
        {
            ValueResult left = ParseUnary();
            while (Current.Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
            {
                Token op = Advance();
                ValueResult right = ParseUnary();
                if (!left.IsSuccess)
                {
                    return left;
                }

                if (!right.IsSuccess)
                {
                    return right;
                }

                left = Multiplicative(left.Value!, right.Value!, op);
            }

            return left;
        }

        private ValueResult ParseUnary()
        {
            if (Current.Kind is TokenKind.Bang or TokenKind.Minus or TokenKind.Plus)
            {
                Token op = Advance();
                ValueResult operand = ParseUnary();
                if (!operand.IsSuccess)
                {
                    return operand;
                }

                CountOperation(op);
                if (op.Kind == TokenKind.Bang)
                {
                    return operand.Value!.Kind == ValueKind.Boolean
                        ? ValueResult.Success(Value.BooleanValue(!operand.Value.Boolean))
                        : Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Logical NOT requires a boolean operand.", op);
                }

                return operand.Value!.Kind == ValueKind.Number
                    ? ValueResult.Success(Value.NumberValue(op.Kind == TokenKind.Minus ? checked(-operand.Value.Number) : operand.Value.Number))
                    : Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Unary numeric operators require a number operand.", op);
            }

            return ParsePostfix();
        }

        private ValueResult ParsePostfix()
        {
            ValueResult value = ParsePrimary();
            while (true)
            {
                if (Match(TokenKind.Dot))
                {
                    Token member = Advance();
                    if (!value.IsSuccess)
                    {
                        return value;
                    }

                    value = AccessMember(value.Value!, member.Text, member);
                    continue;
                }

                if (Match(TokenKind.OpenBracket))
                {
                    Token index = Advance();
                    Expect(TokenKind.CloseBracket);
                    if (!value.IsSuccess)
                    {
                        return value;
                    }

                    value = index.Kind == TokenKind.String
                        ? AccessStringIndex(value.Value!, index.Text, index)
                        : AccessIntegerIndex(value.Value!, index.Text, index);
                    continue;
                }

                break;
            }

            return value;
        }

        private ValueResult ParsePrimary()
        {
            Token token = Advance();
            CountOperation(token);
            return token.Kind switch
            {
                TokenKind.Null => ValueResult.Success(Value.Null),
                TokenKind.True => ValueResult.Success(Value.BooleanValue(true)),
                TokenKind.False => ValueResult.Success(Value.BooleanValue(false)),
                TokenKind.Number => ParseNumber(token),
                TokenKind.String => ValueResult.Success(Value.StringValue(token.Text)),
                TokenKind.OpenParen => ParseGrouped(),
                TokenKind.Identifier when Current.Kind == TokenKind.OpenParen => ParseFunctionCall(token),
                TokenKind.Identifier => ResolveRoot(token),
                _ => Fail(WorkflowValueErrorCode.InvalidExpression, "Expression contains an unexpected token.", token),
            };
        }

        private ValueResult ParseGrouped()
        {
            ValueResult value = ParseExpression();
            Expect(TokenKind.CloseParen);
            return value;
        }

        private ValueResult ParseFunctionCall(Token name)
        {
            Expect(TokenKind.OpenParen);
            List<Func<ValueResult>> arguments = [];
            if (!Match(TokenKind.CloseParen))
            {
                while (Current.Kind != TokenKind.End)
                {
                    int start = _position;
                    SkipExpression();
                    int end = _position;
                    arguments.Add(() =>
                    {
                        int saved = _position;
                        _position = start;
                        ValueResult result = ParseExpression();
                        _position = saved;
                        return result;
                    });

                    if (Match(TokenKind.Comma))
                    {
                        continue;
                    }

                    Expect(TokenKind.CloseParen);
                    break;
                }
            }

            CountOperation(name);
            return InvokeFunction(name, arguments);
        }

        private ValueResult ResolveRoot(Token token)
        {
            return token.Text switch
            {
                "inputs" => ValueResult.Success(Value.FromJson(_context.ProjectInputs())),
                "variables" => ValueResult.Success(Value.FromJson(_context.ProjectVariables())),
                "nodes" => ValueResult.Success(Value.FromJson(_context.ProjectNodes())),
                "iterations" => ValueResult.Success(Value.FromJson(_context.ProjectIterations())),
                _ => Fail(WorkflowValueErrorCode.InvalidExpression, "Unknown expression root.", token),
            };
        }

        private ValueResult ParseNumber(Token token)
        {
            return decimal.TryParse(token.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)
                ? ValueResult.Success(Value.NumberValue(value))
                : Fail(WorkflowValueErrorCode.NumericOverflow, "Expression number is outside the supported decimal range.", token);
        }

        private ValueResult Additive(Value left, Value right, Token op)
        {
            CountOperation(op);
            if (op.Kind == TokenKind.Plus && left.Kind == ValueKind.String && right.Kind == ValueKind.String)
            {
                return ValueResult.Success(Value.StringValue(left.String + right.String));
            }

            if (left.Kind != ValueKind.Number || right.Kind != ValueKind.Number)
            {
                return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Additive operators require two numbers or two strings for concatenation.", op);
            }

            return ValueResult.Success(Value.NumberValue(op.Kind == TokenKind.Plus ? checked(left.Number + right.Number) : checked(left.Number - right.Number)));
        }

        private ValueResult Multiplicative(Value left, Value right, Token op)
        {
            CountOperation(op);
            if (left.Kind != ValueKind.Number || right.Kind != ValueKind.Number)
            {
                return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Numeric operators require number operands.", op);
            }

            if ((op.Kind is TokenKind.Slash or TokenKind.Percent) && right.Number == 0)
            {
                return Fail(WorkflowValueErrorCode.DivisionByZero, "Division or remainder by zero is not allowed.", op);
            }

            decimal result = op.Kind switch
            {
                TokenKind.Star => checked(left.Number * right.Number),
                TokenKind.Slash => left.Number / right.Number,
                _ => left.Number % right.Number,
            };

            return ValueResult.Success(Value.NumberValue(result));
        }

        private ValueResult Compare(Value left, Value right, Token op)
        {
            CountOperation(op);
            int comparison;
            if (left.Kind == ValueKind.Number && right.Kind == ValueKind.Number)
            {
                comparison = left.Number.CompareTo(right.Number);
            }
            else if (left.Kind == ValueKind.String && right.Kind == ValueKind.String)
            {
                comparison = string.Compare(left.String, right.String, StringComparison.Ordinal);
            }
            else
            {
                return Fail(WorkflowValueErrorCode.InvalidExpressionOperandType, "Relational comparisons require two numbers or two strings.", op);
            }

            bool result = op.Kind switch
            {
                TokenKind.Less => comparison < 0,
                TokenKind.LessEquals => comparison <= 0,
                TokenKind.Greater => comparison > 0,
                _ => comparison >= 0,
            };
            return ValueResult.Success(Value.BooleanValue(result));
        }

        private ValueResult AccessMember(Value value, string member, Token token)
        {
            CountOperation(token);
            return value.Kind == ValueKind.Object && value.Object.TryGetValue(member, out Value? memberValue)
                ? ValueResult.Success(memberValue.Clone())
                : Fail(WorkflowValueErrorCode.InvalidMemberAccess, "Object member is missing or cannot be accessed.", token);
        }

        private ValueResult AccessStringIndex(Value value, string member, Token token)
        {
            CountOperation(token);
            return value.Kind == ValueKind.Object && value.Object.TryGetValue(member, out Value? memberValue)
                ? ValueResult.Success(memberValue.Clone())
                : Fail(WorkflowValueErrorCode.InvalidIndexAccess, "String index requires an object with the requested property.", token);
        }

        private ValueResult AccessIntegerIndex(Value value, string indexText, Token token)
        {
            CountOperation(token);
            if (value.Kind != ValueKind.Array ||
                !int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index < 0 ||
                index >= value.Array.Count)
            {
                return Fail(WorkflowValueErrorCode.InvalidIndexAccess, "Integer index requires an array element in range.", token);
            }

            return ValueResult.Success(value.Array[index].Clone());
        }

        private ValueResult InvokeFunction(Token name, IReadOnlyList<Func<ValueResult>> arguments)
        {
            return name.Text switch
            {
                "size" => InvokeSize(arguments, name),
                "isEmpty" => InvokeIsEmpty(arguments, name),
                "contains" => InvokeContains(arguments, name),
                "startsWith" => InvokeStringPair(arguments, name, static (left, right) => left.StartsWith(right, StringComparison.Ordinal)),
                "endsWith" => InvokeStringPair(arguments, name, static (left, right) => left.EndsWith(right, StringComparison.Ordinal)),
                "trim" => InvokeString(arguments, name, static value => value.Trim()),
                "lower" => InvokeString(arguments, name, static value => value.ToLowerInvariant()),
                "upper" => InvokeString(arguments, name, static value => value.ToUpperInvariant()),
                "coalesce" => InvokeCoalesce(arguments),
                "toString" => InvokeToString(arguments, name),
                "toNumber" => InvokeToNumber(arguments, name),
                "toBoolean" => InvokeToBoolean(arguments, name),
                _ => Fail(WorkflowValueErrorCode.InvalidExpression, "Unknown expression function.", name),
            };
        }

        private ValueResult InvokeSize(IReadOnlyList<Func<ValueResult>> arguments, Token token)
        {
            ValueResult argument = arguments[0]();
            if (!argument.IsSuccess)
            {
                return argument;
            }

            return argument.Value!.Kind switch
            {
                ValueKind.String => ValueResult.Success(Value.NumberValue(argument.Value.String.EnumerateRunes().Count())),
                ValueKind.Array => ValueResult.Success(Value.NumberValue(argument.Value.Array.Count)),
                ValueKind.Object => ValueResult.Success(Value.NumberValue(argument.Value.Object.Count)),
                _ => Fail(WorkflowValueErrorCode.InvalidFunctionArgument, "size requires a string, array, or object.", token),
            };
        }

        private ValueResult InvokeIsEmpty(IReadOnlyList<Func<ValueResult>> arguments, Token token)
        {
            ValueResult argument = arguments[0]();
            if (!argument.IsSuccess)
            {
                return argument;
            }

            return argument.Value!.Kind switch
            {
                ValueKind.Null => ValueResult.Success(Value.BooleanValue(true)),
                ValueKind.String => ValueResult.Success(Value.BooleanValue(argument.Value.String.Length == 0)),
                ValueKind.Array => ValueResult.Success(Value.BooleanValue(argument.Value.Array.Count == 0)),
                ValueKind.Object => ValueResult.Success(Value.BooleanValue(argument.Value.Object.Count == 0)),
                _ => Fail(WorkflowValueErrorCode.InvalidFunctionArgument, "isEmpty requires null, string, array, or object.", token),
            };
        }

        private ValueResult InvokeContains(IReadOnlyList<Func<ValueResult>> arguments, Token token)
        {
            ValueResult container = arguments[0]();
            if (!container.IsSuccess)
            {
                return container;
            }

            ValueResult value = arguments[1]();
            if (!value.IsSuccess)
            {
                return value;
            }

            return container.Value!.Kind switch
            {
                ValueKind.String when value.Value!.Kind == ValueKind.String => ValueResult.Success(Value.BooleanValue(container.Value.String.Contains(value.Value.String, StringComparison.Ordinal))),
                ValueKind.Array => ValueResult.Success(Value.BooleanValue(container.Value.Array.Any(item => DeepEquals(item, value.Value!)))),
                ValueKind.Object when value.Value!.Kind == ValueKind.String => ValueResult.Success(Value.BooleanValue(container.Value.Object.ContainsKey(value.Value.String))),
                _ => Fail(WorkflowValueErrorCode.InvalidFunctionArgument, "contains received invalid argument types.", token),
            };
        }

        private ValueResult InvokeStringPair(IReadOnlyList<Func<ValueResult>> arguments, Token token, Func<string, string, bool> operation)
        {
            ValueResult left = arguments[0]();
            ValueResult right = arguments[1]();
            if (!left.IsSuccess)
            {
                return left;
            }

            if (!right.IsSuccess)
            {
                return right;
            }

            return left.Value!.Kind == ValueKind.String && right.Value!.Kind == ValueKind.String
                ? ValueResult.Success(Value.BooleanValue(operation(left.Value.String, right.Value.String)))
                : Fail(WorkflowValueErrorCode.InvalidFunctionArgument, $"{token.Text} requires two strings.", token);
        }

        private ValueResult InvokeString(IReadOnlyList<Func<ValueResult>> arguments, Token token, Func<string, string> operation)
        {
            ValueResult argument = arguments[0]();
            if (!argument.IsSuccess)
            {
                return argument;
            }

            return argument.Value!.Kind == ValueKind.String
                ? ValueResult.Success(Value.StringValue(operation(argument.Value.String)))
                : Fail(WorkflowValueErrorCode.InvalidFunctionArgument, $"{token.Text} requires a string.", token);
        }

        private ValueResult InvokeCoalesce(IReadOnlyList<Func<ValueResult>> arguments)
        {
            foreach (Func<ValueResult> argument in arguments)
            {
                ValueResult value = argument();
                if (!value.IsSuccess)
                {
                    return value;
                }

                if (value.Value!.Kind != ValueKind.Null)
                {
                    return value;
                }
            }

            return ValueResult.Success(Value.Null);
        }

        private ValueResult InvokeToString(IReadOnlyList<Func<ValueResult>> arguments, Token token)
        {
            ValueResult argument = arguments[0]();
            if (!argument.IsSuccess)
            {
                return argument;
            }

            return argument.Value!.Kind switch
            {
                ValueKind.Null => ValueResult.Success(Value.StringValue("null")),
                ValueKind.Boolean => ValueResult.Success(Value.StringValue(argument.Value.Boolean ? "true" : "false")),
                ValueKind.Number => ValueResult.Success(Value.StringValue(argument.Value.Number.ToString(CultureInfo.InvariantCulture))),
                ValueKind.String => argument,
                _ => Fail(WorkflowValueErrorCode.InvalidFunctionArgument, "toString rejects arrays and objects.", token),
            };
        }

        private ValueResult InvokeToNumber(IReadOnlyList<Func<ValueResult>> arguments, Token token)
        {
            ValueResult argument = arguments[0]();
            if (!argument.IsSuccess)
            {
                return argument;
            }

            if (argument.Value!.Kind == ValueKind.Number)
            {
                return argument;
            }

            if (argument.Value.Kind == ValueKind.String &&
                decimal.TryParse(argument.Value.String, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                return ValueResult.Success(Value.NumberValue(value));
            }

            return Fail(WorkflowValueErrorCode.InvalidFunctionArgument, "toNumber requires a number or invariant numeric string.", token);
        }

        private ValueResult InvokeToBoolean(IReadOnlyList<Func<ValueResult>> arguments, Token token)
        {
            ValueResult argument = arguments[0]();
            if (!argument.IsSuccess)
            {
                return argument;
            }

            if (argument.Value!.Kind == ValueKind.Boolean)
            {
                return argument;
            }

            if (argument.Value.Kind == ValueKind.String)
            {
                if (string.Equals(argument.Value.String, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return ValueResult.Success(Value.BooleanValue(true));
                }

                if (string.Equals(argument.Value.String, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return ValueResult.Success(Value.BooleanValue(false));
                }
            }

            if (argument.Value.Kind == ValueKind.Number)
            {
                if (argument.Value.Number == 0)
                {
                    return ValueResult.Success(Value.BooleanValue(false));
                }

                if (argument.Value.Number == 1)
                {
                    return ValueResult.Success(Value.BooleanValue(true));
                }
            }

            return Fail(WorkflowValueErrorCode.InvalidFunctionArgument, "toBoolean requires a boolean, true/false string, or number 0 or 1.", token);
        }

        private void SkipExpression()
        {
            int depth = 0;
            while (Current.Kind != TokenKind.End)
            {
                if (depth == 0 && Current.Kind is TokenKind.Comma or TokenKind.Colon or TokenKind.CloseParen or TokenKind.CloseBracket)
                {
                    return;
                }

                if (Current.Kind is TokenKind.OpenParen or TokenKind.OpenBracket)
                {
                    depth++;
                }
                else if (Current.Kind is TokenKind.CloseParen or TokenKind.CloseBracket && depth > 0)
                {
                    depth--;
                }

                Advance();
            }
        }

        private void SkipExpressionUntilColon()
        {
            int depth = 0;
            while (Current.Kind != TokenKind.End)
            {
                if (depth == 0 && Current.Kind == TokenKind.Colon)
                {
                    return;
                }

                if (Current.Kind is TokenKind.OpenParen or TokenKind.OpenBracket)
                {
                    depth++;
                }
                else if (Current.Kind is TokenKind.CloseParen or TokenKind.CloseBracket && depth > 0)
                {
                    depth--;
                }

                Advance();
            }
        }

        private void SkipLogicalAnd()
        {
            SkipExpression();
        }

        private void SkipEquality()
        {
            SkipExpression();
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private Token Expect(TokenKind kind)
        {
            return Current.Kind == kind ? Advance() : Current;
        }

        private Token Advance()
        {
            Token current = Current;
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }

            return current;
        }

        private Token Current => _tokens[_position];

        private Token Previous => _tokens[Math.Max(0, _position - 1)];

        private void CountOperation(Token token)
        {
            _operations++;
            if (_operations > _limits.MaximumExpressionOperations)
            {
                throw new EvaluationLimitException(token);
            }
        }

        private ValueResult Fail(string code, string message, Token token)
        {
            return ValueResult.Failure(new WorkflowValueError(code, message, string.Empty, token.Offset, token.Length));
        }
    }

    private enum ValueKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object,
    }

    private sealed class Value
    {
        private Value(ValueKind kind)
        {
            Kind = kind;
        }

        public static Value Null { get; } = new(ValueKind.Null);

        public ValueKind Kind { get; }

        public bool Boolean { get; private init; }

        public decimal Number { get; private init; }

        public string String { get; private init; } = string.Empty;

        public IReadOnlyList<Value> Array { get; private init; } = [];

        public IReadOnlyDictionary<string, Value> Object { get; private init; } = new Dictionary<string, Value>(StringComparer.Ordinal);

        public static Value BooleanValue(bool value)
        {
            return new Value(ValueKind.Boolean) { Boolean = value };
        }

        public static Value NumberValue(decimal value)
        {
            return new Value(ValueKind.Number) { Number = value };
        }

        public static Value StringValue(string value)
        {
            return new Value(ValueKind.String) { String = value };
        }

        public static Value ArrayValue(IReadOnlyList<Value> value)
        {
            return new Value(ValueKind.Array) { Array = value.Select(static item => item.Clone()).ToArray() };
        }

        public static Value ObjectValue(IReadOnlyDictionary<string, Value> value)
        {
            return new Value(ValueKind.Object) { Object = value.ToDictionary(static item => item.Key, static item => item.Value.Clone(), StringComparer.Ordinal) };
        }

        public static Value FromJson(JsonNode? value)
        {
            if (value is null)
            {
                return Null;
            }

            return value.GetValueKind() switch
            {
                JsonValueKind.True => BooleanValue(true),
                JsonValueKind.False => BooleanValue(false),
                JsonValueKind.Number => NumberValue(decimal.Parse(value.ToJsonString(), NumberStyles.Number, CultureInfo.InvariantCulture)),
                JsonValueKind.String => StringValue(value.GetValue<string>()),
                JsonValueKind.Array => ArrayValue(value.AsArray().Select(FromJson).ToArray()),
                JsonValueKind.Object => ObjectValue(value.AsObject().ToDictionary(static property => property.Key, static property => FromJson(property.Value), StringComparer.Ordinal)),
                _ => Null,
            };
        }

        public JsonNode? ToJson()
        {
            return Kind switch
            {
                ValueKind.Null => null,
                ValueKind.Boolean => JsonValue.Create(Boolean),
                ValueKind.Number => JsonValue.Create(Number),
                ValueKind.String => JsonValue.Create(String),
                ValueKind.Array => ToJsonArray(),
                ValueKind.Object => ToJsonObject(),
                _ => null,
            };
        }

        public Value Clone()
        {
            return Kind switch
            {
                ValueKind.Null => Null,
                ValueKind.Boolean => BooleanValue(Boolean),
                ValueKind.Number => NumberValue(Number),
                ValueKind.String => StringValue(String),
                ValueKind.Array => ArrayValue(Array),
                ValueKind.Object => ObjectValue(Object),
                _ => Null,
            };
        }

        private JsonArray ToJsonArray()
        {
            JsonArray array = [];
            foreach (Value item in Array)
            {
                array.Add(item.ToJson());
            }

            return array;
        }

        private JsonObject ToJsonObject()
        {
            JsonObject jsonObject = [];
            foreach (KeyValuePair<string, Value> property in Object)
            {
                jsonObject[property.Key] = property.Value.ToJson();
            }

            return jsonObject;
        }
    }

    private sealed class ValueResult
    {
        private ValueResult(Value? value, WorkflowValueError? error)
        {
            Value = value;
            Error = error;
        }

        public bool IsSuccess => Error is null;

        public Value? Value { get; }

        public WorkflowValueError? Error { get; }

        public static ValueResult Success(Value value)
        {
            return new ValueResult(value, null);
        }

        public static ValueResult Failure(WorkflowValueError error)
        {
            return new ValueResult(null, error);
        }
    }

    private static bool DeepEquals(Value left, Value right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            ValueKind.Null => true,
            ValueKind.Boolean => left.Boolean == right.Boolean,
            ValueKind.Number => left.Number == right.Number,
            ValueKind.String => string.Equals(left.String, right.String, StringComparison.Ordinal),
            ValueKind.Array => left.Array.Count == right.Array.Count && left.Array.Zip(right.Array).All(pair => DeepEquals(pair.First, pair.Second)),
            ValueKind.Object => left.Object.Count == right.Object.Count && left.Object.All(property => right.Object.TryGetValue(property.Key, out Value? value) && DeepEquals(property.Value, value)),
            _ => false,
        };
    }

    private static class ResultLimit
    {
        public static WorkflowValueResult Enforce(JsonNode? value, WorkflowValueProcessingLimits limits, string path)
        {
            return Check(value, limits, path, 0) is { } error
                ? WorkflowValueResult.Failure(error)
                : WorkflowValueResult.Success(value);
        }

        private static WorkflowValueError? Check(JsonNode? value, WorkflowValueProcessingLimits limits, string path, int depth)
        {
            if (depth > limits.MaximumResultDepth)
            {
                return new WorkflowValueError(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result JSON depth limit was exceeded.", path);
            }

            if (value is null)
            {
                return null;
            }

            if (value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Length > limits.MaximumStringLength)
            {
                return new WorkflowValueError(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result string length limit was exceeded.", path);
            }

            if (value is JsonArray array)
            {
                if (array.Count > limits.MaximumCollectionItems)
                {
                    return new WorkflowValueError(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result array item limit was exceeded.", path);
                }

                for (int index = 0; index < array.Count; index++)
                {
                    WorkflowValueError? error = Check(array[index], limits, path + "/" + index.ToString(CultureInfo.InvariantCulture), depth + 1);
                    if (error is not null)
                    {
                        return error;
                    }
                }
            }

            if (value is JsonObject jsonObject)
            {
                if (jsonObject.Count > limits.MaximumCollectionItems)
                {
                    return new WorkflowValueError(WorkflowValueErrorCode.ResultSizeLimitExceeded, "Result object property limit was exceeded.", path);
                }

                foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
                {
                    WorkflowValueError? error = Check(property.Value, limits, path + "/" + property.Key, depth + 1);
                    if (error is not null)
                    {
                        return error;
                    }
                }
            }

            return null;
        }
    }

    private sealed class EvaluationLimitException(Token token) : Exception
    {
        public int Offset { get; } = token.Offset;

        public int Length { get; } = token.Length;
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private readonly List<Token> _tokens = [];
        private int _position;

        public Lexer(string text)
        {
            _text = text;
        }

        public IReadOnlyList<Token> Lex()
        {
            while (!AtEnd)
            {
                char current = Current;
                if (char.IsWhiteSpace(current))
                {
                    _position++;
                    continue;
                }

                int start = _position;
                if (char.IsAsciiLetter(current) || current == '_')
                {
                    LexIdentifier(start);
                }
                else if (char.IsDigit(current))
                {
                    LexNumber(start);
                }
                else if (current == '\'')
                {
                    LexString(start);
                }
                else
                {
                    LexPunctuation(start);
                }
            }

            _tokens.Add(new Token(TokenKind.End, string.Empty, _text.Length, 0));
            return _tokens;
        }

        private void LexIdentifier(int start)
        {
            while (!AtEnd && (char.IsAsciiLetterOrDigit(Current) || Current == '_'))
            {
                _position++;
            }

            string text = _text[start.._position];
            TokenKind kind = text switch
            {
                "null" => TokenKind.Null,
                "true" => TokenKind.True,
                "false" => TokenKind.False,
                _ => TokenKind.Identifier,
            };
            _tokens.Add(new Token(kind, text, start, _position - start));
        }

        private void LexNumber(int start)
        {
            while (!AtEnd && char.IsDigit(Current))
            {
                _position++;
            }

            if (!AtEnd && Current == '.')
            {
                _position++;
                while (!AtEnd && char.IsDigit(Current))
                {
                    _position++;
                }
            }

            _tokens.Add(new Token(TokenKind.Number, _text[start.._position], start, _position - start));
        }

        private void LexString(int start)
        {
            _position++;
            StringBuilder builder = new();
            while (!AtEnd)
            {
                char current = Current;
                if (current == '\'')
                {
                    _position++;
                    break;
                }

                if (current == '\\')
                {
                    _position++;
                    char escaped = Current;
                    if (escaped == 'u')
                    {
                        string hex = _text.Substring(_position + 1, 4);
                        builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _position += 5;
                        continue;
                    }

                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => escaped,
                    });
                    _position++;
                    continue;
                }

                builder.Append(current);
                _position++;
            }

            _tokens.Add(new Token(TokenKind.String, builder.ToString(), start, _position - start));
        }

        private void LexPunctuation(int start)
        {
            char current = Current;
            char next = _position + 1 < _text.Length ? _text[_position + 1] : '\0';
            TokenKind? two = (current, next) switch
            {
                ('=', '=') => TokenKind.EqualsEquals,
                ('!', '=') => TokenKind.BangEquals,
                ('<', '=') => TokenKind.LessEquals,
                ('>', '=') => TokenKind.GreaterEquals,
                ('&', '&') => TokenKind.AmpAmp,
                ('|', '|') => TokenKind.BarBar,
                ('?', '?') => TokenKind.QuestionQuestion,
                _ => null,
            };

            if (two is not null)
            {
                _tokens.Add(new Token(two.Value, _text.Substring(start, 2), start, 2));
                _position += 2;
                return;
            }

            TokenKind one = current switch
            {
                '(' => TokenKind.OpenParen,
                ')' => TokenKind.CloseParen,
                '[' => TokenKind.OpenBracket,
                ']' => TokenKind.CloseBracket,
                '.' => TokenKind.Dot,
                ',' => TokenKind.Comma,
                '?' => TokenKind.Question,
                ':' => TokenKind.Colon,
                '!' => TokenKind.Bang,
                '-' => TokenKind.Minus,
                '+' => TokenKind.Plus,
                '*' => TokenKind.Star,
                '/' => TokenKind.Slash,
                '%' => TokenKind.Percent,
                '<' => TokenKind.Less,
                '>' => TokenKind.Greater,
                _ => TokenKind.End,
            };
            _tokens.Add(new Token(one, _text.Substring(start, 1), start, 1));
            _position++;
        }

        private bool AtEnd => _position >= _text.Length;

        private char Current => _text[_position];
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Offset, int Length);

    private enum TokenKind
    {
        End,
        Identifier,
        Number,
        String,
        Null,
        True,
        False,
        OpenParen,
        CloseParen,
        OpenBracket,
        CloseBracket,
        Dot,
        Comma,
        Question,
        QuestionQuestion,
        Colon,
        Bang,
        Minus,
        Plus,
        Star,
        Slash,
        Percent,
        Less,
        LessEquals,
        Greater,
        GreaterEquals,
        EqualsEquals,
        BangEquals,
        AmpAmp,
        BarBar,
    }
}
