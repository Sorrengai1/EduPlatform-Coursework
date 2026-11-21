
const { test, expect } = require('@playwright/test');

test.describe('EduPlatform — навігація та бібліотека', () => {

  test('Перехід з головної сторінки до каталогу курсів', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', { name: 'Каталог курсів' }).click();

    await expect(page.getByRole('link', {
      name: 'Переглянути курс '
    }).first()).toBeVisible();
  });

  test('Перехід до бібліотеки', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', { name: 'Бібліотека' }).click();

    await expect(page.getByRole('textbox', {
      name: 'Пошук за назвою або описом'
    })).toBeVisible();
  });

  test('Пошук матеріалів у бібліотеці', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', { name: 'Бібліотека' }).click();

    const search = page.getByRole('textbox', {
      name: 'Пошук за назвою або описом'
    });

    await search.fill('Python');

    await page.getByRole('button', { name: /Знайти/ }).click();

    await expect(search).toHaveValue('Python');
  });

  test('Фільтрація матеріалів за типом', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', { name: 'Бібліотека' }).click();

    const filter = page.getByRole('combobox');

    await filter.selectOption('Відео');

    await page.getByRole('button', { name: /Знайти/ }).click();

    await expect(filter).toHaveValue('Відео');
  });

});
