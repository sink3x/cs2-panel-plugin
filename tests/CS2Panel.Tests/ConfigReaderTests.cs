using CS2Panel;
using Xunit;

public class ConfigReaderTests
{
    [Fact]
    public void Identity_reads_what_the_panel_writes()
    {
        var id = ConfigReader.ParseIdentity("""
            { "teams": { "ct": { "name": "NAVI", "logo": "navi", "flag": "UA" }, "t": { "name": "FaZe", "logo": "faze", "flag": "" } },
              "players": { "76561198000000000": { "tag": "TAG", "name": "Nick" } } }
            """);
        Assert.Equal("NAVI", id.Teams["ct"].Name);
        Assert.Equal("faze", id.Teams["t"].Logo);
        Assert.Equal("TAG", id.Players["76561198000000000"].Tag);
        Assert.Equal("Nick", id.Players["76561198000000000"].Name);
    }

    [Fact]
    public void Keys_are_case_insensitive() => Assert.Equal("X", ConfigReader.ParseIdentity("""{"Teams":{"ct":{"NAME":"X"}}}""").Teams["ct"].Name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"teams\": ")]
    [InlineData("[]")]
    public void Broken_identity_never_throws(string? json)
    {
        var id = ConfigReader.ParseIdentity(json);
        Assert.Empty(id.Teams);
        Assert.Empty(id.Players);
    }

    [Fact]
    public void Library_reads_entries()
    {
        var lib = ConfigReader.ParseLibrary("""{ "ding": { "play": "UI/buttonclick", "title": "Ding" }, "x": { "play": "a/b" } }""");
        Assert.Equal("UI/buttonclick", lib["ding"].Play);
        Assert.Equal("Ding", lib["ding"].Title);
        Assert.Equal("", lib["x"].Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("oops")]
    [InlineData("[1,2]")]
    public void Broken_library_never_throws(string? json) => Assert.Empty(ConfigReader.ParseLibrary(json));
}
