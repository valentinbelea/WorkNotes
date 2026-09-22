// Progressive enhancement of Razor's validation metadata; server validation remains authoritative.
document.querySelectorAll("form[data-validate]").forEach(form => {
    form.noValidate = true;
    const fields = [...form.querySelectorAll("input[data-val='true']")];
    function validate(input) {
        const d = input.dataset;
        const value = input.value;
        let error = "";
        if (d.valRequired && !value.trim()) error = d.valRequired;
        else if (value && d.valEmail && !/^[^\s@]+@[^\s@]+$/.test(value)) error = d.valEmail;
        else if (value && d.valLengthMax && value.length > Number(d.valLengthMax)) error = d.valLength;
        else if (value && d.valRegexPattern && !new RegExp(d.valRegexPattern).test(value)) error = d.valRegex;
        else if (d.valEqualtoOther) {
            const otherName = d.valEqualtoOther.replace("*.", input.name.slice(0, input.name.lastIndexOf(".") + 1));
            if (value !== form.elements.namedItem(otherName)?.value) error = d.valEqualto;
        }
        input.setAttribute("aria-invalid", error ? "true" : "false");
        const span = [...form.querySelectorAll("[data-valmsg-for]")].find(s => s.dataset.valmsgFor === input.name);
        if (span) {
            span.textContent = error;
            span.id = input.id + "-error";
            const hint = input.getAttribute("aria-describedby")?.split(" ").filter(id => id !== span.id) ?? [];
            input.setAttribute("aria-describedby", [...hint, span.id].join(" "));
        }
        return !error;
    }
    fields.forEach(input => input.addEventListener("input", () => validate(input)));
    form.addEventListener("submit", event => {
        const invalid = fields.filter(input => !validate(input));
        if (invalid.length) { event.preventDefault(); invalid[0].focus(); }
    });
});
