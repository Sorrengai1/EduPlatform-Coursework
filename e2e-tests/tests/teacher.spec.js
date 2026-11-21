
const { test, expect } = require('@playwright/test');

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

test.describe('EduPlatform — кабінет викладача', () => {

  test.beforeEach(async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await page.goto('/Account/Login');

    await page.getByRole('textbox', {
      name: 'Email: *'
    }).fill(teacherEmail);

    await page.getByRole('textbox', {
      name: 'Пароль: *'
    }).fill(teacherPassword);

    await page.getByRole('button', {
      name: 'Увійти'
    }).click();

    await expect(page).not.toHaveURL(/\/Account\/Login/);
  });

  test('Викладач може відкрити свій кабінет', async ({ page }) => {
    await page.goto('/Teacher');

    await expect(page).toHaveURL(/\/Teacher\/?$/);

    await expect(page.locator('body')).toContainText(
      /Статистика|Курси|Тести/
    );
  });

  test('Викладач може переглянути свої курси', async ({ page }) => {
    await page.goto('/Teacher/MyCourses');

    await expect(page).toHaveURL(/\/Teacher\/MyCourses\/?$/);

    await expect(page.locator('body')).toContainText(/Курси/);
  });

  test('Викладач може переглянути авторські тести', async ({ page }) => {
    await page.goto('/Teacher/MyTests');

    await expect(page).toHaveURL(/\/Teacher\/MyTests\/?$/);

    await expect(page.getByRole('link', {
      name: /Створити новий тест/
    })).toBeVisible();
  });

  test('Викладач може відкрити форму створення тесту', async ({ page }) => {
    await page.goto('/Teacher/MyTests');

    await page.getByRole('link', {
      name: /Створити новий тест/
    }).click();

    await expect(page).toHaveURL(/\/Teacher\/CreateTest/);

    await expect(page.locator('form').first()).toBeVisible();
  });

});
