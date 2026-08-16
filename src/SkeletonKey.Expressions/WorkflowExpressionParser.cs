using System.Collections.ObjectModel;
using System.Globalization;

namespace SkeletonKey.Expressions;

/// <summary>
/// Parses safe deterministic workflow expressions and discovers static workflow data references.
/// </summary>
/// <remarks>
/// The parser is stateless, thread-safe, culture-invariant, and has no expression evaluation behavior,
/// reflection behavior, host access, current-time behavior, or mutable global cache.
/// </remarks>
public sealed class WorkflowExpressionParser
{
    private static readonly IReadOnlyDictionary<string, Arity> _functions = new ReadOnlyDictionary<string, Arity>(
        new Dictionary<string, Arity>(StringComparer.Ordinal)
        {
            ["size"] = new(1, 1),
            ["isEmpty"] = new(1, 1),
            ["contains"] = new(2, 2),
            ["startsWith"] = new(2, 2),
            ["endsWith"] = new(2, 2),
            ["trim"] = new(1, 1),
            ["lower"] = new(1, 1),
            ["upper"] = new(1, 1),
            ["coalesce"] = new(2, null),
            ["toString"] = new(1, 1),
            ["toNumber"] = new(1, 1),
            ["toBoolean"] = new(1, 1),
        });

    /// <summary>
    /// Parses expression text into an immutable syntax document.
    /// </summary>
    /// <param name="expression">The exact expression text to parse.</param>
    /// <returns>The immutable parsed document with diagnostics and references.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression" /> is <see langword="null" />.</exception>
    public WorkflowExpressionDocument Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Parser parser = new(expression, _functions);
        return parser.Parse();
    }

    /// <summary>
    /// Tries to parse expression text into an immutable syntax document without throwing for malformed user input.
    /// </summary>
    /// <param name="expression">The exact expression text to parse.</param>
    /// <param name="document">The parsed expression document when parsing completes.</param>
    /// <param name="diagnostics">Deterministic parse diagnostics.</param>
    /// <returns><see langword="true" /> when the expression has no diagnostics.</returns>
    public bool TryParse(
        string expression,
        out WorkflowExpressionDocument? document,
        out IReadOnlyList<WorkflowExpressionDiagnostic> diagnostics)
    {
        document = Parse(expression);
        diagnostics = document.Diagnostics;
        return document.IsValid;
    }

    private readonly record struct Arity(int Minimum, int? Maximum)
    {
        public bool Contains(int count)
        {
            return count >= Minimum && (Maximum is null || count <= Maximum.Value);
        }
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly IReadOnlyDictionary<string, Arity> _functions;
        private readonly IReadOnlyList<Token> _tokens;
        private readonly List<WorkflowExpressionDiagnostic> _diagnostics = [];
        private readonly List<WorkflowExpressionReference> _references = [];
        private int _position;

        public Parser(string text, IReadOnlyDictionary<string, Arity> functions)
        {
            _text = text;
            _functions = functions;
            Lexer lexer = new(text);
            _tokens = lexer.Lex();
            _diagnostics.AddRange(lexer.Diagnostics);
        }

        public WorkflowExpressionDocument Parse()
        {
            if (_text.Length == 0)
            {
                Add("Expression.Empty", "Expression text must not be empty.", 0, 0);
                return CreateDocument();
            }

            ParseExpression();
            if (Current.Kind != TokenKind.End)
            {
                Add("Expression.TrailingTokens", "Expression contains trailing tokens.", Current.Offset, Current.Length);
            }

            return CreateDocument();
        }

        private WorkflowExpressionDocument CreateDocument()
        {
            return new WorkflowExpressionDocument(_text, _diagnostics, _references);
        }

        private ValueShape ParseExpression()
        {
            return ParseConditional();
        }

        private ValueShape ParseConditional()
        {
            ValueShape condition = ParseNullCoalescing();
            if (Match(TokenKind.Question))
            {
                ParseExpression();
                Expect(TokenKind.Colon, "Expression.Conditional", "Conditional expression requires `:`.");
                ParseExpression();
                return ValueShape.Value;
            }

            return condition;
        }

        private ValueShape ParseNullCoalescing()
        {
            ValueShape left = ParseLogicalOr();
            if (Match(TokenKind.QuestionQuestion))
            {
                ParseNullCoalescing();
                return ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseLogicalOr()
        {
            ValueShape left = ParseLogicalAnd();
            while (Match(TokenKind.BarBar))
            {
                ParseLogicalAnd();
                left = ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseLogicalAnd()
        {
            ValueShape left = ParseEquality();
            while (Match(TokenKind.AmpAmp))
            {
                ParseEquality();
                left = ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseEquality()
        {
            ValueShape left = ParseComparison();
            while (Match(TokenKind.EqualsEquals) || Match(TokenKind.BangEquals))
            {
                ParseComparison();
                left = ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseComparison()
        {
            ValueShape left = ParseAdditive();
            while (Match(TokenKind.Less) || Match(TokenKind.LessEquals) || Match(TokenKind.Greater) || Match(TokenKind.GreaterEquals))
            {
                ParseAdditive();
                left = ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseAdditive()
        {
            ValueShape left = ParseMultiplicative();
            while (Match(TokenKind.Plus) || Match(TokenKind.Minus))
            {
                ParseMultiplicative();
                left = ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseMultiplicative()
        {
            ValueShape left = ParseUnary();
            while (Match(TokenKind.Star) || Match(TokenKind.Slash) || Match(TokenKind.Percent))
            {
                ParseUnary();
                left = ValueShape.Value;
            }

            return left;
        }

        private ValueShape ParseUnary()
        {
            if (Match(TokenKind.Bang) || Match(TokenKind.Minus) || Match(TokenKind.Plus))
            {
                ParseUnary();
                return ValueShape.Value;
            }

            return ParsePostfix();
        }

        private ValueShape ParsePostfix()
        {
            ValueShape value = ParsePrimary();

            while (true)
            {
                if (Match(TokenKind.Dot))
                {
                    Token member = Expect(TokenKind.Identifier, "Expression.MemberAccess", "Member access requires an identifier.");
                    if (value.Root is not null && member.Kind == TokenKind.Identifier)
                    {
                        value.Segments.Add(PathSegment.Member(member.Text, member.Offset, member.Length));
                    }

                    continue;
                }

                if (Match(TokenKind.OpenBracket))
                {
                    if (Current.Kind == TokenKind.String)
                    {
                        Token index = Advance();
                        if (value.Root is not null)
                        {
                            value.Segments.Add(PathSegment.StringIndex(index.Text, index.Offset, index.Length));
                        }
                    }
                    else if (Current.Kind == TokenKind.Number && Current.Text.All(static c => char.IsDigit(c)))
                    {
                        Token index = Advance();
                        if (value.Root is not null)
                        {
                            value.Segments.Add(PathSegment.IntegerIndex(index.Text, index.Offset, index.Length));
                        }
                    }
                    else
                    {
                        Add("Expression.Index", "Only string and integer index access is supported.", Current.Offset, Current.Length);
                        ParseExpression();
                    }

                    Expect(TokenKind.CloseBracket, "Expression.Index", "Index access requires `]`.");
                    continue;
                }

                if (Match(TokenKind.OpenParen))
                {
                    Add("Expression.MethodCall", "Calls on values or members are not supported.", Previous.Offset, Previous.Length);
                    ParseArgumentListAfterOpenParen();
                    value = ValueShape.Value;
                    continue;
                }

                break;
            }

            AddReference(value);
            return ValueShape.Value;
        }

        private ValueShape ParsePrimary()
        {
            if (Current.Kind is TokenKind.Null or TokenKind.True or TokenKind.False or TokenKind.Number or TokenKind.String)
            {
                Advance();
                return ValueShape.Value;
            }

            if (Match(TokenKind.OpenParen))
            {
                ParseExpression();
                Expect(TokenKind.CloseParen, "Expression.Grouping", "Grouping requires `)`.");
                return ValueShape.Value;
            }

            if (Current.Kind == TokenKind.Identifier)
            {
                Token identifier = Advance();
                if (Match(TokenKind.OpenParen))
                {
                    ParseFunctionCall(identifier);
                    return ValueShape.Value;
                }

                if (identifier.Text is "inputs" or "variables" or "nodes" or "iterations")
                {
                    return ValueShape.Rooted(identifier.Text, identifier.Offset, identifier.Length);
                }

                Add("Expression.UnknownRoot", $"Unknown expression root '{identifier.Text}'.", identifier.Offset, identifier.Length);
                return ValueShape.Value;
            }

            if (Current.Kind is TokenKind.Equals or TokenKind.PlusPlus or TokenKind.MinusMinus)
            {
                Add("Expression.UnsupportedOperator", "Assignment, increment, and decrement operators are not supported.", Current.Offset, Current.Length);
                Advance();
                return ValueShape.Value;
            }

            Add("Expression.Syntax", "Expression contains an unexpected token.", Current.Offset, Current.Length);
            if (Current.Kind != TokenKind.End)
            {
                Advance();
            }

            return ValueShape.Value;
        }

        private void ParseFunctionCall(Token identifier)
        {
            int argumentCount = ParseArgumentListAfterOpenParen();
            if (!_functions.TryGetValue(identifier.Text, out Arity arity))
            {
                Add("Expression.UnknownFunction", $"Unknown expression function '{identifier.Text}'.", identifier.Offset, identifier.Length);
                return;
            }

            if (!arity.Contains(argumentCount))
            {
                Add("Expression.FunctionArity", $"Expression function '{identifier.Text}' received an invalid argument count.", identifier.Offset, identifier.Length);
            }
        }

        private int ParseArgumentListAfterOpenParen()
        {
            int count = 0;
            if (Match(TokenKind.CloseParen))
            {
                return count;
            }

            while (Current.Kind != TokenKind.End)
            {
                ParseExpression();
                count++;

                if (Match(TokenKind.Comma))
                {
                    continue;
                }

                Expect(TokenKind.CloseParen, "Expression.FunctionCall", "Function call requires `)`.");
                break;
            }

            return count;
        }

        private void AddReference(ValueShape value)
        {
            if (value.Root is null)
            {
                return;
            }

            WorkflowExpressionSourceSpan span = new(value.RootOffset, value.RootLength);
            if (value.Root == "inputs")
            {
                _references.Add(new WorkflowExpressionReference(
                    WorkflowExpressionReferenceKind.Input,
                    value.Root,
                    FirstStringSegment(value),
                    null,
                    null,
                    null,
                    span));
            }
            else if (value.Root == "variables")
            {
                _references.Add(new WorkflowExpressionReference(
                    WorkflowExpressionReferenceKind.Variable,
                    value.Root,
                    FirstStringSegment(value),
                    null,
                    null,
                    null,
                    span));
            }
            else if (value.Root == "nodes")
            {
                string? nodeId = FirstStringSegment(value);
                string? port = null;
                if (value.Segments.Count >= 3 &&
                    string.Equals(value.Segments[1].Text, "outputs", StringComparison.Ordinal))
                {
                    port = value.Segments[2].Text;
                }

                _references.Add(new WorkflowExpressionReference(
                    WorkflowExpressionReferenceKind.Node,
                    value.Root,
                    null,
                    nodeId,
                    port,
                    null,
                    span));
            }
            else if (value.Root == "iterations")
            {
                _references.Add(new WorkflowExpressionReference(
                    WorkflowExpressionReferenceKind.Iteration,
                    value.Root,
                    null,
                    null,
                    null,
                    FirstStringSegment(value),
                    span));
            }
        }

        private static string? FirstStringSegment(ValueShape value)
        {
            return value.Segments.Count == 0 ? null : value.Segments[0].Text;
        }

        private Token Expect(TokenKind kind, string code, string message)
        {
            if (Current.Kind == kind)
            {
                return Advance();
            }

            Add(code, message, Current.Offset, Current.Length);
            return new Token(kind, string.Empty, Current.Offset, 0);
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

        private void Add(string code, string message, int offset, int length)
        {
            _diagnostics.Add(new WorkflowExpressionDiagnostic(code, message, offset, length));
        }
    }

    private sealed class ValueShape
    {
        private ValueShape(string? root, int rootOffset, int rootLength)
        {
            Root = root;
            RootOffset = rootOffset;
            RootLength = rootLength;
        }

        public static ValueShape Value { get; } = new(null, 0, 0);

        public string? Root { get; }

        public int RootOffset { get; }

        public int RootLength { get; }

        public List<PathSegment> Segments { get; } = [];

        public static ValueShape Rooted(string root, int offset, int length)
        {
            return new ValueShape(root, offset, length);
        }
    }

    private readonly record struct PathSegment(string Text, int Offset, int Length)
    {
        public static PathSegment Member(string text, int offset, int length)
        {
            return new PathSegment(text, offset, length);
        }

        public static PathSegment StringIndex(string text, int offset, int length)
        {
            return new PathSegment(text, offset, length);
        }

        public static PathSegment IntegerIndex(string text, int offset, int length)
        {
            return new PathSegment(text, offset, length);
        }
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private readonly List<Token> _tokens = [];
        private readonly List<WorkflowExpressionDiagnostic> _diagnostics = [];
        private int _position;

        public Lexer(string text)
        {
            _text = text;
        }

        public IReadOnlyList<WorkflowExpressionDiagnostic> Diagnostics => _diagnostics;

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
                    continue;
                }

                if (char.IsDigit(current))
                {
                    LexNumber(start);
                    continue;
                }

                if (current == '\'')
                {
                    LexString(start);
                    continue;
                }

                LexPunctuation(start);
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
                if (AtEnd || !char.IsDigit(Current))
                {
                    _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.Number", "Decimal numbers require at least one digit after `.`.", start, _position - start));
                }

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
            string value = string.Empty;
            bool terminated = false;

            while (!AtEnd)
            {
                char current = Current;
                if (current == '\'')
                {
                    _position++;
                    terminated = true;
                    break;
                }

                if (current == '\\')
                {
                    _position++;
                    if (AtEnd)
                    {
                        break;
                    }

                    char escaped = Current;
                    if (escaped is '\\' or '\'' or 'n' or 'r' or 't')
                    {
                        value += escaped switch
                        {
                            'n' => "\n",
                            'r' => "\r",
                            't' => "\t",
                            _ => escaped.ToString(CultureInfo.InvariantCulture),
                        };
                        _position++;
                        continue;
                    }

                    if (escaped == 'u')
                    {
                        if (_position + 4 >= _text.Length || !IsHex(_text.AsSpan(_position + 1, 4)))
                        {
                            _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.String", "Unicode escapes must use `\\uXXXX`.", _position - 1, Math.Min(6, _text.Length - (_position - 1))));
                            _position++;
                            continue;
                        }

                        string hex = _text.Substring(_position + 1, 4);
                        value += ((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                        _position += 5;
                        continue;
                    }

                    _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.String", "String escape is not supported.", _position - 1, Math.Min(2, _text.Length - (_position - 1))));
                    _position++;
                    continue;
                }

                if (current is '\r' or '\n')
                {
                    _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.String", "String literals cannot contain raw line breaks.", _position, 1));
                }

                value += current.ToString(CultureInfo.InvariantCulture);
                _position++;
            }

            if (!terminated)
            {
                _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.String", "String literal is not terminated.", start, _position - start));
            }

            _tokens.Add(new Token(TokenKind.String, value, start, _position - start));
        }

        private void LexPunctuation(int start)
        {
            char current = Current;
            char next = _position + 1 < _text.Length ? _text[_position + 1] : '\0';

            if (current == '/' && next is '/' or '*')
            {
                _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.Comment", "Comments are not supported.", start, 2));
                _position += 2;
                return;
            }

            TokenKind? two = (current, next) switch
            {
                ('=', '=') => TokenKind.EqualsEquals,
                ('!', '=') => TokenKind.BangEquals,
                ('<', '=') => TokenKind.LessEquals,
                ('>', '=') => TokenKind.GreaterEquals,
                ('&', '&') => TokenKind.AmpAmp,
                ('|', '|') => TokenKind.BarBar,
                ('?', '?') => TokenKind.QuestionQuestion,
                ('+', '+') => TokenKind.PlusPlus,
                ('-', '-') => TokenKind.MinusMinus,
                _ => null,
            };

            if (two is not null)
            {
                _tokens.Add(new Token(two.Value, _text.Substring(start, 2), start, 2));
                _position += 2;
                return;
            }

            TokenKind? one = current switch
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
                '=' => TokenKind.Equals,
                _ => null,
            };

            if (one is not null)
            {
                _tokens.Add(new Token(one.Value, _text.Substring(start, 1), start, 1));
                _position++;
                return;
            }

            _diagnostics.Add(new WorkflowExpressionDiagnostic("Expression.Character", "Expression contains an unsupported character.", start, 1));
            _position++;
        }

        private bool AtEnd => _position >= _text.Length;

        private char Current => _text[_position];

        private static bool IsHex(ReadOnlySpan<char> span)
        {
            foreach (char value in span)
            {
                if (!Uri.IsHexDigit(value))
                {
                    return false;
                }
            }

            return true;
        }
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
        Equals,
        EqualsEquals,
        BangEquals,
        AmpAmp,
        BarBar,
        PlusPlus,
        MinusMinus,
    }
}
