using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class GitReferenceRulesTests
{
    private static readonly NoteReferenceParser Parser = new(["CR", "BUG"]);

    [Theory]
    [InlineData("CR:30080", "CR", 30080)]
    [InlineData("BUG:1234", "BUG", 1234)]
    [InlineData("CR:0", "CR", 0)]
    public void AReferenceIsReadAsTheNotesNormalizeIt(string reference, string type, long number)
    {
        var match = GitReferenceRules.ParseReference(Parser, reference);

        Assert.Equal((type, number), (match?.ReferenceType, match?.ReferenceNumber));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("TASK:12")]
    [InlineData("CR30080")]
    [InlineData("CR-30080")]
    [InlineData("cr:30080")]
    [InlineData("CR:030080")]
    [InlineData("CR: 30080")]
    [InlineData("CR:30080:1")]
    [InlineData("CR:")]
    [InlineData("CR:12a")]
    [InlineData("CR:3008012345678901234567")]
    public void ATextThatIsNotANormalizedReferenceIsRefused(string? reference) =>
        Assert.Null(GitReferenceRules.ParseReference(Parser, reference));

    [Fact]
    public void ATypeThatIsNotConfiguredIsRefused() =>
        Assert.Null(GitReferenceRules.ParseReference(new NoteReferenceParser(["BUG"]), "CR:30080"));

    [Theory]
    [InlineData("CR_30080", true)]
    [InlineData("CR-30080", true)]
    [InlineData("CR30080", true)]
    [InlineData("cr_30080", true)]
    [InlineData("feature/CR_30080_export", true)]
    [InlineData("feature/CR-30080-export", true)]
    [InlineData("feature/some-CR_30080", true)]
    [InlineData("feature/cr30080", true)]
    [InlineData("CR_00030080", true)]
    [InlineData("feature/CR 30080", true)]
    [InlineData("feature/CR_30081", false)]
    [InlineData("feature/CR_300801", false)]
    [InlineData("feature/CR_3008", false)]
    [InlineData("feature/CR_30080a", false)]
    [InlineData("feature/XCR_30080", false)]
    [InlineData("feature/BUG_30080", false)]
    [InlineData("feature/30080", false)]
    [InlineData("main", false)]
    public void ABranchNameContainsTheReferenceByTheRulesOfTheNotes(string branch, bool matches) =>
        Assert.Equal(matches, GitReferenceRules.BranchMatches(Parser, branch, "CR:30080"));

    [Fact]
    public void ATypeIsNotFoundInsideAWord()
    {
        Assert.True(GitReferenceRules.BranchMatches(Parser, "bugfix/BUG1234", "BUG:1234"));
        Assert.False(GitReferenceRules.BranchMatches(Parser, "release/debug-1234", "BUG:1234"));
    }

    [Fact]
    public void ABranchNameWithSeveralReferencesMatchesEachOfThem()
    {
        Assert.True(GitReferenceRules.BranchMatches(Parser, "feature/CR_1_and_BUG_2", "CR:1"));
        Assert.True(GitReferenceRules.BranchMatches(Parser, "feature/CR_1_and_BUG_2", "BUG:2"));
        Assert.False(GitReferenceRules.BranchMatches(Parser, "feature/CR_1_and_BUG_2", "CR:2"));
    }

    [Fact]
    public void AParagraphWritesAReferenceInAnyForm()
    {
        Assert.True(GitReferenceRules.Writes(Parser, "See cr 30080 and more", "CR:30080"));
        Assert.True(GitReferenceRules.Writes(Parser, "CR_030080", "CR:30080"));
        Assert.False(GitReferenceRules.Writes(Parser, "See CR 30081", "CR:30080"));
        Assert.False(GitReferenceRules.Writes(Parser, null, "CR:30080"));
    }

    [Theory]
    [InlineData("main", true)]
    [InlineData("feature/CR_30080", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    [InlineData("a\nb", false)]
    public void ABranchNameIsStoredWhenItFitsTheColumn(string? name, bool valid) =>
        Assert.Equal(valid, GitReferenceRules.ValidBranchName(name));

    [Fact]
    public void ABranchNameLongerThanTheColumnIsRefused()
    {
        Assert.True(GitReferenceRules.ValidBranchName(new string('b', GitReferenceRules.BranchNameMaxLength)));
        Assert.False(GitReferenceRules.ValidBranchName(new string('b', GitReferenceRules.BranchNameMaxLength + 1)));
    }

    [Theory]
    [InlineData("https://github.com/o/r", "feature/CR_1", "https://github.com/o/r/tree/feature/CR_1")]
    [InlineData("https://github.com/o/r/", "main", "https://github.com/o/r/tree/main")]
    [InlineData("https://github.com/o/r", "fix/a b#c%d", "https://github.com/o/r/tree/fix/a%20b%23c%25d")]
    public void TheUrlOfABranchKeepsItsPathAndEncodesEachPart(string repository, string branch, string expected) =>
        Assert.Equal(expected, GitReferenceRules.BranchUrl(repository, branch));
}
