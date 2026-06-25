namespace ElasticBreath.App.Services;

/// <summary>
/// 表达式求值器，用于计算数学表达式的值。
/// 支持加、减、乘、除四则运算，以及括号改变运算优先级。
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>
    /// 尝试计算字符串表达式的值。
    /// </summary>
    /// <param name="text">要计算的数学表达式字符串。</param>
    /// <param name="value">如果计算成功，返回表达式的结果；否则为0。</param>
    /// <param name="error">如果计算失败，返回错误描述信息；否则为空字符串。</param>
    /// <returns>如果表达式计算成功，返回 true；否则返回 false。</returns>
    public static bool TryEvaluate(string? text, out double value, out string error)
    {
        // 初始化输出参数
        value = 0;
        error = string.Empty;

        // 检查输入字符串是否为空或只包含空白字符
// 检查文本是否为空或仅包含空白字符
        if (string.IsNullOrWhiteSpace(text))
        {
            // 文本为空时，设置错误消息为"empty"并返回失败状态
            error = "empty";
            return false;
        }

        // 遍历字符串中的每个字符，检查是否都是合法的数学表达式字符
        // 遍历文本中的每个字符，逐个验证其合法性
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            // 合法字符包括：空白字符、数字、四则运算符、括号和小数点
            var valid = char.IsWhiteSpace(ch)
                || char.IsDigit(ch)
                || ch is '+' or '-' or '*' or '/' or '(' or ')' or '.';
            // 如果遇到非法字符，记录错误并返回false表示验证失败
            if (!valid)
            {
                error = "invalid_char";
                return false;
            }
        }

        try
        {
            // 创建解析器实例并进行解析
            var parser = new Parser(text);
            value = parser.Parse();
            // 检查结果是否为 NaN（非数字）或无穷大
/// <summary>
/// 检查给定的双精度浮点数值是否为NaN或无穷大。
/// </summary>
/// <param name="value">需要检查的双精度浮点数值。</param>
/// <param name="error">如果检查失败，将包含错误描述信息。</param>
/// <returns>如果值既不是NaN也不是无穷大，则返回true；否则返回false。</returns>
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                // 检测到数值为NaN或无穷大，设置错误标识并返回false
                error = "nan_or_inf";
                return false;
            }
            return true;
        }
        /// <summary>
        /// 捕获除以零异常的处理方法
        /// </summary>
        catch (DivideByZeroException)
        {
            // 捕获除以零的异常
            error = "divide_zero"; // 设置错误信息标识为除以零错误
            return false; // 返回操作失败
        }
        catch
        {
            // 捕获其他所有格式或解析错误
            error = "syntax";
            return false;
        }
    }

    /// <summary>
    /// 内部解析器类，使用递归下降法解析数学表达式。
    /// </summary>
    private sealed class Parser
    {
        private readonly string _s; // 待解析的表达式字符串
        private int _i;             // 当前解析位置索引

/// <summary>
/// 初始化 <see cref="Parser"/> 类的新实例。
/// </summary>
/// <param name="s">用于解析的字符串。</param>
        public Parser(string s)
        {
            _s = s; // 将传入的字符串赋值给私有字段 _s
        }

        /// <summary>
        /// 解析表达式并返回结果。
        /// </summary>
        /// <returns>表达式的计算结果。</returns>
        public double Parse()
        {
            // 从表达式开始解析
            var value = ParseExpression();
            // 跳过可能的尾部空白
/// <summary>
/// 解析完成后验证是否所有输入都已被处理，若有剩余字符则抛出格式异常。
/// </summary>
            SkipWs();
            // 确保整个字符串都已解析完毕，否则说明有多余字符
            if (_i != _s.Length)
            {
                throw new FormatException();
            }
            return value;
        }

        /// <summary>
        /// 解析加法和减法表达式（最低优先级）。
        /// </summary>
        /// <returns>表达式的计算结果。</returns>
        private double ParseExpression()
        {
            // 先解析一个乘除项
            var value = ParseTerm();
            // 循环处理连续的加法和减法
/// <summary>
/// 解析并计算表达式，处理加减运算符的循环。
/// </summary>
            while (true)
            {
                SkipWs(); // 跳过空白字符
                if (Match('+')) // 如果当前字符是加号
                {
                    // 遇到加号，解析下一项并累加
                    value += ParseTerm(); // 解析项并累加到当前值
                    continue; // 继续下一次循环，尝试匹配更多运算符
                }

                if (Match('-')) // 如果当前字符是减号
                {
                    // 遇到减号，解析下一项并累减
                    value -= ParseTerm(); // 解析项并从当前值中减去
                    continue; // 继续下一次循环
                }

                // 没有遇到加减号，返回当前结果
                return value; // 返回计算结果
            }
        }

        /// <summary>
        /// 解析乘法和除法表达式（较高优先级）。
        /// </summary>
        /// <returns>表达式的计算结果。</returns>
        private double ParseTerm()
        {
            // 先解析一个因子（可能是数字、括号或一元运算符）
            var value = ParseFactor();
            // 循环处理连续的乘法和除法
/// <summary>
/// 解析并计算乘除运算的方法。该方法持续循环，处理输入中的乘法和除法操作，直到遇到非乘除运算符时返回计算结果。
/// </summary>
            while (true)
            {
                SkipWs(); // 跳过空白字符
                if (Match('*'))
                {
                    // 遇到乘号，解析下一个因子并相乘
                    value *= ParseFactor();
                    continue;
                }

                if (Match('/'))
                {
                    // 遇到除号，解析下一个因子作为除数
                    var denominator = ParseFactor();
                    // 检查除数是否接近于零（避免浮点精度问题导致的除零错误）
                    if (Math.Abs(denominator) < 1e-12)
                    {
                        throw new DivideByZeroException();
                    }
                    // 执行除法
                    value /= denominator;
                    continue;
                }

                // 没有遇到乘除号，返回当前结果
                return value;
            }
        }

        /// <summary>
        /// 解析因子（一元运算符、括号表达式或数字）。
        /// </summary>
        /// <returns>因子的计算结果。</returns>
        private double ParseFactor()
        {
            SkipWs();
            // 处理一元正号
            if (Match('+'))
            {
                return ParseFactor();
            }

            // 处理一元负号
// 如果当前字符匹配到减号（负号），则解析一个因子并返回其负值
            if (Match('-'))
            {
                // 调用ParseFactor方法解析下一个因子，并返回其相反数
                return -ParseFactor();
            }

            // 处理括号表达式
            if (Match('('))
            {
                // 解析括号内的表达式
                var value = ParseExpression();
                SkipWs();
                // 确保括号闭合
                if (!Match(')'))
                {
                    throw new FormatException();
                }
                return value;
            }

            // 解析数字字面量
            return ParseNumber();
        }

        /// <summary>
        /// 解析数字（支持整数和小数）。
        /// </summary>
        /// <returns>解析出的数字值。</returns>
        private double ParseNumber()
        {
// 跳过空白字符，为解析数字做准备
            SkipWs();
            var start = _i;       // 记录数字开始位置
            var hasDot = false;   // 标记是否已经遇到小数点

            // 逐个字符读取数字部分
            while (_i < _s.Length)
            {
                var c = _s[_i];
                // 判断当前字符是否为数字
                if (char.IsDigit(c))
                {
                    _i++; // 数字字符，索引向后移动
                    continue; // 继续下一个字符
                }

                // 处理小数点（每个数字只能有一个小数点）
                if (c == '.' && !hasDot)
                {
                    hasDot = true; // 标记已遇到小数点
                    _i++; // 小数点字符，索引向后移动
                    continue; // 继续下一个字符
                }

                break; // 遇到非数字且非有效小数点，结束循环
            }

            // 如果没有读取到任何数字字符，则格式错误
// 检查start是否等于_i，如果相等则抛出FormatException异常
            if (start == _i)
            {
                throw new FormatException();
            }

            // 提取数字字符串
            var token = _s[start.._i];
            // 尝试将字符串解析为双精度浮点数（使用不变文化信息，避免区域设置影响）
/// <summary>
/// 尝试将字符串 token 解析为双精度浮点数，如果解析失败则抛出 FormatException 异常。
/// </summary>
/// <param name="token">要解析的字符串。</param>
/// <exception cref="FormatException">当 token 无法被解析为有效的浮点数时引发。</exception>
            // 使用 invariant culture 和 Float 样式进行解析，以确保行为与语言环境无关
            if (!double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                // 解析失败，抛出格式异常
                throw new FormatException();
            }

            return value;
        }

        /// <summary>
        /// 尝试匹配当前字符。如果匹配成功，则推进解析位置并返回 true。
        /// </summary>
        /// <param name="c">要匹配的字符。</param>
        /// <returns>如果匹配成功返回 true；否则返回 false。</returns>
        private bool Match(char c)
        {
            // 检查是否到达字符串末尾或当前字符不匹配
// 检查索引 _i 是否超出字符串 _s 的长度，或者当前位置的字符不等于 c
            if (_i >= _s.Length || _s[_i] != c)
            {
                // 如果条件成立，返回 false 表示匹配失败
                return false;
            }

            // 匹配成功，移动位置
            _i++;
            return true;
        }

        /// <summary>
        /// 跳过当前解析位置之后的空白字符。
        /// </summary>
        private void SkipWs()
        {
            // 遇到空白字符时，持续向后移动解析位置
/// <summary>
/// 跳过字符串中的前导空白字符。
/// 此方法通过一个while循环，递增索引 `_i`，直到遇到非空白字符或字符串结束。
/// </summary>
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) // 当索引在字符串范围内且当前字符为空白时，继续循环
            {
                _i++; // 移动索引，跳过当前空白字符
            }
        }
    }
}
