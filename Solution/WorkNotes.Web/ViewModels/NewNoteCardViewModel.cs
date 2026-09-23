using WorkNotes.Business.Models;

namespace WorkNotes.Web.ViewModels;

// The new-note card needs the form values and the contexts the user may write in.
public sealed record NewNoteCardViewModel(NewNoteInput Input, IReadOnlyList<WorkContext> Contexts);
