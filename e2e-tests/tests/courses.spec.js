
const { test, expect } = require('@playwright/test');

test.describe('EduPlatform — каталог курсів', () => {

  test('Перехід до каталогу з головної сторінки', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('link', {
      name: 'Переглянути курси'
    }).click();

    await expect(page.getByRole('link', {
      name: 'Переглянути курс '
    }).first()).toBeVisible();
  });

  test('Каталог містить посилання на курси', async ({ page }) => {
    await page.goto('/Course');

    const courseLinks = page.getByRole('link', {
      name: 'Переглянути курс '
    });

    expect(await courseLinks.count()).toBeGreaterThan(0);
  });

  test('Відкриття сторінки окремого курсу', async ({ page }) => {
    await page.goto('/Course');

    await page.getByRole('link', {
      name: 'Переглянути курс '
    }).first().click();

    await expect(page.getByRole('link', {
      name: /Увійти, щоб приєднатися/
    })).toBeVisible();
  });

  test('Неавторизованому користувачу пропонується вхід', async ({ page }) => {
    await page.goto('/Course');

    await page.getByRole('link', {
      name: 'Переглянути курс '
    }).first().click();

    await page.getByRole('link', {
      name: /Увійти, щоб приєднатися/
    }).click();

    await expect(page.getByRole('button', {
      name: 'Увійти'
    })).toBeVisible();
  });

  test('Зі сторінки входу можна перейти до реєстрації', async ({ page }) => {
    await page.goto('/Course');

    await page.getByRole('link', {
      name: 'Переглянути курс '
    }).first().click();

    await page.getByRole('link', {
      name: /Увійти, щоб приєднатися/
    }).click();

    await page.getByRole('link', {
      name: 'Немає акаунта? Зареєструватися'
    }).click();

    await expect(page.getByRole('button', {
      name: 'Зареєструватися'
    })).toBeVisible();
  });

});
