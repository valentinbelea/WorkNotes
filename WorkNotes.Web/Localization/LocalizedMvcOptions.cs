using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;

namespace WorkNotes.Web.Localization;

public sealed class LocalizedMvcOptions(IStringLocalizer localizer) : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        var messages = options.ModelBindingMessageProvider;
        messages.SetAttemptedValueIsInvalidAccessor((_, _) => localizer["Validation_InvalidValue"]);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => localizer["Validation_InvalidValue"]);
        messages.SetValueIsInvalidAccessor(_ => localizer["Validation_InvalidValue"]);
        messages.SetValueMustNotBeNullAccessor(_ => localizer["Validation_InvalidValue"]);
        messages.SetUnknownValueIsInvalidAccessor(_ => localizer["Validation_InvalidValue"]);
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => localizer["Validation_InvalidValue"]);
        messages.SetMissingBindRequiredValueAccessor(_ => localizer["Validation_InvalidValue"]);
        messages.SetMissingKeyOrValueAccessor(() => localizer["Validation_InvalidValue"]);
        messages.SetMissingRequestBodyRequiredValueAccessor(() => localizer["Validation_InvalidValue"]);
        messages.SetValueMustBeANumberAccessor(_ => localizer["Validation_InvalidValue"]);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => localizer["Validation_InvalidValue"]);
    }
}
