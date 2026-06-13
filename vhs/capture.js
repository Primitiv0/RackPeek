const { chromium } = require('playwright');
const fs = require('fs');

const URLS = [
  "http://localhost:5287",
  "http://localhost:5287/visualise/topology",
  "http://localhost:5287/visualise/logical",
  "http://localhost:5287/cli",
  "http://localhost:5287/yaml",
  "http://localhost:5287/hardware/tree",
  "http://localhost:5287/servers/list",
  "http://localhost:5287/resources/hardware/proxmox-node01",
  "http://localhost:5287/systems/list",
  "http://localhost:5287/services/list"
];

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({
    viewport: { width: 1366, height: 768 }
  });

  if (!fs.existsSync("./webui_screenshots"))
    fs.mkdirSync("./webui_screenshots");

  for (const url of URLS) {
    const filename = url.replace(/^https?:\/\//, '').replace(/\//g, '_') + ".png";
    console.log("Capturing", url);

    // Diagram routes are taller than the standard viewport; stretch it so
    // the GraphView's `h-full` container shows the whole diagram in-frame.
    const isVisualise = url.includes("/visualise/");
    if (isVisualise) {
      await page.setViewportSize({ width: 2400, height: 5000 });
    } else {
      await page.setViewportSize({ width: 1366, height: 768 });
    }

    await page.goto(url, {
      waitUntil: "networkidle",
      timeout: 30000
    });

    // extra settle time for SPA hydration; Mermaid renders async so the
    // visualise routes need a longer window before screenshotting.
    await page.waitForTimeout(isVisualise ? 5000 : 2000);

    // The visualise route wraps the diagram in an overflow:auto container
    // bounded to 75vh — fine for the live UI, but it clips taller diagrams
    // in screenshots. For the capture only, expand any ancestor of the SVG
    // to its natural height and disable overflow clipping.
    if (isVisualise) {
      await page.evaluate(() => {
        const svg = document.querySelector("[id^='visualise-graph-host'] svg, #visualise-graph-host svg");
        if (!svg) return;
        let el = svg.parentElement;
        while (el && el !== document.body) {
          el.style.height = "auto";
          el.style.maxHeight = "none";
          el.style.overflow = "visible";
          el = el.parentElement;
        }
      });
      await page.waitForTimeout(500);
    }

    await page.screenshot({
      path: `webui_screenshots/${filename}`,
      fullPage: isVisualise
    });
  }

  await browser.close();
})();
