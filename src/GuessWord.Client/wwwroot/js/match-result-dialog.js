// Native modal makes the background inert and handles Escape.
export function show(dialog, returnFocus, returnFocusSelector) {
    if (dialog.open) return;
    if (returnFocusSelector) returnFocus = document.querySelector(returnFocusSelector);
    if (!returnFocus?.isConnected) returnFocus = document.activeElement;
    const keepFocus = event => {
        if (event.key !== 'Tab' || !dialog.open) return;
        const controls = [...dialog.querySelectorAll('button:not([disabled]), [href], input:not([disabled]), [tabindex]:not([tabindex="-1"])')];
        const first = controls[0], last = controls.at(-1);
        if (!dialog.contains(document.activeElement)) {
            event.preventDefault();
            (event.shiftKey ? last : first)?.focus();
        } else if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    };
    document.addEventListener('keydown', keepFocus);
    dialog.addEventListener('close', () => {
        document.removeEventListener('keydown', keepFocus);
        if (returnFocus?.isConnected) returnFocus.focus();
    }, { once: true });
    dialog.showModal();
}

export function close(dialog) { if (dialog?.open) dialog.close(); }
