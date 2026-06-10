// RackPeek graph rendering shim.
//
// Public API (called from Blazor via JSInterop):
//   window.rackpeekGraph.render(elementId, mermaidSource)
//     – Wipes the target element, runs Mermaid on the source, and inserts
//       the resulting SVG with pan/zoom enabled.
//
// Assumes Mermaid is already loaded globally (via <script src="mermaid.min.js">).
// The ELK layout plugin is loaded lazily on first render.

(function () {
    "use strict";

    let _initPromise = null;
    let _mermaidScriptPromise = null;

    function loadMermaidScript() {
        // Inject the (large) mermaid UMD bundle on first use rather than
        // shipping it on every page. Keeps non-graph pages light so Blazor's
        // SignalR circuit establishes promptly under load.
        if (_mermaidScriptPromise) return _mermaidScriptPromise;

        _mermaidScriptPromise = new Promise((resolve, reject) => {
            if (window.mermaid) {
                resolve();
                return;
            }
            const script = document.createElement("script");
            script.src = "/_content/Shared.Rcl/js/graph/mermaid.min.js";
            script.async = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error("Failed to load mermaid.min.js"));
            document.head.appendChild(script);
        });

        return _mermaidScriptPromise;
    }

    async function ensureInitialised() {
        if (_initPromise) return _initPromise;

        _initPromise = (async () => {
            await loadMermaidScript();

            if (!window.mermaid) {
                throw new Error("Mermaid bundle loaded but window.mermaid is undefined");
            }

            // Register the ELK layout loader. Mermaid 11 dispatches by the
            // `layout` config key; "elk" maps to the layered algorithm.
            try {
                const elk = await import("./mermaid-layout-elk.min.mjs");
                window.mermaid.registerLayoutLoaders(elk.default);
            } catch (e) {
                // Fall back silently to the default dagre layout — still
                // renders, just with the less polished arrow routing.
                console.warn("[rackpeekGraph] ELK plugin failed to load:", e);
            }

            window.mermaid.initialize({
                startOnLoad: false,
                securityLevel: "loose",
                theme: "dark",
                fontFamily: "ui-sans-serif, system-ui, -apple-system, sans-serif"
            });
        })();

        return _initPromise;
    }

    async function render(elementId, source) {
        const host = document.getElementById(elementId);
        if (host) host.innerHTML = "";

        // Empty source = "clear" request. Don't hand "" to mermaid.render —
        // it treats that as malformed input and produces a "Syntax error in
        // text" SVG which can leak out into the page if the host element has
        // already been detached (e.g. component disposal during navigation).
        if (!source || !source.trim()) {
            cleanupOrphans();
            return;
        }

        // Defer the (heavy) mermaid + ELK work to browser idle time so the
        // Blazor circuit and nav-click handlers stay responsive on pages
        // that render diagrams (e.g. the homepage). If the host element is
        // gone by the time idle fires (user navigated away), bail.
        await waitForIdle();
        if (!document.getElementById(elementId)) {
            cleanupOrphans();
            return;
        }

        await ensureInitialised();

        // The host may have been detached while ensureInitialised was awaiting
        // (especially on first render). Re-fetch and bail if it's gone.
        const liveHost = document.getElementById(elementId);
        if (!liveHost) {
            cleanupOrphans();
            return;
        }

        // A unique id per render avoids collisions when the same element is
        // re-rendered with different source.
        const renderId = `rpkg-${elementId}-${Date.now()}`;
        let result;
        try {
            result = await window.mermaid.render(renderId, source);
        } finally {
            // Mermaid creates a scratch <div id="d{renderId}"> in <body> for
            // measurement and normally removes it; sweep up just in case.
            const scratch = document.getElementById("d" + renderId);
            if (scratch && scratch.parentElement) scratch.parentElement.removeChild(scratch);
        }

        // Host may have been disposed during the render await.
        const stillLive = document.getElementById(elementId);
        if (!stillLive) {
            cleanupOrphans();
            return;
        }

        stillLive.innerHTML = result.svg;

        const svgEl = stillLive.querySelector("svg");
        if (svgEl) {
            // Let the SVG fill its container rather than honouring the
            // intrinsic max-width Mermaid sets, so pan/zoom feels natural.
            svgEl.removeAttribute("width");
            svgEl.removeAttribute("height");
            svgEl.style.maxWidth = "100%";
            svgEl.style.width = "100%";
            svgEl.style.height = "100%";
        }

        if (result.bindFunctions) result.bindFunctions(stillLive);
    }

    function waitForIdle() {
        return new Promise((resolve) => {
            if (typeof window.requestIdleCallback === "function") {
                // 2s timeout means we still fire eventually if the browser
                // never goes idle.
                window.requestIdleCallback(() => resolve(), { timeout: 2000 });
            } else {
                // Safari < 16 has no requestIdleCallback — fall back to a
                // short defer that still yields the current event loop.
                setTimeout(resolve, 50);
            }
        });
    }

    function cleanupOrphans() {
        // Mermaid sometimes leaves "d{renderId}" scratch nodes attached to
        // <body> when the originating host is gone — remove any that match
        // our renderId prefix.
        document.querySelectorAll("body > [id^='drpkg-']").forEach((el) => {
            el.parentElement?.removeChild(el);
        });
    }

    function triggerDownload(blob, filename) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        // Defer revoke so Safari has time to start the download.
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    function downloadSvg(elementId, filename) {
        const host = document.getElementById(elementId);
        if (!host) {
            console.warn(`[rackpeekGraph] element '${elementId}' not found`);
            return;
        }

        const svg = host.querySelector("svg");
        if (!svg) {
            console.warn(`[rackpeekGraph] no SVG in element '${elementId}' to export`);
            return;
        }

        // Clone so the in-page interactive copy isn't modified. Ensure
        // xmlns + a viewBox-derived width/height so the file renders cleanly
        // in any standalone viewer.
        const clone = svg.cloneNode(true);
        if (!clone.getAttribute("xmlns")) {
            clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
        }
        if (!clone.getAttribute("xmlns:xlink")) {
            clone.setAttribute("xmlns:xlink", "http://www.w3.org/1999/xlink");
        }
        const vb = clone.getAttribute("viewBox");
        if (vb && !clone.getAttribute("width")) {
            const parts = vb.split(/\s+/);
            if (parts.length === 4) {
                clone.setAttribute("width", parts[2]);
                clone.setAttribute("height", parts[3]);
            }
        }

        const serialiser = new XMLSerializer();
        const body = serialiser.serializeToString(clone);
        const xml = '<?xml version="1.0" encoding="UTF-8" standalone="no"?>\n' + body;
        triggerDownload(new Blob([xml], { type: "image/svg+xml;charset=utf-8" }), filename);
    }

    function downloadText(content, filename, mime) {
        triggerDownload(
            new Blob([content ?? ""], { type: (mime ?? "text/plain") + ";charset=utf-8" }),
            filename);
    }

    window.rackpeekGraph = { render, downloadSvg, downloadText };
})();
