// RackPeek UI helpers — lightweight Blazor JSInterop primitives that don't
// fit cleanly into a Razor component on their own. Currently exposes a
// "dismiss me when the user interacts outside this element" helper used by
// the mobile nav dropdown.
//
// Public API (called from Blazor via JSInterop):
//   window.rackpeekUi.registerOutsideDismiss(id, containerSelector, dotnetRef, methodName)
//     Listens for clicks/touches/Escape outside `containerSelector` and
//     invokes `methodName` on the supplied .NET ref. Idempotent — calling
//     twice with the same id replaces the prior handler.
//
//   window.rackpeekUi.unregisterOutsideDismiss(id)
//     Removes the previously-registered handlers for `id`.

(function () {
    "use strict";

    const _state = new Map();

    function registerOutsideDismiss(id, containerSelector, dotnetRef, methodName) {
        unregisterOutsideDismiss(id);

        const dismiss = () => {
            dotnetRef.invokeMethodAsync(methodName).catch(() => {
                // Component already disposed — drop the call silently.
            });
        };

        const pointerHandler = (e) => {
            const container = document.querySelector(containerSelector);
            if (!container) return;
            if (container.contains(e.target)) return;
            dismiss();
        };

        const keyHandler = (e) => {
            if (e.key === "Escape") dismiss();
        };

        // Listen on pointerdown rather than click so the dropdown closes
        // immediately when interaction starts elsewhere — feels snappier and
        // also catches drags / scrolls beginning outside the menu.
        document.addEventListener("pointerdown", pointerHandler, true);
        document.addEventListener("keydown", keyHandler, true);

        _state.set(id, { pointerHandler, keyHandler });
    }

    function unregisterOutsideDismiss(id) {
        const entry = _state.get(id);
        if (!entry) return;
        document.removeEventListener("pointerdown", entry.pointerHandler, true);
        document.removeEventListener("keydown", entry.keyHandler, true);
        _state.delete(id);
    }

    window.rackpeekUi = { registerOutsideDismiss, unregisterOutsideDismiss };
})();
