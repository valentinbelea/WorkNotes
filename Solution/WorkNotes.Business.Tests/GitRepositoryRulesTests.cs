using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class GitRepositoryRulesTests
{
    [Theory]
    [InlineData("1296269", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("12a", false)]
    [InlineData("-1", false)]
    [InlineData("１２", false)]
    public void AnIdIsAsciiDigits(string? id, bool valid) =>
        Assert.Equal(valid, GitRepositoryRules.ValidId(id));

    [Fact]
    public void AnIdLongerThanTheColumnIsRefused() =>
        Assert.False(GitRepositoryRules.ValidId(new string('1', GitRepositoryRules.IdMaxLength + 1)));

    [Fact]
    public void AValidRepositoryIsKeptWithItsNameTrimmed()
    {
        var normalized = GitRepositoryRules.Normalize(Repository() with { FullName = " octocat/Hello-World " });

        Assert.Equal(Repository(), normalized);
    }

    [Theory]
    [InlineData("", "octocat/Hello-World", "https://github.com/octocat/Hello-World")]
    [InlineData("1296269", " ", "https://github.com/octocat/Hello-World")]
    [InlineData("1296269", "octocat/\nHello", "https://github.com/octocat/Hello-World")]
    [InlineData("1296269", "octocat/Hello-World", "http://github.com/octocat/Hello-World")]
    [InlineData("1296269", "octocat/Hello-World", "javascript:alert(1)")]
    [InlineData("1296269", "octocat/Hello-World", "/octocat/Hello-World")]
    public void ARepositoryThatCannotBeStoredIsLeftOut(string id, string fullName, string url) =>
        Assert.Null(GitRepositoryRules.Normalize(Repository() with { Id = id, FullName = fullName, HtmlUrl = url }));

    [Fact]
    public void ANameOrUrlLongerThanItsColumnIsLeftOut()
    {
        Assert.Null(GitRepositoryRules.Normalize(Repository() with { FullName = new string('a', GitRepositoryRules.FullNameMaxLength + 1) }));
        Assert.Null(GitRepositoryRules.Normalize(Repository() with { HtmlUrl = "https://github.com/" + new string('a', GitRepositoryRules.HtmlUrlMaxLength) }));
    }

    [Fact]
    public void TheDescriptionLosesItsControlCharactersAndIsCutToTheColumn()
    {
        Assert.Equal("My first repository", GitRepositoryRules.Normalize(Repository() with { Description = " My first\nrepository\t" })?.Description);
        Assert.Null(GitRepositoryRules.Normalize(Repository() with { Description = "  " })?.Description);
        Assert.Equal(GitRepositoryRules.DescriptionMaxLength,
            GitRepositoryRules.Normalize(Repository() with { Description = new string('d', 1000) })?.Description?.Length);
    }

    [Fact]
    public void ABranchThatDoesNotFitIsLeftOut()
    {
        Assert.Null(GitRepositoryRules.Normalize(Repository() with { DefaultBranch = new string('b', GitRepositoryRules.DefaultBranchMaxLength + 1) })?.DefaultBranch);
        Assert.Null(GitRepositoryRules.Normalize(Repository() with { DefaultBranch = "" })?.DefaultBranch);
        Assert.Equal("main", GitRepositoryRules.Normalize(Repository())?.DefaultBranch);
    }

    private static GitRepositoryInfo Repository() =>
        new("1296269", "octocat/Hello-World", "My first repository", false, "main", "https://github.com/octocat/Hello-World");
}
