namespace SkeletonKey.Expressions.Tests;

/// <summary>
/// Covers deterministic safe expression parsing and static reference discovery.
/// </summary>
public sealed class WorkflowExpressionParserTests
{
    private readonly WorkflowExpressionParser _parser = new();

    /// <summary>
    /// Parses supported literal forms.
    /// </summary>
    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("123")]
    [InlineData("123.45")]
    [InlineData("'hello'")]
    [InlineData("'a\\'b\\\\c\\n\\r\\t\\u0041'")]
    public void ParseAcceptsLiterals(string expression)
    {
        Assert.True(_parser.Parse(expression).IsValid);
    }

    /// <summary>
    /// Parses supported operators with deterministic precedence.
    /// </summary>
    [Theory]
    [InlineData("(inputs.count + 1) * 2")]
    [InlineData("!inputs.enabled")]
    [InlineData("-inputs.count + +variables.offset")]
    [InlineData("inputs.count * 2 >= variables.limit")]
    [InlineData("inputs.enabled && variables.ready || false")]
    [InlineData("inputs.value ?? variables.fallback")]
    [InlineData("inputs.enabled ? variables.yes : variables.no")]
    public void ParseAcceptsOperatorsAndGrouping(string expression)
    {
        Assert.True(_parser.Parse(expression).IsValid);
    }

    /// <summary>
    /// Parses member and index access forms.
    /// </summary>
    [Theory]
    [InlineData("inputs.account.name")]
    [InlineData("inputs.items[0]")]
    [InlineData("nodes['check-account'].outputs.result")]
    [InlineData("iterations['process-contacts'].item.name")]
    public void ParseAcceptsMemberAndIndexAccess(string expression)
    {
        Assert.True(_parser.Parse(expression).IsValid);
    }

    /// <summary>
    /// Parses allowed pure built-in function calls.
    /// </summary>
    [Theory]
    [InlineData("size(inputs.items)")]
    [InlineData("isEmpty(inputs.items)")]
    [InlineData("contains(inputs.names, 'Ada')")]
    [InlineData("startsWith(inputs.name, 'A')")]
    [InlineData("endsWith(inputs.name, 'a')")]
    [InlineData("trim(inputs.name)")]
    [InlineData("lower(inputs.name)")]
    [InlineData("upper(inputs.name)")]
    [InlineData("coalesce(inputs.name, variables.name, 'fallback')")]
    [InlineData("toString(inputs.value)")]
    [InlineData("toNumber(inputs.value)")]
    [InlineData("toBoolean(inputs.value)")]
    public void ParseAcceptsAllowedFunctions(string expression)
    {
        Assert.True(_parser.Parse(expression).IsValid);
    }

    /// <summary>
    /// Rejects unsafe or unsupported expression constructs.
    /// </summary>
    [Theory]
    [InlineData("eval(inputs.value)", "Expression.UnknownFunction")]
    [InlineData("inputs.name.trim()", "Expression.MethodCall")]
    [InlineData("inputs.value = 1", "Expression.TrailingTokens")]
    [InlineData("inputs.value // comment", "Expression.Comment")]
    [InlineData("'unterminated", "Expression.String")]
    [InlineData("inputs.value false", "Expression.TrailingTokens")]
    [InlineData("environment.value", "Expression.UnknownRoot")]
    public void ParseRejectsUnsupportedConstructs(string expression, string diagnosticCode)
    {
        WorkflowExpressionDocument document = _parser.Parse(expression);

        Assert.False(document.IsValid);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode);
    }

    /// <summary>
    /// Extracts workflow input, variable, node, and iteration references.
    /// </summary>
    [Fact]
    public void ParseExtractsWorkflowReferences()
    {
        WorkflowExpressionDocument document = _parser.Parse("inputs.account.id == variables.expected && nodes['check-account'].outputs.result && iterations['process-contacts'].item");

        Assert.True(document.IsValid);
        Assert.Contains(document.References, reference => reference.Kind == WorkflowExpressionReferenceKind.Input && reference.ReferencedName == "account");
        Assert.Contains(document.References, reference => reference.Kind == WorkflowExpressionReferenceKind.Variable && reference.ReferencedName == "expected");
        Assert.Contains(document.References, reference => reference.Kind == WorkflowExpressionReferenceKind.Node && reference.NodeId == "check-account" && reference.Port == "result");
        Assert.Contains(document.References, reference => reference.Kind == WorkflowExpressionReferenceKind.Iteration && reference.IterationId == "process-contacts");
    }

    /// <summary>
    /// Preserves deterministic source spans for references.
    /// </summary>
    [Fact]
    public void ParsePreservesSourceSpans()
    {
        WorkflowExpressionDocument document = _parser.Parse("  inputs.value");
        WorkflowExpressionReference reference = Assert.Single(document.References);

        Assert.Equal(2, reference.SourceSpan.Offset);
        Assert.Equal(6, reference.SourceSpan.Length);
    }

    /// <summary>
    /// Repeated parsing produces deterministic diagnostics and references.
    /// </summary>
    [Fact]
    public void ParseIsDeterministic()
    {
        WorkflowExpressionDocument first = _parser.Parse("missing.value && eval(inputs.value)");
        WorkflowExpressionDocument second = _parser.Parse("missing.value && eval(inputs.value)");

        Assert.Equal(first.Diagnostics.Select(static diagnostic => diagnostic.Code), second.Diagnostics.Select(static diagnostic => diagnostic.Code));
        Assert.Equal(first.References.Select(static reference => reference.Root), second.References.Select(static reference => reference.Root));
    }

    /// <summary>
    /// Parser use is safe from multiple threads.
    /// </summary>
    [Fact]
    public void ParseIsThreadSafe()
    {
        WorkflowExpressionDocument[] documents = ParallelEnumerable.Range(0, 64)
            .Select(_ => _parser.Parse("size(inputs.items) > 0 && variables.enabled"))
            .ToArray();

        Assert.All(documents, document => Assert.True(document.IsValid));
    }
}
