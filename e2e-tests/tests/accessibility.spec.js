
const { test, expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;

test('EduPlatform — перевірка доступності WCAG 2.2 AA', async ({ page }) => {
    await page.goto('http://localhost:5225/');

    const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
        .analyze();

    console.log('\nПорушень доступності:', results.violations.length);

    for (const violation of results.violations) {
        console.log(`\n${violation.id}: ${violation.help}`);
        console.log(`Вплив: ${violation.impact}`);

        for (const node of violation.nodes) {
            console.log(`Елемент: ${node.target.join(', ')}`);
            console.log(`Причина: ${node.failureSummary}`);
        }
    }

    expect(results.violations).toEqual([]);
});
