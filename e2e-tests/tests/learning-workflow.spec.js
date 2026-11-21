
const { test, expect } = require('@playwright/test');

const studentEmail = process.env.E2E_STUDENT_EMAIL;
const studentPassword = process.env.E2E_STUDENT_PASSWORD;

test.describe('EduPlatform — навчальні матеріали та модулі курсу', () => {
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

    await page.goto('/Course');

    await expect(page).toHaveURL(/\/Course\/?$/);

    // Обираємо перший доступний курс із каталогу.
    const courseLinks = page.locator(
      'a[href*="/Course/Details/"]'
    );

    await expect(courseLinks.first()).toBeVisible();

    const href = await courseLinks.first().getAttribute('href');

    expect(href).toMatch(/\/Course\/Details\/\d+/);

    await page.goto(href);

    await expect(page).toHaveURL(/\/Course\/Details\/\d+/);

    await expect(
      page.locator('.course-details-page')
    ).toBeVisible();
  });

  test('Сторінка курсу містить назву та основну інформацію', async ({ page }) => {
    const hero = page.locator('.course-hero');

    await expect(hero).toBeVisible();

    await expect(
      hero.locator('h1.course-heading')
    ).not.toBeEmpty();

    await expect(
      hero.locator('.course-pill')
    ).toHaveCount(3);
  });

  test('Сторінка курсу містить розділ навчальних матеріалів', async ({ page }) => {
    const section = page.locator(
      '[aria-labelledby="course-materials-title"]'
    );

    await expect(section).toBeVisible();

    await expect(
      page.locator('#course-materials-title')
    ).toHaveText('Навчальні матеріали');

    const materials = section.locator(
      'article.course-material'
    );

    if (await materials.count() === 0) {
      await expect(section).toContainText(
        'Навчальних матеріалів поки немає.'
      );
    } else {
      await expect(
        materials.first().locator('h3')
      ).not.toBeEmpty();
    }
  });

  test('Навчальні матеріали містять назви', async ({ page }) => {
    const materials = page.locator(
      'article.course-material'
    );

    const count = await materials.count();

    if (count === 0) {
      await expect(
        page.locator(
          '[aria-labelledby="course-materials-title"]'
        )
      ).toContainText(
        'Навчальних матеріалів поки немає.'
      );
      return;
    }

    for (let i = 0; i < count; i++) {
      await expect(
        materials.nth(i).locator('h3')
      ).not.toBeEmpty();
    }
  });

  test('Посилання на файли навчальних матеріалів мають коректні атрибути', async ({ page }) => {
    const downloadLinks = page.locator(
      'article.course-material a.course-btn-outline'
    );

    const count = await downloadLinks.count();

    for (let i = 0; i < count; i++) {
      const link = downloadLinks.nth(i);

      await expect(link).toHaveText(/Завантажити/);

      const href = await link.getAttribute('href');

      expect(href).toBeTruthy();
      expect(href.trim()).not.toBe('#');

      await expect(link).toHaveAttribute(
        'target',
        '_blank'
      );

      await expect(link).toHaveAttribute(
        'rel',
        /noopener/
      );
    }

    // Відсутність файлів не є помилкою:
    // FilePath є необов'язковим полем.
  });

  test('Сторінка курсу містить розділ тестів та завдань', async ({ page }) => {
    const section = page.locator(
      '[aria-labelledby="course-tests-title"]'
    );

    await expect(section).toBeVisible();

    await expect(
      page.locator('#course-tests-title')
    ).toHaveText('Тести та завдання');

    const modules = section.locator(
      'article.course-test'
    );

    if (await modules.count() === 0) {
      await expect(section).toContainText(
        'Тестів поки немає.'
      );
    } else {
      await expect(
        modules.first().locator('h3')
      ).not.toBeEmpty();
    }
  });

  test('Модулі курсу відображають назву та кількість питань', async ({ page }) => {
    const modules = page.locator(
      'article.course-test'
    );

    const count = await modules.count();

    if (count === 0) {
      await expect(
        page.locator(
          '[aria-labelledby="course-tests-title"]'
        )
      ).toContainText('Тестів поки немає.');
      return;
    }

    for (let i = 0; i < count; i++) {
      const module = modules.nth(i);

      await expect(
        module.locator('h3')
      ).not.toBeEmpty();

      await expect(module).toContainText(
        /Питань:\s*\d+/
      );
    }
  });

  test('Сторінка курсу відображає інформацію про викладача', async ({ page }) => {
    const sidebar = page.locator('.course-sidebar');

    await expect(sidebar).toBeVisible();

    await expect(sidebar).toContainText(
      'Автор курсу'
    );

    await expect(sidebar).toContainText(
      'Модулів'
    );

    await expect(sidebar).toContainText(
      'Тривалість'
    );

    await expect(sidebar).toContainText(
      'Рівень'
    );
  });

  test('Студент може повернутися до каталогу курсів', async ({ page }) => {
    await page.getByRole('link', {
      name: 'Назад до каталогу'
    }).click();

    await expect(page).toHaveURL(
      /\/Course\/?$/
    );

    await expect(
      page.locator('main').first()
    ).toBeVisible();
  });
});
