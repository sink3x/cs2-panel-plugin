using CS2Panel;
using Xunit;

public class AnimalRulesTests
{
    [Theory]
    [InlineData("chicken", true)]
    [InlineData("PIG", true)]
    [InlineData("cow", true)]
    [InlineData("dragon", false)]
    [InlineData("", false)]
    public void IsKnown(string kind, bool ok) => Assert.Equal(ok, AnimalRules.IsKnown(kind));

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(7, 7)]
    [InlineData(30, 30)]
    [InlineData(31, 30)]
    [InlineData(10_000, 30)]
    public void ClampCount(int requested, int expected) => Assert.Equal(expected, AnimalRules.ClampCount(requested));

    [Theory]
    [InlineData(0, 30, true)]
    [InlineData(30, 30, true)]
    [InlineData(31, 30, false)]
    [InlineData(60, 1, false)]
    [InlineData(59, 1, true)]
    public void Fits(int existing, int count, bool ok) => Assert.Equal(ok, AnimalRules.Fits(existing, count));
}
