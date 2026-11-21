const { test, expect } = require('@playwright/test');

test.describe('EduPlatform — перевірка сторінок', () => {

  test('Головна сторінка відкривається', async ({ page }) => {
    const response = await page.goto('/');

    expect(response.status()).toBe(200);
    await expect(page.locator('body')).toBeVisible();
  });

  test('Сторінка входу відкривається', async ({ page }) => {
    const response = await page.goto('/Account/Login');

    expect(response.status()).toBe(200);
    await expect(page.locator('form')).toBeVisible();
  });

  test('Сторінка реєстрації відкривається', async ({ page }) => {
    const response = await page.goto('/Account/Register');

    expect(response.status()).toBe(200);
    await expect(page.locator('form')).toBeVisible();
  });

  test('Каталог курсів відкривається', async ({ page }) => {
    const response = await page.goto('/Course');

    expect(response.status()).toBe(200);
    await expect(page.locator('body')).toBeVisible();
  });

  test('Неіснуюча сторінка повертає 404', async ({ page }) => {
    const response = await page.goto('/UnknownPageForTesting123');

    expect(response.status()).toBe(404);
  });

});