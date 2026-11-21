
const { test, expect } = require('@playwright/test');

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

test.describe('EduPlatform — сценарії роботи студента', () => {
  test.skip(
    !studentEmail || !studentPassword,
    'Не задано дані тестового студента'
  );

  test.beforeEach(async ({ page }) => {
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

    await page.goto('/Student/Dashboard');

    await expect(page).toHaveURL(/\/Student\/Dashboard\/?$/);

    await expect(
      page.locator('main').first()
    ).toBeVisible();
  });

  test('Студент може відкрити особистий кабінет', async ({ page }) => {
    await expect(
      page.locator('main').first()
    ).toContainText('Продовжити навчання');
  });

  test('Студент може переглянути власні курси', async ({ page }) => {
    const response = await page.goto('/Student/MyCourses');

    expect(response.status()).toBe(200);

    await expect(page).toHaveURL(/\/Student\/MyCourses\/?$/);

    await expect(
      page.locator('main').first()
    ).toBeVisible();
  });

  test('Студент може переглянути власний прогрес', async ({ page }) => {
    const response = await page.goto('/Student/MyProgress');

    expect(response.status()).toBe(200);

    await expect(page).toHaveURL(/\/Student\/MyProgress\/?$/);

    await expect(
      page.locator('main').first()
    ).toBeVisible();
  });

  test('Студент може перейти до каталогу курсів', async ({ page }) => {
    const response = await page.goto('/Course');

    expect(response.status()).toBe(200);

    await expect(page).toHaveURL(/\/Course\/?$/);

    await expect(
      page.locator('main').first()
    ).toBeVisible();
  });

  test('Студент зберігає авторизацію після переходів', async ({ page }) => {
    const routes = [
      '/Student/Dashboard',
      '/Student/MyCourses',
      '/Student/MyProgress',
      '/Course',
      '/Student/Dashboard'
    ];

    for (const route of routes) {
      const response = await page.goto(route);

      expect(
        response.status(),
        `Помилка сторінки ${route}`
      ).toBe(200);

      await expect(page).toHaveURL(
        new RegExp(`${route.replace(/\//g, '\\/')}/?$`)
      );

      await expect(
        page.locator('main').first()
      ).toBeVisible();
    }
  });

  test('Студент може відкрити доступний курс зі свого кабінету', async ({ page }) => {
    await expect(page).toHaveURL(/\/Student\/Dashboard\/?$/);

    const courseLinks = page.locator(
      'main a[href^="/Course/Details/"]'
    );

    const count = await courseLinks.count();

    if (count === 0) {
      // Якщо немає посилань на курси, перевіряємо каталог.
      const catalogLink = page.locator(
        'main a[href="/Course"]'
      ).first();

      if (await catalogLink.count() > 0) {
        await catalogLink.click();
      } else {
        await page.goto('/Course');
      }

      await expect(page).toHaveURL(/\/Course\/?$/);

      await expect(
        page.locator('main').first()
      ).toBeVisible();

      return;
    }

    const courseLink = courseLinks.first();

    const href = await courseLink.getAttribute('href');

    expect(href).toMatch(/^\/Course\/Details\/\d+/);

    await courseLink.click();

    await expect(page).toHaveURL(/\/Course\/Details\/\d+/);

    await expect(
      page.locator('main').first()
    ).toBeVisible();
  });

  test('Студент не може відкрити форму створення курсу', async ({ page }) => {
    const response = await page.goto('/Course/Create');

    expect(response.status()).not.toBe(200);

    await expect(
      page.locator('#createCourseForm')
    ).toHaveCount(0);
  });

  test('Студент не може відкрити кабінет викладача', async ({ page }) => {
    await page.goto('/Teacher');

    await expect(page).not.toHaveURL(/\/Teacher\/?$/);
  });
});


