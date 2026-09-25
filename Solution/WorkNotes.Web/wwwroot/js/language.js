document.querySelectorAll("[data-language-selector]").forEach(select => {
    select.addEventListener("change", () => select.form.requestSubmit());
});

