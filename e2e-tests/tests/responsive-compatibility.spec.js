
const { test, expect, chromium, firefox, webkit } = require('@playwright/test');

const browsers = {
  chromium,
  firefox,
  webkit
};

const widths = [360, 768, 1280];

for (const [browserName, browserType] of Object.entries(browsers)) {
  for (const width of widths) {
    test(`${browserName}: адаптивність при ${width}px`, async () => {
      const browser = await browserType.launch();

      try {
        const context = await browser.newContext({
          viewport: { width, height: 900 },
	  ignoreHTTPSErrors: true
        });

        const page = await context.newPage();

        const response = await page.goto('http://localhost:5225/', {
          waitUntil: 'networkidle'
        });

        expect(response).not.toBeNull();
        expect(response.status()).toBeLessThan(400);

        const dimensions = await page.evaluate(() => ({
          viewportWidth: document.documentElement.clientWidth,
          documentWidth: document.documentElement.scrollWidth
        }));

        console.log(
          `${browserName} | ${width}px | ` +
          `documentWidth=${dimensions.documentWidth}px`
        );

        expect(dimensions.documentWidth).toBeLessThanOrEqual(
          dimensions.viewportWidth + 1
        );

        await page.screenshot({
          path: `screenshots/${browserName}-${width}.png`,
          fullPage: true
        });

        await context.close();
      } finally {
        await browser.close();
      }
    });
  }
}
