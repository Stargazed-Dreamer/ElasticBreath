namespace ElasticBreath.App.Services;

public static class ExpressionEvaluator
{
    public static bool TryEvaluate(string? text, out double value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "empty";
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var valid = char.IsWhiteSpace(ch)
                || char.IsDigit(ch)
                || ch is '+' or '-' or '*' or '/' or '(' or ')' or '.';
            if (!valid)
            {
                error = "invalid_char";
                return false;
            }
        }

        try
        {
            var parser = new Parser(text);
            value = parser.Parse();
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                error = "nan_or_inf";
                return false;
            }
            return true;
        }
        catch (DivideByZeroException)
        {
            error = "divide_zero";
            return false;
        }
        catch
        {
            error = "syntax";
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;

        public Parser(string s)
        {
            _s = s;
        }

        public double Parse()
        {
            var value = ParseExpression();
            SkipWs();
            if (_i != _s.Length)
            {
                throw new FormatException();
            }
            return value;
        }

        private double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWs();
                if (Match('+'))
                {
                    value += ParseTerm();
                    continue;
                }

                if (Match('-'))
                {
                    value -= ParseTerm();
                    continue;
                }

                return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWs();
                if (Match('*'))
                {
                    value *= ParseFactor();
                    continue;
                }

                if (Match('/'))
                {
                    var denominator = ParseFactor();
                    if (Math.Abs(denominator) < 1e-12)
                    {
                        throw new DivideByZeroException();
                    }
                    value /= denominator;
                    continue;
                }

                return value;
            }
        }

        private double ParseFactor()
        {
            SkipWs();
            if (Match('+'))
            {
                return ParseFactor();
            }

            if (Match('-'))
            {
                return -ParseFactor();
            }

            if (Match('('))
            {
                var value = ParseExpression();
                SkipWs();
                if (!Match(')'))
                {
                    throw new FormatException();
                }
                return value;
            }

            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipWs();
            var start = _i;
            var hasDot = false;
            while (_i < _s.Length)
            {
                var c = _s[_i];
                if (char.IsDigit(c))
                {
                    _i++;
                    continue;
                }

                if (c == '.' && !hasDot)
                {
                    hasDot = true;
                    _i++;
                    continue;
                }

                break;
            }

            if (start == _i)
            {
                throw new FormatException();
            }

            var token = _s[start.._i];
            if (!double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException();
            }

            return value;
        }

        private bool Match(char c)
        {
            if (_i >= _s.Length || _s[_i] != c)
            {
                return false;
            }

            _i++;
            return true;
        }

        private void SkipWs()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i]))
            {
                _i++;
            }
        }
    }
}
