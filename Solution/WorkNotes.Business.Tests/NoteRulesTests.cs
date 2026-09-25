using WorkNotes.Business.Models;

namespace WorkNotes.Business.Tests;

public sealed class NoteRulesTests
{
    [Fact]
    public void PreviewKeepsShortParagraphsOnePerLine() =>
        Assert.Equal("Primul paragraf\nAl doilea", NoteRules.BuildPreview(["  Primul paragraf ", "", "Al doilea"]));

    [Fact]
    public void NoTextMeansNoPreview()
    {
        Assert.Null(NoteRules.BuildPreview([]));
        Assert.Null(NoteRules.BuildPreview([" ", ""]));
        Assert.Null(NoteRules.BuildPreview(null));
    }

    [Fact]
    public void LongPreviewIsShortenedAtAWordBoundary()
    {
        var words = string.Join(' ', Enumerable.Repeat("cuvânt", 60));

        var preview = NoteRules.BuildPreview([words])!;

        Assert.EndsWith("cuvânt…", preview);
        Assert.True(preview.Length <= NoteRules.PreviewMaxLength + 1);
    }

    [Fact]
    public void AVeryLongWordIsCutAtTheLimit()
    {
        var preview = NoteRules.BuildPreview([new string('x', 500)])!;

        Assert.Equal(new string('x', NoteRules.PreviewMaxLength) + "…", preview);
    }

    [Fact]
    public void AReferenceCountsAsTheNumberItShows()
    {
        // 260 + 5 + 4 characters are shown, although the stored text is longer than the limit.
        var text = string.Concat(Enumerable.Repeat("a ", 130)) + "[[note:12|30080]] fin";

        Assert.Equal(text, NoteRules.BuildPreview([text]));
    }

    [Fact]
    public void AReferenceIsNeverCutInHalf()
    {
        // The limit falls inside the number: the reference is left out whole.
        var preview = NoteRules.BuildPreview([new string('x', NoteRules.PreviewMaxLength - 2) + "[[note:12|30080]]"])!;

        Assert.Equal(new string('x', NoteRules.PreviewMaxLength - 2) + "…", preview);
    }

    [Fact]
    public void AReferenceBeforeTheCutIsKept()
    {
        var words = string.Join(' ', Enumerable.Repeat("cuvânt", 60));

        var preview = NoteRules.BuildPreview(["Vezi [[note:12|30080]] " + words])!;

        Assert.StartsWith("Vezi [[note:12|30080]] cuvânt", preview);
        Assert.EndsWith("cuvânt…", preview);
    }

    [Fact]
    public void AReferenceCutWhereTheParagraphWasReadIsDropped()
    {
        // The board reads PreviewSourceLength characters of a paragraph; this one stops inside a reference.
        var start = string.Concat(Enumerable.Repeat("a ", (NoteRules.PreviewSourceLength - 8) / 2));
        var read = (start + "[[note:12|30080]]")[..NoteRules.PreviewSourceLength];

        var preview = NoteRules.BuildPreview([read])!;

        Assert.DoesNotContain("[[", preview);
        Assert.EndsWith("a…", preview);
    }
}
