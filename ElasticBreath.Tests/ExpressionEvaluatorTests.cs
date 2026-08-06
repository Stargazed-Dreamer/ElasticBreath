using ElasticBreath.App.Services;
using Xunit;

namespace ElasticBreath.Tests;

public class ExpressionEvaluatorTests
{
    [Theory]
    [InlineData("2+3", 5)]
    [InlineData("10-4", 6)]
    [InlineData("6*7", 42)]
    [InlineData("8/2", 4)]
    public void TryEvaluate_BasicArithmetic(string expr, double expected)
    {
        Assert.True(ExpressionEvaluator.TryEvaluate(expr, out var value, out var error));
        Assert.Equal(string.Empty, error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("2+3*4", 14)]
    [InlineData("(2+3)*4", 20)]
    public void TryEvaluate_OperatorPrecedence(string expr, double expected)
    {
        Assert.True(ExpressionEvaluator.TryEvaluate(expr, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-5", -5)]
    [InlineData("3*-2", -6)]
    public void TryEvaluate_UnaryMinus(string expr, double expected)
    {
        Assert.True(ExpressionEvaluator.TryEvaluate(expr, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryEvaluate_Decimals()
    {
        Assert.True(ExpressionEvaluator.TryEvaluate("1.5+2.5", out var value, out _));
        Assert.Equal(4, value);
    }

    [Fact]
    public void TryEvaluate_WhitespaceTolerance()
    {
        Assert.True(ExpressionEvaluator.TryEvaluate(" 2 + 3 ", out var value, out _));
        Assert.Equal(5, value);
    }

    [Theory]
    [InlineData("35*60", 2100)]
    [InlineData("5*60", 300)]
    public void TryEvaluate_SettingsExpressions(string expr, double expected)
    {
        Assert.True(ExpressionEvaluator.TryEvaluate(expr, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryEvaluate_PureNumber()
    {
        Assert.True(ExpressionEvaluator.TryEvaluate("42", out var value, out _));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryEvaluate_Null_ReturnsFalseWithError()
    {
        Assert.False(ExpressionEvaluator.TryEvaluate(null, out var value, out var error));
        Assert.Equal(0, value);
        Assert.Equal("empty", error);
    }

    [Fact]
    public void TryEvaluate_Empty_ReturnsFalseWithError()
    {
        Assert.False(ExpressionEvaluator.TryEvaluate("", out var value, out var error));
        Assert.Equal(0, value);
        Assert.Equal("empty", error);
    }

    [Fact]
    public void TryEvaluate_WhitespaceOnly_ReturnsFalseWithError()
    {
        Assert.False(ExpressionEvaluator.TryEvaluate("   ", out _, out var error));
        Assert.Equal("empty", error);
    }

    [Fact]
    public void TryEvaluate_InvalidChar_ReturnsFalseWithError()
    {
        Assert.False(ExpressionEvaluator.TryEvaluate("2^3", out _, out var error));
        Assert.Equal("invalid_char", error);
    }

    [Fact]
    public void TryEvaluate_SyntaxError_ReturnsFalseWithError()
    {
        Assert.False(ExpressionEvaluator.TryEvaluate("2+", out _, out var error));
        Assert.Equal("syntax", error);
    }

    [Fact]
    public void TryEvaluate_DivideByZero_ReturnsFalseWithError()
    {
        Assert.False(ExpressionEvaluator.TryEvaluate("1/0", out _, out var error));
        Assert.Equal("divide_zero", error);
    }
}
