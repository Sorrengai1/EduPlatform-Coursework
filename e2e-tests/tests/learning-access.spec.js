
const { test, expect } = require('@playwright/test');

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

const teacherEmail = process.env.E2E_TEACHER_EMAIL;
const teacherPassword = process.env.E2E_TEACHER_PASSWORD;

// Пошук доступного курсу через каталог.
async function openCourse(page) {
  await page.goto('/Course');

  const links = page.locator('a[href*="/Course/Details/"]');

  await expect(links.first()).toBeVisible();

  const href = await links.first().getAttribute('href');

  expect(href).toMatch(/\/Course\/Details\/\d+/);

  const response = await page.goto(href);

  expect(response.status()).toBe(200);

  await expect(
    page.locator('.course-details-page')
  ).toBeVisible();
}

// Авторизація користувача.
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
}

test.describe('EduPlatform — контроль доступу до навчання', () => {

  test('Гість може переглядати інформацію про курс', async ({ page }) => {
    await openCourse(page);

    await expect(
      page.locator('.course-heading')
    ).not.toBeEmpty();

    await expect(
      page.locator('#course-materials-title')
    ).toBeVisible();

    await expect(
      page.locator('#course-tests-title')
    ).toBeVisible();
  });

  test('Гість не бачить кнопок запуску тестування', async ({ page }) => {
    await openCourse(page);

    await expect(
      page.locator('form[action*="/Test/Start"]')
    ).toHaveCount(0);

    await expect(
      page.getByRole('link', {
        name: 'Увійти, щоб приєднатися'
      })
    ).toBeVisible();
  });

  test('Неавторизований користувач не може відкрити сторінку проходження тесту', async ({ page }) => {
    await page.goto('/Test/Take?attemptId=1');

    await expect(page).toHaveURL(
      /\/Account\/Login/
    );
  });

  test('Неавторизований користувач не може переглянути результат тесту', async ({ page }) => {
    await page.goto('/Test/Result?attemptId=1');

    await expect(page).toHaveURL(
      /\/Account\/Login/
    );
  });

  test('Студент не бачить керування курсом викладача', async ({ page }) => {
    test.skip(
      !studentEmail || !studentPassword,
      'Не задано дані студента'
    );

    await login(page, studentEmail, studentPassword);

    await openCourse(page);

    await expect(
      page.getByRole('link', {
        name: 'Керувати курсом'
      })
    ).toHaveCount(0);
  });

  test('Студент бачить статус запису або кнопку приєднання', async ({ page }) => {
    test.skip(
      !studentEmail || !studentPassword,
      'Не задано дані студента'
    );

    await login(page, studentEmail, studentPassword);

    await openCourse(page);

    const enrolled = page.locator('.course-enrolled');

    const joinButton = page.getByRole('button', {
      name: 'Приєднатися до курсу'
    });

    const enrolledCount = await enrolled.count();
    const joinCount = await joinButton.count();

    expect(enrolledCount + joinCount).toBe(1);

    if (enrolledCount === 1) {
      await expect(enrolled).toContainText(
        'Ви вже на курсі'
      );
    } else {
      await expect(joinButton).toBeVisible();
    }
  });

  test('Форми запуску тестів містять CSRF-токен', async ({ page }) => {
    test.skip(
      !studentEmail || !studentPassword,
      'Не задано дані студента'
    );

    await login(page, studentEmail, studentPassword);

    await openCourse(page);

    const forms = page.locator(
      'form[action*="/Test/Start"]'
    );

    const count = await forms.count();

    for (let i = 0; i < count; i++) {
      const form = forms.nth(i);

      await expect(form).toHaveAttribute(
        'method',
        /post/i
      );

      await expect(
        form.locator(
          'input[name="__RequestVerificationToken"]'
        )
      ).toHaveCount(1);

      await expect(
        form.locator('input[name="moduleId"]')
      ).toHaveCount(1);
    }

    // За відсутності доступних форм перевірка
    // не створює нових спроб тестування.
  });

  test('Викладач не бачить студентських кнопок проходження тесту', async ({ page }) => {
    test.skip(
      !teacherEmail || !teacherPassword,
      'Не задано дані викладача'
    );

    await login(page, teacherEmail, teacherPassword);

    await openCourse(page);

    await expect(
      page.locator('form[action*="/Test/Start"]')
    ).toHaveCount(0);

    await expect(
      page.getByRole('button', {
        name: 'Приєднатися до курсу'
      })
    ).toHaveCount(0);
  });

});
