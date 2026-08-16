using System.Globalization;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Execution;

namespace SkeletonKey.Evaluation.Tests;

/// <summary>
/// Verifies deterministic workflow expression evaluation.
/// </summary>
public sealed class WorkflowExpressionEvaluatorTests
{
    private readonly WorkflowExpressionEvaluator _evaluator = new();

    /// <summary>
    /// Evaluates literals, grouping, unary operators, arithmetic precedence, and string concatenation.
    /// </summary>
    [Theory]
    [InlineData("null", null)]
    [InlineData("true", true)]
    [InlineData("123", 123)]
    [InlineData("123.45", 123.45)]
    [InlineData("'a\\'b'", "a'b")]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("!false", true)]
    [InlineData("-1 + +2", 1)]
    [InlineData("'a' + 'b'", "ab")]
    public void EvaluatesLiteralsAndOperators(string expression, object? expected)
    {
        AssertJson(expected, Eval(expression));
    }

    /// <summary>
    /// Evaluates strict equality, comparisons, logical operators, null coalescing, and conditional short-circuiting.
    /// </summary>
    [Fact]
    public void EvaluatesStrictAndShortCircuitOperators()
    {
        Assert.True(EvalValue("1 == 1.0").GetValue<bool>());
        Assert.True(EvalValue("inputs.items == inputs.items").GetValue<bool>());
        Assert.True(EvalValue("inputs.account == inputs.account").GetValue<bool>());
        Assert.True(EvalValue("'b' > 'a'").GetValue<bool>());
        Assert.False(EvalValue("false && (1 / 0 == 0)").GetValue<bool>());
        Assert.True(EvalValue("true || (1 / 0 == 0)").GetValue<bool>());
        Assert.Equal("fallback", EvalValue("null ?? 'fallback'").GetValue<string>());
        Assert.Equal("yes", EvalValue("true ? 'yes' : (1 / 0)").GetValue<string>());
    }

    /// <summary>
    /// Evaluates root access, member access, string indexes, integer indexes, node output projection, and iteration projection.
    /// </summary>
    [Fact]
    public void EvaluatesRootsAndAccess()
    {
        WorkflowValueResolutionContext context = Context();

        Assert.Equal("Ada", EvalValue("inputs.account.name", context).GetValue<string>());
        Assert.Equal("B", EvalValue("inputs.items[1]", context).GetValue<string>());
        Assert.Equal("ok", EvalValue("nodes['check'].outputs['status']", context).GetValue<string>());
        Assert.Equal(2, EvalValue("nodes['check'].outputs.many", context).AsArray().Count);
        Assert.Equal(1, EvalValue("iterations['loop'].number", context).GetValue<decimal>());
    }

    /// <summary>
    /// Evaluates all documented built-in functions.
    /// </summary>
    [Fact]
    public void EvaluatesBuiltInFunctions()
    {
        WorkflowValueResolutionContext context = Context();

        Assert.Equal(3, EvalValue("size('A\\uD801\\uDC37B')", context).GetValue<decimal>());
        Assert.True(EvalValue("isEmpty(null)", context).GetValue<bool>());
        Assert.True(EvalValue("contains('Ada', 'd')", context).GetValue<bool>());
        Assert.True(EvalValue("contains(inputs.items, 'A')", context).GetValue<bool>());
        Assert.True(EvalValue("contains(inputs.account, 'name')", context).GetValue<bool>());
        Assert.True(EvalValue("startsWith('Ada', 'A')", context).GetValue<bool>());
        Assert.True(EvalValue("endsWith('Ada', 'a')", context).GetValue<bool>());
        Assert.Equal("x", EvalValue("trim(' x ')", context).GetValue<string>());
        Assert.Equal("ada", EvalValue("lower('ADA')", context).GetValue<string>());
        Assert.Equal("ADA", EvalValue("upper('ada')", context).GetValue<string>());
        Assert.Equal("x", EvalValue("coalesce(null, 'x', (1 / 0))", context).GetValue<string>());
        Assert.Equal("null", EvalValue("toString(null)", context).GetValue<string>());
        Assert.Equal(12.5m, EvalValue("toNumber('12.5')", context).GetValue<decimal>());
        Assert.True(EvalValue("toBoolean('TRUE')", context).GetValue<bool>());
        Assert.False(EvalValue("toBoolean(0)", context).GetValue<bool>());
    }

    /// <summary>
    /// Rejects invalid operand types, invalid functions, invalid access, division by zero, overflow, and operation limits.
    /// </summary>
    [Theory]
    [InlineData("'a' + 1", WorkflowValueErrorCode.InvalidExpressionOperandType)]
    [InlineData("size(1)", WorkflowValueErrorCode.InvalidFunctionArgument)]
    [InlineData("inputs.missing", WorkflowValueErrorCode.InvalidMemberAccess)]
    [InlineData("inputs.items[9]", WorkflowValueErrorCode.InvalidIndexAccess)]
    [InlineData("1 / 0", WorkflowValueErrorCode.DivisionByZero)]
    [InlineData("79228162514264337593543950335 + 1", WorkflowValueErrorCode.NumericOverflow)]
    [InlineData("eval(1)", WorkflowValueErrorCode.InvalidExpression)]
    public void RejectsInvalidExpressions(string expression, string code)
    {
        WorkflowValueResult result = _evaluator.Evaluate(expression, Context());

        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
    }

    /// <summary>
    /// Evaluation is culture-invariant, deterministic, thread-safe, and limit-bound.
    /// </summary>
    [Fact]
    public void EvaluationIsInvariantDeterministicThreadSafeAndLimited()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(3.5m, EvalValue("toNumber('3.5') + 0").GetValue<decimal>());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        Assert.All(ParallelEnumerable.Range(0, 64).Select(_ => _evaluator.Evaluate("inputs.account.name", Context())).ToArray(), result => Assert.Equal("Ada", result.Value!.GetValue<string>()));

        WorkflowValueResult limited = _evaluator.Evaluate("1 + 2 + 3", Context(), new WorkflowValueProcessingLimits(maximumExpressionOperations: 1));
        Assert.Equal(WorkflowValueErrorCode.EvaluationOperationLimitExceeded, limited.Error!.Code);
    }

    private JsonNode? Eval(string expression, WorkflowValueResolutionContext? context = null)
    {
        WorkflowValueResult result = _evaluator.Evaluate(expression, context ?? Context());
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    private JsonNode EvalValue(string expression, WorkflowValueResolutionContext? context = null)
    {
        JsonNode? value = Eval(expression, context);
        Assert.NotNull(value);
        return value;
    }

    private static void AssertJson(object? expected, JsonNode? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
        }
        else if (expected is bool boolean)
        {
            Assert.Equal(boolean, actual!.GetValue<bool>());
        }
        else if (expected is string text)
        {
            Assert.Equal(text, actual!.GetValue<string>());
        }
        else
        {
            Assert.Equal(Convert.ToDecimal(expected, CultureInfo.InvariantCulture), actual!.GetValue<decimal>());
        }
    }

    private static WorkflowValueResolutionContext Context()
    {
        return new WorkflowValueResolutionContext(
            new Dictionary<string, JsonNode?>
            {
                ["account"] = new JsonObject { ["name"] = "Ada" },
                ["items"] = new JsonArray("A", "B"),
            },
            new Dictionary<string, JsonNode?> { ["message"] = " hello " },
            new Dictionary<string, NodePortValueMap>
            {
                ["check"] = new(new Dictionary<string, NodePortValueSet>
                {
                    ["status"] = new([JsonValue.Create("ok")]),
                    ["many"] = new([JsonValue.Create(1), JsonValue.Create(2)]),
                }),
            },
            new Dictionary<string, WorkflowIterationContext>
            {
                ["loop"] = new("loop", 0, 1, new JsonObject { ["name"] = "Ada" }, hasItem: true, count: 2),
            });
    }
}
