using CS2Panel;
using Xunit;

public class ValidationTests
{
    [Theory]
    [InlineData("UI/buttonclick")]
    [InlineData("player/headshot1.vsnd")]
    [InlineData("sounds/my-sound_01.mp3")]
    public void SafeSound_accepts_plain_paths(string s) => Assert.True(Validation.IsSafeSound(s));

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a;quit")]
    [InlineData("a\"b")]
    [InlineData("../x y")]
    [InlineData("snd\nquit")]
    public void SafeSound_rejects_anything_that_could_break_out_of_the_command(string s) => Assert.False(Validation.IsSafeSound(s));

    [Fact]
    public void SafeSound_rejects_null_and_overlong() { Assert.False(Validation.IsSafeSound(null)); Assert.False(Validation.IsSafeSound(new string('a', 161))); Assert.True(Validation.IsSafeSound(new string('a', 160))); }

    [Theory]
    [InlineData("")]
    [InlineData("NAVI")]
    [InlineData("Team Spirit")]
    [InlineData("Игрок №1")]
    public void SafeText_accepts_names_and_empty(string s) => Assert.True(Validation.IsSafeText(s));

    [Theory]
    [InlineData("a\"b")]
    [InlineData("a;b")]
    [InlineData("a\\b")]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    public void SafeText_rejects_command_injection_characters(string s) => Assert.False(Validation.IsSafeText(s));

    [Fact]
    public void SafeText_limits_length_to_32() { Assert.True(Validation.IsSafeText(new string('x', 32))); Assert.False(Validation.IsSafeText(new string('x', 33))); Assert.True(Validation.IsSafeText(null)); }

    [Theory]
    [InlineData("", true)]
    [InlineData("navi", true)]
    [InlineData("team_vitality2", true)]
    [InlineData("na vi", false)]
    [InlineData("navi;", false)]
    public void SafeLogo(string s, bool ok) => Assert.Equal(ok, Validation.IsSafeLogo(s));

    [Theory]
    [InlineData("", true)]
    [InlineData("ru", true)]
    [InlineData("RU", true)]
    [InlineData("rus", false)]
    [InlineData("r1", false)]
    public void SafeFlag(string s, bool ok) => Assert.Equal(ok, Validation.IsSafeFlag(s));
}
