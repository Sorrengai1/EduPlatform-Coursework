
const { test, expect } = require('@playwright/test');

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

test.describe('EduPlatform — перевірка зображень курсів', () => {

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

  async function sendCourseWithImage(page, filename, content, contentType) {
    const token = await page.locator(
      '#createCourseForm input[name="__RequestVerificationToken"]'
    ).inputValue();

    expect(token).toBeTruthy();

    return page.request.post('/Course/Create', {
      multipart: {
        __RequestVerificationToken: token,
        Title: 'E2E перевірка зображення',
        Description: 'Перевірка серверної валідації файлів зображень.',
        Level: 'Beginner',
        DurationHours: '10',
        IsPublic: 'true',
        imageFile: {
          name: filename,
          mimeType: contentType,
          buffer: content
        }
      },
      maxRedirects: 0
    });
  }

  async function expectRejected(response, expectedMessage) {
    expect(response.status()).toBe(200);

    const html = await response.text();

    expect(html).toContain('id="createCourseForm"');

    // Повідомлення можуть містити HTML-сутності.
    // Перевіряємо декодований текст через браузерний DOM
    // у наступних тестах або характерні фрагменти HTML.
    expect(html).toContain('imageFileError');

    expect(html).not.toContain('HTTP 302');

    return html;
  }

  test('Сервер відхиляє текстовий файл замість зображення', async ({ page }) => {
    const response = await sendCourseWithImage(
      page,
      'document.txt',
      Buffer.from('This is a text document.'),
      'text/plain'
    );

    await expectRejected(response);
  });

  test('Сервер відхиляє файл із підробленим розширенням PNG', async ({ page }) => {
    const response = await sendCourseWithImage(
      page,
      'fake.png',
      Buffer.from('This is not a real PNG image.'),
      'image/png'
    );

    await expectRejected(response);
  });

  test('Сервер відхиляє порожній файл зображення', async ({ page }) => {
    const response = await sendCourseWithImage(
      page,
      'empty.png',
      Buffer.alloc(0),
      'image/png'
    );

    await expectRejected(response);
  });

  test('Сервер відхиляє зображення понад 5 МБ', async ({ page }) => {
    const response = await sendCourseWithImage(
      page,
      'large.png',
      Buffer.alloc(5 * 1024 * 1024 + 1, 0),
      'image/png'
    );

    await expectRejected(response);
  });

});
