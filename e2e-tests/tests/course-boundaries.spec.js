
const { test, expect } = require('@playwright/test');

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

test.describe('EduPlatform — граничні значення полів курсу', () => {
  test.skip(
    !teacherEmail || !teacherPassword,
    'Не задано дані тестового викладача'
  );

  test.beforeEach(async ({ page }) => {
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

    await expect(page).not.toHaveURL(/\/Account\/Login\/?$/);

    await page.goto('/Course/Create');

    await expect(
      page.locator('#createCourseForm')
    ).toBeVisible();
  });

  async function setValidForm(page) {
    await page.locator('#Title').fill('Тестовий курс');
    await page.locator('#Description').fill(
      'Коректний опис навчального курсу.'
    );
    await page.locator('#Level').selectOption('Beginner');
    await page.locator('#DurationHours').fill('10');
  }

  async function isFieldValid(page, selector) {
    return page.locator(selector).evaluate(
      element => element.checkValidity()
    );
  }

  test('Назва з 3 символів відповідає мінімальній довжині', async ({ page }) => {
    await setValidForm(page);

    const title = page.locator('#Title');
    await title.fill('АБВ');

    expect(await isFieldValid(page, '#Title')).toBe(true);
    await expect(title).toHaveValue('АБВ');
  });

  test('Назва зі 150 символів відповідає максимальній довжині', async ({ page }) => {
    await setValidForm(page);

    const title = page.locator('#Title');
    await title.fill('А'.repeat(150));

    expect(await isFieldValid(page, '#Title')).toBe(true);
    await expect(title).toHaveValue('А'.repeat(150));
  });

  test('Опис із 10 символів відповідає мінімальній довжині', async ({ page }) => {
    await setValidForm(page);

    const description = page.locator('#Description');
    await description.fill('А'.repeat(10));

    expect(await isFieldValid(page, '#Description')).toBe(true);
    await expect(description).toHaveValue('А'.repeat(10));
  });

  test('Опис із 3000 символів відповідає максимальній довжині', async ({ page }) => {
    await setValidForm(page);

    const description = page.locator('#Description');
    await description.fill('А'.repeat(3000));

    expect(await isFieldValid(page, '#Description')).toBe(true);
    await expect(description).toHaveValue('А'.repeat(3000));
  });

  test('Тривалість 1 година є допустимою', async ({ page }) => {
    await setValidForm(page);

    await page.locator('#DurationHours').fill('1');

    expect(await isFieldValid(page, '#DurationHours')).toBe(true);
  });

  test('Тривалість 10000 годин є допустимою', async ({ page }) => {
    await setValidForm(page);

    await page.locator('#DurationHours').fill('10000');

    expect(await isFieldValid(page, '#DurationHours')).toBe(true);
  });

  test('Тривалість 0 годин є недопустимою', async ({ page }) => {
    await setValidForm(page);

    await page.locator('#DurationHours').fill('0');

    expect(await isFieldValid(page, '#DurationHours')).toBe(false);
  });

  test('Тривалість 10001 година є недопустимою', async ({ page }) => {
    await setValidForm(page);

    await page.locator('#DurationHours').fill('10001');

    expect(await isFieldValid(page, '#DurationHours')).toBe(false);
  });
});
