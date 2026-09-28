using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class NoteReferenceRulesTests
{
    [Theory]
    [InlineData("CR 30080", "CR", 30080)]
    [InlineData("CR-30080", "CR", 30080)]
    [InlineData("CR_30080", "CR", 30080)]
    [InlineData("CR30080", "CR", 30080)]
    [InlineData("cr 30080", "CR", 30080)]
    [InlineData("Cr-30080", "CR", 30080)]
    [InlineData("cR_30080", "CR", 30080)]
    [InlineData("bug 30042", "BUG", 30042)]
    [InlineData("bug-30042", "BUG", 30042)]
    [InlineData("bug_30042", "BUG", 30042)]
    [InlineData("bug30042", "BUG", 30042)]
    [InlineData("BUG 30042", "BUG", 30042)]
    [InlineData("Bug-30042", "BUG", 30042)]
    [InlineData("bUg30042", "BUG", 30042)]
    public void EveryWrittenFormIsOneReference(string text, string type, long number)
    {
        var match = Assert.Single(NoteReferenceRules.Find(text));

        // The text keeps the separator and the case it was written with; type and number are normalized.
        Assert.Equal((0, text, type, number), (match.Start, match.Text, match.ReferenceType, match.ReferenceNumber));
        Assert.Equal($"{type}:{number}", match.NormalizedReference);
    }

    [Theory]
    [InlineData("CR   30080")]
    [InlineData("CR 30080")]
    [InlineData("bug   30042")]
    public void SeveralSpacesAndNoBreakSpacesSeparateTheTypeFromTheNumber(string text) =>
        Assert.Equal(text, Assert.Single(NoteReferenceRules.Find(text)).Text);

    [Fact]
    public void AtMostFiftySpacesSeparateTheTypeFromTheNumber()
    {
        Assert.Single(NoteReferenceRules.Find("CR" + new string(' ', NoteReferenceRules.MaxSeparatorLength) + "30080"));
        Assert.Empty(NoteReferenceRules.Find("CR" + new string(' ', NoteReferenceRules.MaxSeparatorLength + 1) + "30080"));
    }

    [Fact]
    public void TheNumberHasAtMostEighteenDigits()
    {
        var longest = "CR " + new string('9', NoteReferenceRules.MaxNumberLength);

        Assert.Equal(999_999_999_999_999_999, Assert.Single(NoteReferenceRules.Find(longest)).ReferenceNumber);
        Assert.Equal(NoteReferenceRules.MaxTextLength,
            Assert.Single(NoteReferenceRules.Find("BUG" + new string(' ', NoteReferenceRules.MaxSeparatorLength) + new string('9', NoteReferenceRules.MaxNumberLength))).Length);
        // A longer number is not cut to fit: it is no reference.
        Assert.Empty(NoteReferenceRules.Find(longest + "9"));
    }

    [Theory]
    [InlineData("XCR30080A")]
    [InlineData("XCR30080")]
    [InlineData("CR30080A")]
    [InlineData("CR 30080a")]
    [InlineData("MCR 30080")]
    [InlineData("debug 1234")]
    [InlineData("bugs 1234")]
    [InlineData("BUGCR 30080")]
    [InlineData("1CR 30080")]
    [InlineData("CR 300801ă")]
    [InlineData("ăCR 30080")]
    [InlineData("CR 30080́")]
    [InlineData("CR 30080٣")]
    [InlineData("CR - 30080")]
    [InlineData("CR--30080")]
    [InlineData("CR-_30080")]
    [InlineData("CR_ 30080")]
    [InlineData("CR\t30080")]
    [InlineData("CR\n30080")]
    [InlineData("CR")]
    [InlineData("CR 3OO8O")]
    [InlineData("C R 30080")]
    [InlineData("ＣＲ 30080")]
    [InlineData("CR ３００８０")]
    public void OnlyAWholeTypeAndAWholeNumberMakeAReference(string text) =>
        Assert.Empty(NoteReferenceRules.Find(text));

    [Theory]
    [InlineData("(CR-30080)", "CR-30080")]
    [InlineData("Vezi CR30080, apoi", "CR30080")]
    [InlineData("feature/CR-30080_export", "CR-30080")]
    [InlineData("am rezolvat bug 1234.", "bug 1234")]
    [InlineData("„CR 30080”", "CR 30080")]
    [InlineData("CR 30080-2", "CR 30080")]
    public void PunctuationAroundAReferenceIsNotPartOfIt(string text, string reference) =>
        Assert.Equal(reference, Assert.Single(NoteReferenceRules.Find(text)).Text);

    [Fact]
    public void LeadingZerosAreNotPartOfTheNumber()
    {
        var match = Assert.Single(NoteReferenceRules.Find("CR 030080"));

        Assert.Equal(("CR 030080", "CR:30080"), (match.Text, match.NormalizedReference));
    }

    [Fact]
    public void EveryReferenceOfATextIsFoundInOrder()
    {
        var matches = NoteReferenceRules.Find("CR 30080 și bug_30080, apoi Cr-30081 și iar CR30080.");

        Assert.Equal([(0, "CR 30080", "CR:30080"), (12, "bug_30080", "BUG:30080"), (28, "Cr-30081", "CR:30081"), (44, "CR30080", "CR:30080")],
            matches.Select(match => (match.Start, match.Text, match.NormalizedReference)));
    }

    [Fact]
    public void ACrAndABugWithTheSameNumberAreDifferentReferences()
    {
        var matches = NoteReferenceRules.Find("CR 1234 / bug 1234");

        Assert.Equal(["CR:1234", "BUG:1234"], matches.Select(match => match.NormalizedReference));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Fără referințe: 30080, CRs, bug-uri.")]
    public void TextWithoutReferencesHasNone(string? text) =>
        Assert.Empty(NoteReferenceRules.Find(text));

    [Fact]
    public void TitlesNameTheReferencesTheyHave()
    {
        var titles = NoteReferenceRules.NotesByTitleReference(
        [
            (12, "CR 30080 Export facturi"),
            (13, "Rezolvare Bug-1234"),
            (14, "Testare CR_30080 și bug 512"),
            (15, null),
            (16, "CRs")
        ]);

        Assert.Equal([12, 14], titles["CR:30080"]);
        Assert.Equal([13], titles["BUG:1234"]);
        Assert.Equal([14], titles["BUG:512"]);
        Assert.Equal(3, titles.Count);
    }

    [Fact]
    public void ATitleWritingAReferenceTwiceNamesItOnce() =>
        Assert.Equal([12], NoteReferenceRules.NotesByTitleReference([(12, "CR 30080 (CR-30080)")])["CR:30080"]);

    [Fact]
    public void AReferenceOpensEveryOtherNoteWithItInItsTitle()
    {
        var titles = NoteReferenceRules.NotesByTitleReference(
            [(12, "CR 30080"), (13, "bug 30080"), (15, "Testare CR 512"), (14, "CR 512"), (7, "CR 777 și CR 512")]);

        Assert.Equal([12], NoteReferenceRules.TargetsOf(titles, "CR:30080", 7));
        Assert.Equal([13], NoteReferenceRules.TargetsOf(titles, "BUG:30080", 7));
        // Several notes: all of them, in the order of their ids; the note itself is never one of them.
        Assert.Equal([14, 15], NoteReferenceRules.TargetsOf(titles, "CR:512", 7));
        Assert.Equal([7, 15], NoteReferenceRules.TargetsOf(titles, "CR:512", 14));
        // No note, or only the note itself: no link.
        Assert.Empty(NoteReferenceRules.TargetsOf(titles, "CR:9999", 7));
        Assert.Empty(NoteReferenceRules.TargetsOf(titles, "CR:777", 7));
    }

    [Fact]
    public void EveryPlaceAParagraphWritesALinkedReferenceIsALink()
    {
        const string text = "CR 30080, cr-30080 și CR_30080; bug 30080 nu.";

        var links = NoteReferenceRules.LinksIn(text, normalized => normalized == "CR:30080" ? [12, 15] : []);

        Assert.Equal([(0, 8), (10, 8), (22, 8)], links.Select(link => (link.Start, link.Length)));
        Assert.Equal(["CR 30080", "cr-30080", "CR_30080"], links.Select(link => text.Substring(link.Start, link.Length)));
        // Each place opens all the notes of the reference.
        Assert.All(links, link => Assert.Equal([12, 15], link.TargetNoteIds));
    }
}
