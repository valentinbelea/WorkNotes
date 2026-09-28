using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class NoteReferenceParserTests
{
    [Fact]
    public void TheConfiguredTypesAreThePrefixesRead()
    {
        var parser = new NoteReferenceParser(["CR", "BUG", "TASK"]);

        var matches = parser.Find("task 12, Task-12, TASK_13 și TASK14; CR 30080 și bug 1234");

        Assert.Equal(["TASK:12", "TASK:12", "TASK:13", "TASK:14", "CR:30080", "BUG:1234"], matches.Select(match => match.NormalizedReference));
        Assert.Equal(["task 12", "Task-12", "TASK_13", "TASK14"], matches.Take(4).Select(match => match.Text));
    }

    [Fact]
    public void ATypeThatIsNotConfiguredIsNoReference()
    {
        var parser = new NoteReferenceParser(["CR"]);

        Assert.Equal(["CR:30080"], parser.Find("CR 30080, bug 1234, TASK 12").Select(match => match.NormalizedReference));
    }

    [Fact]
    public void WithoutTypesNoTextHasReferences()
    {
        var parser = new NoteReferenceParser([]);

        Assert.Empty(parser.Types);
        Assert.Empty(parser.Find("CR 30080 și bug 1234"));
    }

    [Fact]
    public void OnlyTypesOfOneToTenCapitalAsciiLettersAreRead()
    {
        // Small letters, digits, spaces, signs, letters with diacritics or of other alphabets, and more than ten letters are
        // not types (CK_ReferenceTypes_Code keeps them out of the table); each type counts once.
        var parser = new NoteReferenceParser(["BUG", "CR", "cr", "CR2", "C R", "C-R", "ȘT", "ΑΒ", "", " ", null, "ABCDEFGHIJK", "BUG", "ABCDEFGHIJ"]);

        Assert.Equal(["ABCDEFGHIJ", "BUG", "CR"], parser.Types);
        Assert.Empty(parser.Find("ȘT 2, ΑΒ 3, C R 4, ABCDEFGHIJK 5"));
    }

    [Fact]
    public void ATypeThatStartsAnotherIsReadOnlyAsItself()
    {
        var parser = new NoteReferenceParser(["CR", "CRQ"]);

        Assert.Equal(["CRQ:12", "CR:12", "CRQ:13", "CR:14"],
            parser.Find("CRQ 12, CR 12, crq-13 și CR_14; CRQA 15 nu").Select(match => match.NormalizedReference));
    }

    [Fact]
    public void TheLongestTypeFitsTheStoredText()
    {
        var parser = new NoteReferenceParser(["ABCDEFGHIJ"]);
        var longest = "abcdefghij" + new string(' ', NoteReferenceRules.MaxSeparatorLength) + new string('9', NoteReferenceRules.MaxNumberLength);

        var match = Assert.Single(parser.Find(longest));

        Assert.Equal(NoteReferenceRules.MaxTextLength, match.Length);
        Assert.Equal("ABCDEFGHIJ:999999999999999999", match.NormalizedReference);
    }

    [Theory]
    // The Kelvin sign, the dotless i and the long s are small or capital forms of K, I and S only outside ASCII.
    [InlineData("KB", "KB 12")]
    [InlineData("KB", "kb 12", true)]
    [InlineData("IT", "ıt 12")]
    [InlineData("IT", "İT 12")]
    [InlineData("SR", "ſr 12")]
    public void EachLetterOfATypeIsReadInItsTwoAsciiFormsOnly(string type, string text, bool isReference = false) =>
        Assert.Equal(isReference ? 1 : 0, new NoteReferenceParser([type]).Find(text).Count);

    [Fact]
    public void ReferencesKeepTheirPlaceWhateverTheirType()
    {
        var parser = new NoteReferenceParser(["BUG", "CR", "TASK"]);

        var matches = parser.Find("(TASK-7) cr30080 [bug_12]");

        Assert.Equal([(1, "TASK-7", "TASK", 7L), (9, "cr30080", "CR", 30080L), (18, "bug_12", "BUG", 12L)],
            matches.Select(match => (match.Start, match.Text, match.ReferenceType, match.ReferenceNumber)));
    }
}
