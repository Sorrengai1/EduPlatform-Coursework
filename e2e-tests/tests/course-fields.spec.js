
const { test, expect } = require('@playwright/test');

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

// Перетворення HTML-сутностей на звичайний текст.
function decodeHtmlEntities(value) {
  return value
    .replace(/&#x([0-9a-f]+);/gi, (_, hex) =>
      String.fromCodePoint(parseInt(hex, 16))
    )
    .replace(/&#([0-9]+);/g, (_, number) =>
      String.fromCodePoint(parseInt(number, 10))
    )
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>');
}

// Отримання серверного повідомлення валідації.
function getValidationMessage(html, fieldName) {
  const pattern = new RegExp(
    `<span\\b(?=[^>]*\\bdata-valmsg-for="${fieldName}")` +
    `[^>]*>([\\s\\S]*?)<\\/span>`,
    'i'
  );

  const match = html.match(pattern);

  expect(
    match,
    `Не знайдено повідомлення для поля ${fieldName}`
  ).not.toBeNull();

  return decodeHtmlEntities(
    match[1].replace(/<[^>]*>/g, '').trim()
  );
}

test.describe('EduPlatform — валідація параметрів курсу', () => {

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

  async function submitInvalidCourse(page, fields) {
    const token = await page.locator(
      '#createCourseForm input[name="__RequestVerificationToken"]'
    ).inputValue();

    expect(token).toBeTruthy();

    const response = await page.request.post('/Course/Create', {
      form: {
        __RequestVerificationToken: token,
        Title: 'E2E перевірка параметрів',
        Description: 'Перевірка серверної валідації параметрів курсу.',
        Level: fields.Level ?? 'Beginner',
        DurationHours: fields.DurationHours ?? '10',
        IsPublic: 'true'
      },
      maxRedirects: 0
    });

    expect(response.status()).toBe(200);

    const html = await response.text();

    expect(html).toContain('id="createCourseForm"');

    return html;
  }

  test('Сервер відхиляє недопустимий рівень складності', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Level: 'Expert'
    });

    expect(getValidationMessage(html, 'Level')).toContain(
      'Оберіть допустимий рівень складності.'
    );
  });

  test('Сервер відхиляє порожній рівень складності', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Level: ''
    });

    expect(getValidationMessage(html, 'Level')).toContain(
      'Оберіть рівень складності.'
    );
  });

  test('Сервер відхиляє нульову тривалість курсу', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      DurationHours: '0'
    });

    expect(getValidationMessage(html, 'DurationHours')).toContain(
      'Тривалість має бути від 1 до 10000 годин.'
    );
  });

  test('Сервер відхиляє від’ємну тривалість курсу', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      DurationHours: '-5'
    });

    expect(getValidationMessage(html, 'DurationHours')).toContain(
      'Тривалість має бути від 1 до 10000 годин.'
    );
  });

  test('Сервер відхиляє тривалість понад 10000 годин', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      DurationHours: '10001'
    });

    expect(getValidationMessage(html, 'DurationHours')).toContain(
      'Тривалість має бути від 1 до 10000 годин.'
    );
  });

  test('Сервер відхиляє текст замість тривалості', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      DurationHours: 'abc'
    });

    const message = getValidationMessage(html, 'DurationHours');

    expect(message.length).toBeGreaterThan(0);
  });

});
