using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class NoteReferenceRulesTests
{
    [Theory]
    [InlineData("300", true)]
    [InlineData("30080", true)]
    [InlineData("123456789012345678", true)]
    [InlineData("12", false)]
    [InlineData("1234567890123456789", false)]
    [InlineData("30a80", false)]
    [InlineData("30 80", false)]
    [InlineData("-3008", false)]
    [InlineData("٣٠٠٨٠", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ReferenceNumbersAreAsciiDigitsOfTheAllowedLength(string? value, bool expected) =>
        Assert.Equal(expected, NoteReferenceRules.IsReferenceNumber(value));

    [Theory]
    [InlineData("CR 30080", true)]
    [InlineData("30080", true)]
    [InlineData("CR-30080: analiză", true)]
    [InlineData("CR30080", true)]
    [InlineData("Bug 130080 și CR 30080", true)]
    [InlineData("CR 130080", false)]
    [InlineData("CR 300801", false)]
    [InlineData("CR 3008", false)]
    [InlineData(null, false)]
    public void TitleHasTheNumberOnlyAsAWholeNumber(string? title, bool expected) =>
        Assert.Equal(expected, NoteReferenceRules.TitleContainsNumber(title, "30080"));

    [Fact]
    public void AShortNumberIsNeverLookedForInTitles() =>
        Assert.False(NoteReferenceRules.TitleContainsNumber("Sprint 12", "12"));

    [Fact]
    public void ReferencesAreSplitFromThePlainText()
    {
        var parts = NoteReferenceRules.Split("Vezi [[note:12|30080]], apoi [[note:7|512]]");

        Assert.Equal([new NoteTextPart("Vezi ", null), new NoteTextPart("30080", 12), new NoteTextPart(", apoi ", null), new NoteTextPart("512", 7)],
            parts);
    }

    [Fact]
    public void AFormattedReferenceIsReadBack() =>
        Assert.Equal([new NoteTextPart("30080", 12)], NoteReferenceRules.Split(NoteReferenceRules.Format(12, "30080")));

    [Theory]
    [InlineData("[[note:x|30080]]")]
    [InlineData("[[note:12|30a80]]")]
    [InlineData("[[note:12|]]")]
    [InlineData("[[note:0|30080]]")]
    [InlineData("[[note:99999999999|30080]]")]
    [InlineData("[[note:12|30080]")]
    [InlineData("[note:12|30080]]")]
    [InlineData("[[Note:12|30080]]")]
    public void AnythingElseStaysPlainText(string text) =>
        Assert.Equal([new NoteTextPart(text, null)], NoteReferenceRules.Split(text));

    [Fact]
    public void NoTextHasNoParts()
    {
        Assert.Empty(NoteReferenceRules.Split(null));
        Assert.Empty(NoteReferenceRules.Split(""));
    }

    [Fact]
    public void EachTargetAndNumberIsFoundOnce()
    {
        var references = NoteReferenceRules.Find([
            "[[note:12|30080]] și [[note:12|30080]]",
            "Același număr, altă notă: [[note:13|30080]]",
            "Altă formă a titlului: [[note:12|512]]",
            "Fără referințe"
        ]);

        Assert.Equal([new NoteReferenceInput(12, "30080"), new NoteReferenceInput(13, "30080"), new NoteReferenceInput(12, "512")], references);
    }

    [Theory]
    [InlineData("Vezi [[note:12|300", "Vezi ")]
    [InlineData("Vezi [[note:", "Vezi ")]
    [InlineData("[[note:12|30080]] și [[note:1", "[[note:12|30080]] și ")]
    [InlineData("Vezi [[note:12|30080]]", "Vezi [[note:12|30080]]")]
    [InlineData("Fără referințe", "Fără referințe")]
    public void AnUnfinishedReferenceAtTheEndIsDropped(string text, string expected) =>
        Assert.Equal(expected, NoteReferenceRules.WithoutUnfinishedReference(text));
}
