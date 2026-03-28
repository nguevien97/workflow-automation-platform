namespace WorkflowAutomation.WorkflowLanguage.Domain.Conditions;

/// <summary>
/// Shared workflow-language helper for validating and evaluating
/// condition expressions after templates have been resolved.
/// </summary>
public static class ConditionEvaluator
{
    public static bool Evaluate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return ParseExpression(expression);
    }

    public static void ValidateSyntax(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        _ = ParseExpression(expression);
    }

    private static bool ParseExpression(string expression)
    {
        var tokens = Tokenize(expression);
        var pos = 0;
        var result = ParseOr(tokens, ref pos);

        if (pos != tokens.Count)
        {
            throw new InvalidOperationException(
                $"Unexpected token '{tokens[pos]}' at position {pos} in expression: {expression}");
        }

        return result;
    }

    private static bool ParseOr(List<string> tokens, ref int pos)
    {
        var left = ParseAnd(tokens, ref pos);
        while (pos < tokens.Count && tokens[pos] == "OR")
        {
            pos++;
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
            pos++;
            var right = ParseComparison(tokens, ref pos);
            left = left && right;
        }

        return left;
    }

    private static bool ParseComparison(List<string> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos] == "(")
        {
            pos++;
            var result = ParseOr(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos] != ")")
                throw new InvalidOperationException("Missing closing parenthesis.");
            pos++;
            return result;
        }

        if (pos < tokens.Count && IsBooleanLiteral(tokens[pos]))
        {
            var boolVal = tokens[pos].Equals("true", StringComparison.OrdinalIgnoreCase);
            pos++;
            return boolVal;
        }

        var leftVal = ParseValue(tokens, ref pos);
        if (pos >= tokens.Count || !IsComparisonOperator(tokens[pos]))
        {
            throw new InvalidOperationException(
                $"Expected comparison operator at position {pos}.");
        }

        var op = tokens[pos];
        pos++;

        var rightVal = ParseValue(tokens, ref pos);
        return Compare(leftVal, op, rightVal);
    }

    private static string ParseValue(List<string> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new InvalidOperationException("Unexpected end of expression — expected a value.");

        var token = tokens[pos];
        if (token == "(" || token == ")" || IsComparisonOperator(token) || token == "AND" || token == "OR")
        {
            throw new InvalidOperationException(
                $"Expected a value but found '{token}' at position {pos}.");
        }

        pos++;
        return token;
    }

    private static bool Compare(string left, string op, string right)
    {
        var leftIsNum = decimal.TryParse(left, out var leftNum);
        var rightIsNum = decimal.TryParse(right, out var rightNum);

        if (leftIsNum && rightIsNum)
        {
            return op switch
            {
                "==" => leftNum == rightNum,
                "!=" => leftNum != rightNum,
                ">" => leftNum > rightNum,
                ">=" => leftNum >= rightNum,
                "<" => leftNum < rightNum,
                "<=" => leftNum <= rightNum,
                _ => throw new InvalidOperationException($"Unknown operator '{op}'.")
            };
        }

        var cmp = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "==" => cmp == 0,
            "!=" => cmp != 0,
            ">" => cmp > 0,
            ">=" => cmp >= 0,
            "<" => cmp < 0,
            "<=" => cmp <= 0,
            _ => throw new InvalidOperationException($"Unknown operator '{op}'.")
        };
    }

    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var i = 0;
        var span = expression.AsSpan();

        while (i < span.Length)
        {
            if (char.IsWhiteSpace(span[i]))
            {
                i++;
                continue;
            }

            if (span[i] is '(' or ')')
            {
                tokens.Add(span[i].ToString());
                i++;
                continue;
            }

            if (span[i] is '\'' or '"')
            {
                var quote = span[i];
                i++;
                var start = i;
                while (i < span.Length && span[i] != quote)
                    i++;
                tokens.Add(span[start..i].ToString());
                if (i < span.Length)
                    i++;
                continue;
            }

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

            if (span[i] is '>' or '<')
            {
                tokens.Add(span[i].ToString());
                i++;
                continue;
            }

            var wordStart = i;
            while (i < span.Length
                   && !char.IsWhiteSpace(span[i])
                   && span[i] is not ('(' or ')' or '\'' or '"' or '>' or '<' or '=' or '!'))
            {
                i++;
            }

            var word = span[wordStart..i].ToString();
            if (word.Equals("AND", StringComparison.OrdinalIgnoreCase))
                tokens.Add("AND");
            else if (word.Equals("OR", StringComparison.OrdinalIgnoreCase))
                tokens.Add("OR");
            else
                tokens.Add(word);
        }

        return tokens;
    }

    private static bool IsComparisonOperator(string token) =>
        token is "==" or "!=" or ">" or ">=" or "<" or "<=";

    private static bool IsBooleanLiteral(string token) =>
        token.Equals("true", StringComparison.OrdinalIgnoreCase)
        || token.Equals("false", StringComparison.OrdinalIgnoreCase);
}