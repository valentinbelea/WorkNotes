using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class AccountRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ana\nMaria")]
    public void InvalidNamesAreRejected(string? name) => Assert.False(AccountRules.ValidName(name));

    [Theory]
    [InlineData("Ștefan")]
    [InlineData(" Ana-Maria ")]
    [InlineData("O'Connor")]
    public void NamesAllowDiacriticsAndPunctuation(string name) => Assert.True(AccountRules.ValidName(name));

    [Fact]
    public void NamesMustFitDatabaseColumn() => Assert.False(AccountRules.ValidName(new string('a', 101)));

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("a@@b.ro")]
    public void InvalidEmailIsRejected(string? email) => Assert.False(AccountRules.ValidEmail(email));
}
