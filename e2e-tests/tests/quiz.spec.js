
const { test, expect } = require('@playwright/test');

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

const testCourseId = process.env.E2E_TEST_COURSE_ID || '9';

test.describe('EduPlatform — тестування знань студента', () => {

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

  async function openTestCourse(page) {
    await page.goto(`/Course/Details/${testCourseId}`);

    await expect(page).toHaveURL(
      new RegExp(`/Course/Details/${testCourseId}/?$`)
    );
  }

  function getStartButton(page) {
    return page.getByRole('button', {
      name: /Пройти тест|Продовжити тест|Пройти для самоперевірки/i
    }).first();
  }

  test('Студент може відкрити каталог курсів', async ({ page }) => {
    await page.goto('/Course');

    await expect(page).toHaveURL(/\/Course\/?$/);

    await expect(page.getByRole('link', {
      name: /Переглянути курс/
    }).first()).toBeVisible();
  });

  test('Студент може відкрити сторінку навчального курсу', async ({ page }) => {
    await openTestCourse(page);

    await expect(page.locator('body')).toBeVisible();

    await expect(page.getByRole('button', {
      name: /Пройти тест/
    }).first()).toBeVisible();
  });

  test('Студент бачить доступну кнопку проходження тесту', async ({ page }) => {
    await openTestCourse(page);

    const startButton = getStartButton(page);

    await expect(startButton).toBeVisible();
    await expect(startButton).toBeEnabled();
  });

  test('Форма запуску тесту містить правильні дані', async ({ page }) => {
    await openTestCourse(page);

    const startButton = getStartButton(page);

    const form = startButton.locator('xpath=ancestor::form');

    await expect(form).toHaveAttribute('method', /post/i);

    await expect(form).toHaveAttribute(
      'action',
      /\/Test\/Start/
    );

    await expect(
      form.locator('input[name="moduleId"]')
    ).toHaveAttribute('value', /^\d+$/);

    await expect(
      form.locator('input[name="__RequestVerificationToken"]')
    ).toBeAttached();
  });

});
