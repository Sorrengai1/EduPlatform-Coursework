
const { test, expect } = require('@playwright/test');

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

async function login(page, email, password) {
  await page.goto('/Account/Login');

  await page.getByRole('textbox', {
    name: 'Email: *'
  }).fill(email);

  await page.getByRole('textbox', {
    name: 'Пароль: *'
  }).fill(password);

  await page.getByRole('button', {
    name: 'Увійти'
  }).click();

  await expect(page).not.toHaveURL(/\/Account\/Login\/?$/);
}

test.describe('EduPlatform — керування курсами', () => {

  test('Гість перенаправляється на сторінку входу під час створення курсу', async ({ page }) => {
    await page.goto('/Course/Create');

    await expect(page).toHaveURL(/\/Account\/Login\/?$/);
  });

  test('Гість не може створити курс через POST-запит', async ({ request }) => {
    const response = await request.post('/Course/Create', {
      form: {
        Title: 'E2E Unauthorized Course',
        Description: 'Перевірка захисту створення курсу'
      },
      maxRedirects: 0
    });

    // Неавторизований запит має бути відхилений.
    const status = response.status();

    expect([302, 303, 307, 400, 401, 403]).toContain(status);

    // Якщо сервер перенаправляє запит, перевіряємо адресу.
    if ([302, 303, 307].includes(status)) {
      expect(response.headers()['location']).toMatch(
        /\/Account\/Login/i
      );
    }
  });

  test('Викладач може відкрити форму створення курсу', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);

    await page.goto('/Course/Create');

    await expect(page).toHaveURL(/\/Course\/Create\/?$/);

    await expect(
      page.getByRole('heading', { name: 'Створити новий курс' })
    ).toBeVisible();

    await expect(
      page.locator('#createCourseForm')
    ).toBeVisible();
  });

  test('Форма створення курсу містить обов’язкові поля', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);
    await page.goto('/Course/Create');

    const form = page.locator('#createCourseForm');

    await expect(form.locator('[name="Title"]')).toBeVisible();
    await expect(form.locator('[name="Description"]')).toBeVisible();

    await expect(form.locator('[name="Title"]')).toHaveAttribute('required', '');
    await expect(form.locator('[name="Description"]')).toHaveAttribute('required', '');
  });

  test('Форма створення курсу містить захист від CSRF', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);
    await page.goto('/Course/Create');

    const form = page.locator('#createCourseForm');

    await expect(form).toHaveAttribute('method', /post/i);

    const token = form.locator(
      'input[name="__RequestVerificationToken"]'
    );

    await expect(token).toHaveCount(1);
    await expect(token).not.toHaveValue('');
  });

  test('Порожня назва курсу блокує відправлення форми', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);
    await page.goto('/Course/Create');

    const title = page.locator('#createCourseForm [name="Title"]');

    await title.fill('');

    const isInvalid = await title.evaluate(
      element => !element.checkValidity()
    );

    expect(isInvalid).toBe(true);

    await expect(page).toHaveURL(/\/Course\/Create\/?$/);
  });

  test('Порожній опис курсу блокує відправлення форми', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);
    await page.goto('/Course/Create');

    const description = page.locator(
      '#createCourseForm [name="Description"]'
    );

    await description.fill('');

    const isInvalid = await description.evaluate(
      element => !element.checkValidity()
    );

    expect(isInvalid).toBe(true);

    await expect(page).toHaveURL(/\/Course\/Create\/?$/);
  });

  test('Студент не може відкрити форму створення курсу', async ({ page }) => {
    test.skip(
      !studentEmail || !studentPassword,
      'Не задано дані тестового студента'
    );

    await login(page, studentEmail, studentPassword);

    const response = await page.goto('/Course/Create');

    expect(response).not.toBeNull();

    // Сервер не повинен повертати успішну сторінку створення.
    expect(response.status()).not.toBe(200);

    await expect(
      page.locator('#createCourseForm')
    ).toHaveCount(0);
  });

});
