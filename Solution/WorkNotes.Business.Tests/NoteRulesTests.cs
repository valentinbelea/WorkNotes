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
    public void ASavedNoteHasThePreviewTheBoardReadsOfItsParagraphs()
    {
        // The board reads the first paragraphs, PreviewSourceLength characters of each: the rest never shows.
        Assert.Equal("Unu\nDoi\nTrei", NoteRules.PreviewOf(["Unu", "Doi", "Trei", "Patru"]));
        Assert.Equal("Început", NoteRules.PreviewOf(["Început" + new string(' ', NoteRules.PreviewSourceLength) + "sfârșit"]));
        Assert.Null(NoteRules.PreviewOf([]));
    }
}
