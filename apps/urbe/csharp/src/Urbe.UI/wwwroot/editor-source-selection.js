// DOM-only bridge. The canonical Markdown edit and validation live in Urbe.Core.
export function capture(textarea) {
    if (!(textarea instanceof HTMLTextAreaElement)) return null;
    return {
        text: textarea.value,
        start: textarea.selectionStart,
        end: textarea.selectionEnd
    };
}

export function restore(textarea, value, start, end) {
    if (!(textarea instanceof HTMLTextAreaElement)) return;
    textarea.value = value;
    textarea.focus({ preventScroll: true });
    textarea.setSelectionRange(start, end);
}
