// Save results added by scripts (success, warning, error). A copy of the server-rendered template for the kind
// (Pages/Shared/_StatusMessage.cshtml, so the texts come from the page) goes first in the given status region.
// It stays until the user closes it: its close button is a form with method="dialog", no script needed.
// An identical open message is replaced, so repeated saves do not pile up.
export function showStatusMessage(region, kind, text) {
    const message = document.querySelector(`template[data-status-template="${kind}"]`)?.content.firstElementChild?.cloneNode(true);
    if (!region || !message || !text) return;
    message.querySelector("[data-status-text]").textContent = text;
    for (const open of region.querySelectorAll(":scope > .status-message[open]")) {
        if (open.className === message.className && open.querySelector("[data-status-text]")?.textContent === text) open.remove();
    }
    region.prepend(message);
}
