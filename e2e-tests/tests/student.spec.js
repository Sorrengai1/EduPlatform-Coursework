
const { test, expect } = require('@playwright/test');

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

test.describe('EduPlatform — кабінет студента', () => {

  test.beforeEach(async ({ page }) => {
    test.skip(
      !studentEmail || !studentPassword,
      'Не задано дані тестового студента'
    );

    await page.goto('/Account/Login');

    await page.getByRole('textbox', {
      name: 'Email: *'
    }).fill(studentEmail);

    await page.getByRole('textbox', {
      name: 'Пароль: *'
    }).fill(studentPassword);

    await page.getByRole('button', {
      name: 'Увійти'
    }).click();

    await expect(page.getByRole('link', {
      name: 'Кабінет студента'
    })).toBeVisible();
  });

  test('Відкриття кабінету студента', async ({ page }) => {
    await page.getByRole('link', {
      name: 'Кабінет студента'
    }).click();

    await expect(page.locator('body')).toBeVisible();

    await expect(page.getByRole('link', {
      name: /Прогрес/
    })).toBeVisible();
  });

  test('Перехід до прогресу навчання', async ({ page }) => {
    await page.getByRole('link', {
      name: 'Кабінет студента'
    }).click();

    await page.getByRole('link', {
      name: /Прогрес/
    }).click();

    await expect(page.locator('body')).toBeVisible();
  });

  test('Перехід до списку курсів студента', async ({ page }) => {
    await page.getByRole('link', {
      name: 'Кабінет студента'
    }).click();

    await page.getByRole('link', {
      name: /Курси/
    }).last().click();

    await expect(page.locator('body')).toBeVisible();
  });

 test('Перехід до каталогу курсів', async ({ page }) => {
  await page.goto('/Course');

  await expect(page).toHaveURL(/\/Course\/?$/);

  await expect(page.locator('body')).toBeVisible();

  await expect(page.locator('main, .container').first()).toBeVisible();
});
});
