
const { test, expect } = require('@playwright/test');

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

const teacherRoutes = [
  '/Teacher',
  '/Teacher/MyCourses',
  '/Teacher/MyTests',
  '/Teacher/CreateTest'
];

// Авторизація користувача
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

test.describe('EduPlatform — контроль доступу', () => {

  test('Гість не може відкрити кабінет викладача', async ({ page }) => {
    await page.goto('/Teacher');

    await expect(page).not.toHaveURL(/\/Teacher\/?$/);
  });

  test('Гість не може відкрити список тестів викладача', async ({ page }) => {
    await page.goto('/Teacher/MyTests');

    await expect(page).not.toHaveURL(/\/Teacher\/MyTests\/?$/);
  });

  test('Гість не може відкрити форму створення тесту', async ({ page }) => {
    await page.goto('/Teacher/CreateTest');

    await expect(page).not.toHaveURL(/\/Teacher\/CreateTest\/?$/);
  });

  test('Гість не може відкрити кабінет студента', async ({ page }) => {
    await page.goto('/Student/Dashboard');

    // Гість має бути перенаправлений на сторінку входу
    await expect(page).toHaveURL(/\/Account\/Login\/?$/);

    // Перевірка наявності форми авторизації
    await expect(
      page.getByRole('textbox', { name: 'Email: *' })
    ).toBeVisible();

    await expect(
      page.getByRole('textbox', { name: 'Пароль: *' })
    ).toBeVisible();

    await expect(
      page.getByRole('button', { name: 'Увійти' })
    ).toBeVisible();
  });

  test('Студент не може отримати доступ до функцій викладача', async ({ page }) => {
    test.skip(
      !studentEmail || !studentPassword,
      'Не задано дані тестового студента'
    );

    await login(page, studentEmail, studentPassword);

    for (const route of teacherRoutes) {
      await page.goto(route);

      const currentPath = new URL(page.url()).pathname;

      expect(
        currentPath,
        `Студент отримав доступ до ${route}`
      ).not.toMatch(
        new RegExp(`^${route}/?$`, 'i')
      );
    }
  });

  test('Викладач має доступ до власного кабінету', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);

    await page.goto('/Teacher');

    await expect(page).toHaveURL(/\/Teacher\/?$/);
  });

  test('Викладач має доступ до керування тестами', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані тестового викладача'
    );

    await login(page, teacherEmail, teacherPassword);

    await page.goto('/Teacher/MyTests');

    await expect(page).toHaveURL(/\/Teacher\/MyTests\/?$/);
  });

});

