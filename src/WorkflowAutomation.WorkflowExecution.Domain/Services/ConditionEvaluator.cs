namespace WorkflowAutomation.WorkflowExecution.Domain.Services;

/// <summary>
/// Evaluates boolean expressions after template references have been
/// resolved to concrete values by <see cref="ITemplateResolver"/>.
///
/// Supported syntax:
///   Comparisons:  ==  !=  >  >=  &lt;  &lt;=
///   Logical:      AND  OR
///   Grouping:     ( )
///   Values:       'string literals', numbers (int/decimal), true, false
///
/// Examples (post-resolution):
///   "urgent" == "urgent"
///   1500 > 1000 AND "finance" == "finance"
///   ("high" == "high" OR 2000 > 1500) AND "active" == "active"
/// </summary>
public sealed class ConditionEvaluator : IConditionEvaluator
{
    public bool Evaluate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var tokens = Tokenize(expression);
        var pos = 0;
        var result = ParseOr(tokens, ref pos);

        if (pos != tokens.Count)
            throw new InvalidOperationException(
                $"Unexpected token '{tokens[pos]}' at position {pos} in expression: {expression}");

        return result;
    }

    // ── Recursive descent parser ─────────────────────────────────────────

    // OR has lowest precedence.
    private static bool ParseOr(List<string> tokens, ref int pos)
    {
        var left = ParseAnd(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos] == "OR")
        {
            pos++; // consume OR
            var right = ParseAnd(tokens, ref pos);
            left = left || right;
        }
        return left;
    }

    private static bool ParseAnd(List<string> tokens, ref int pos)
    {
        var left = ParseComparison(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos] == "AND")
        {
            pos++; // consume AND
            var right = ParseComparison(tokens, ref pos);
            left = left && right;
        }
        return left;
    }

    private static bool ParseComparison(List<string> tokens, ref int pos)
    {
        // Handle parenthesised sub-expressions.
        if (pos < tokens.Count && tokens[pos] == "(")
        {
            pos++; // consume (
            var result = ParseOr(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos] != ")")
                throw new InvalidOperationException("Missing closing parenthesis.");
            pos++; // consume )
            return result;
        }

        // Handle boolean literals at comparison level.
        if (pos < tokens.Count && IsBooleanLiteral(tokens[pos]))
        {
            var boolVal = tokens[pos].Equals("true", StringComparison.OrdinalIgnoreCase);
            pos++;
            return boolVal;
        }

        // Otherwise, expect: value operator value
        var leftVal = ParseValue(tokens, ref pos);
        if (pos >= tokens.Count || !IsComparisonOperator(tokens[pos]))
            throw new InvalidOperationException(
                $"Expected comparison operator at position {pos}.");

        var op = tokens[pos];
        pos++; // consume operator

        var rightVal = ParseValue(tokens, ref pos);
        return Compare(leftVal, op, rightVal);
    }

    private static string ParseValue(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new InvalidOperationException("Unexpected end of expression — expected a value.");

        var token = tokens[pos];

        if (token == "(" || token == ")" || IsComparisonOperator(token)
            || token == "AND" || token == "OR")
            throw new InvalidOperationException(
                $"Expected a value but found '{token}' at position {pos}.");

        pos++;
        return token;
    }

    // ── Comparison logic ─────────────────────────────────────────────────

    private static bool Compare(string left, string op, string right)
    {
        // Try numeric comparison first.
        var leftIsNum = decimal.TryParse(left, out var leftNum);
        var rightIsNum = decimal.TryParse(right, out var rightNum);

        if (leftIsNum && rightIsNum)
        {
            return op switch
            {
                "==" => leftNum == rightNum,
                "!=" => leftNum != rightNum,
                ">"  => leftNum > rightNum,
                ">=" => leftNum >= rightNum,
                "<"  => leftNum < rightNum,
                "<=" => leftNum <= rightNum,
                _ => throw new InvalidOperationException($"Unknown operator '{op}'.")
            };
        }

        // Fall back to string comparison (case-insensitive).
        var cmp = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "==" => cmp == 0,
            "!=" => cmp != 0,
            ">"  => cmp > 0,
            ">=" => cmp >= 0,
            "<"  => cmp < 0,
            "<=" => cmp <= 0,
            _ => throw new InvalidOperationException($"Unknown operator '{op}'.")
        };
    }

    // ── Tokenizer ────────────────────────────────────────────────────────

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var i = 0;
        var span = expression.AsSpan();

        while (i < span.Length)
        {
            // Skip whitespace.
            if (char.IsWhiteSpace(span[i])) { i++; continue; }

            // Parentheses.
            if (span[i] is '(' or ')')
            {
                tokens.Add(span[i].ToString());
                i++;
                continue;
            }

            // Quoted string — supports both single and double quotes.
            if (span[i] is '\'' or '"')
            {
                var quote = span[i];
                i++; // consume opening quote
                var start = i;
                while (i < span.Length && span[i] != quote) i++;
                tokens.Add(span[start..i].ToString());
                if (i < span.Length) i++; // consume closing quote
                continue;
            }

            // Two-character operators: ==  !=  >=  <=
            if (i + 1 < span.Length)
            {
                var twoChar = span[i..(i + 2)];
                if (twoChar is "==" or "!=" or ">=" or "<=")
                {
                    tokens.Add(twoChar.ToString());
                    i += 2;
                    continue;
                }
            }

            // Single-character operators: >  <
            if (span[i] is '>' or '<')
            {
                tokens.Add(span[i].ToString());
                i++;
                continue;
            }

            // Unquoted word or number.
            {
                var start = i;
                while (i < span.Length
                       && !char.IsWhiteSpace(span[i])
                       && span[i] is not ('(' or ')' or '\'' or '"' or '>' or '<' or '=' or '!'))
                    i++;
                var word = span[start..i].ToString();

                // Recognise AND / OR keywords (case-insensitive input,
                // normalised to upper).
                if (word.Equals("AND", StringComparison.OrdinalIgnoreCase))
                    tokens.Add("AND");
                else if (word.Equals("OR", StringComparison.OrdinalIgnoreCase))
                    tokens.Add("OR");
                else
                    tokens.Add(word);
            }
        }

        return tokens;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static bool IsComparisonOperator(string token) =>
        token is "==" or "!=" or ">" or ">=" or "<" or "<=";

    private static bool IsBooleanLiteral(string token) =>
        token.Equals("true", StringComparison.OrdinalIgnoreCase)
        || token.Equals("false", StringComparison.OrdinalIgnoreCase);
}