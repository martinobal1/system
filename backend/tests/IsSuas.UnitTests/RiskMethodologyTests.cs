using IsSuas.Application.Documents;
using IsSuas.Domain.Enums;

namespace IsSuas.UnitTests;

public class RiskMethodologyTests
{
    [Theory]
    [InlineData(1, 1, 1, RiskClassification.Low)]
    [InlineData(2, 2, 4, RiskClassification.Low)]
    [InlineData(3, 3, 9, RiskClassification.Medium)]
    [InlineData(4, 3, 12, RiskClassification.High)]
    [InlineData(5, 5, 25, RiskClassification.Critical)]
    public void Evaluate_ReturnsExpectedScoreAndClass(int likelihood, int severity, int score, RiskClassification expected)
    {
        var (s, c) = RiskMethodology.Evaluate(likelihood, severity);
        Assert.Equal(score, s);
        Assert.Equal(expected, c);
    }
}
