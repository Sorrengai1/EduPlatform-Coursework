
const { test, expect } = require('@playwright/test');

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

// Декодування HTML-сутностей у повідомленнях ASP.NET Core.
// Наприклад: &#x412; перетворюється на "В".
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
    .replace(/&gt;/g, '>')
    .replace(/&nbsp;/g, ' ');
}

// Отримання повідомлення серверної валідації
// для конкретного поля з HTML-відповіді.
function getValidationMessage(html, fieldName) {
  const escapedName = fieldName.replace(
    /[.*+?^${}()|[\]\\]/g,
    '\\$&'
  );

  const pattern = new RegExp(
    `<span\\b(?=[^>]*\\bdata-valmsg-for="${escapedName}")` +
    `[^>]*>([\\s\\S]*?)<\\/span>`,
    'i'
  );

  const match = html.match(pattern);

  expect(
    match,
    `Не знайдено елемент валідації для поля ${fieldName}`
  ).not.toBeNull();

  return decodeHtmlEntities(
    match[1].replace(/<[^>]*>/g, '').trim()
  );
}

test.describe('EduPlatform — серверна валідація курсів', () => {

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
    const form = page.locator('#createCourseForm');

    const token = await form.locator(
      'input[name="__RequestVerificationToken"]'
    ).inputValue();

    expect(token).toBeTruthy();

    const response = await page.request.post('/Course/Create', {
      form: {
        __RequestVerificationToken: token,
        Title: fields.Title ?? '',
        Description: fields.Description ?? '',
        Level: fields.Level ?? 'Beginner',
        DurationHours: '10',
        IsPublic: 'true'
      },
      maxRedirects: 0
    });

    // Некоректні дані мають повернути форму з помилками,
    // а не перенаправлення на створений курс.
    expect(response.status()).toBe(200);

    const html = await response.text();

    expect(html).toContain('id="createCourseForm"');

    return html;
  }

  test('Сервер відхиляє курс без назви', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Title: '',
      Description: 'Тестовий опис навчального курсу'
    });

    const message = getValidationMessage(html, 'Title');

    expect(message).toContain('Вкажіть назву курсу.');
  });

  test('Сервер відхиляє курс без опису', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Title: 'E2E перевірка опису',
      Description: ''
    });

    const message = getValidationMessage(html, 'Description');

    expect(message).toContain('Вкажіть опис курсу.');
  });

  test('Сервер відхиляє курс без назви та опису', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Title: '',
      Description: ''
    });

    const titleMessage = getValidationMessage(html, 'Title');
    const descriptionMessage = getValidationMessage(html, 'Description');

    expect(titleMessage).toContain('Вкажіть назву курсу.');
    expect(descriptionMessage).toContain('Вкажіть опис курсу.');
  });

  test('Сервер відхиляє назву довшу за 150 символів', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Title: 'А'.repeat(151),
      Description: 'Коректний опис тестового курсу'
    });

    const message = getValidationMessage(html, 'Title');

    expect(message).toContain(
      'Назва курсу має містити від 3 до 150 символів.'
    );
  });

  test('Сервер відхиляє опис довший за 3000 символів', async ({ page }) => {
    const html = await submitInvalidCourse(page, {
      Title: 'E2E перевірка довжини опису',
      Description: 'А'.repeat(3001)
    });

    const message = getValidationMessage(html, 'Description');

    expect(message).toContain(
      'Опис курсу має містити від 10 до 3000 символів.'
    );
  });

});
