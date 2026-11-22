using EduPlatform.Services;
using Xunit;

namespace EduPlatform.Tests;

public class AnswerEvaluatorTests
{
    [Fact]
    public void CorrectAnswer_ReturnsTrue()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "A" },
            new[] { "A" });

        Assert.True(result);
    }

    [Fact]
    public void IncorrectAnswer_ReturnsFalse()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "B" },
            new[] { "A" });

        Assert.False(result);
    }

    [Fact]
    public void MultipleCorrectAnswers_ReturnsTrue()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "B", "A" },
            new[] { "A", "B" });

        Assert.True(result);
    }

    [Fact]
    public void MissingCorrectAnswer_ReturnsFalse()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "A" },
            new[] { "A", "B" });

        Assert.False(result);
    }

    [Fact]
    public void ExtraIncorrectAnswer_ReturnsFalse()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "A", "B" },
            new[] { "A" });

        Assert.False(result);
    }

    [Fact]
    public void DuplicateAnswer_ReturnsFalse()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "A", "A" },
            new[] { "A" });

        Assert.False(result);
    }

    [Fact]
    public void NoCorrectOptions_ReturnsFalse()
    {
        var result = AnswerEvaluator.IsCorrect(
            new[] { "A" },
            System.Array.Empty<string>());

        Assert.False(result);
    }
}
