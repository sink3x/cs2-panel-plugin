using CS2Panel;
using Xunit;

public class TargetSpecTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("all")]
    [InlineData("ALL")]
    public void Empty_or_all_means_everybody(string? s) => Assert.Equal(TargetKind.All, TargetSpec.Parse(s).Kind);

    [Fact]
    public void Me_is_case_insensitive() => Assert.Equal(TargetKind.Me, TargetSpec.Parse("Me").Kind);

    [Fact]
    public void Hash_number_is_a_user_id()
    {
        var t = TargetSpec.Parse("#12");
        Assert.Equal(TargetKind.UserId, t.Kind);
        Assert.Equal(12, t.UserId);
    }

    [Fact]
    public void Seventeen_digits_are_a_steam_id()
    {
        var t = TargetSpec.Parse("76561198000000000");
        Assert.Equal(TargetKind.SteamId, t.Kind);
        Assert.Equal(76561198000000000UL, t.SteamId);
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("7656119800000000")]       // 16 digits: just a name
    [InlineData("76561198000000000x")]
    [InlineData("john")]
    public void Anything_else_is_part_of_a_nickname(string s)
    {
        var t = TargetSpec.Parse(s);
        Assert.Equal(TargetKind.Name, t.Kind);
        Assert.Equal(s, t.Name);
    }
}
