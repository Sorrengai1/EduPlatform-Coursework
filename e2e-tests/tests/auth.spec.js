
const { test, expect } = require('@playwright/test');

test.describe('EduPlatform — реєстрація та авторизація', () => {

  test('Форма реєстрації містить необхідні поля', async ({ page }) => {
    await page.goto('/Account/Register');

    await expect(page.getByRole('textbox', {
      name: 'Ім\'я: *'
    })).toBeVisible();

    await expect(page.getByRole('textbox', {
      name: 'Прізвище: *'
    })).toBeVisible();

    await expect(page.getByRole('textbox', {
      name: 'Email: *'
    })).toBeVisible();

    await expect(page.getByRole('textbox', {
      name: 'Пароль: *'
    })).toBeVisible();

    await expect(page.getByRole('textbox', {
      name: 'Підтвердження пароля: *'
    })).toBeVisible();

    await expect(page.getByLabel('Хто ви?')).toBeVisible();

    await expect(page.getByRole('button', {
      name: 'Зареєструватися'
    })).toBeVisible();
  });

  test('Перевірка невідповідності паролів', async ({ page }) => {
    await page.goto('/Account/Register');

    await page.getByRole('textbox', {
      name: 'Ім\'я: *'
    }).fill('Тест');

    await page.getByRole('textbox', {
      name: 'Прізвище: *'
    }).fill('Користувач');

    await page.getByRole('textbox', {
      name: 'Email: *'
    }).fill('test-mismatch@example.com');

    await page.getByRole('textbox', {
      name: 'Пароль: *'
    }).fill('TestPassword123!');

    await page.getByRole('textbox', {
      name: 'Підтвердження пароля: *'
    }).fill('DifferentPassword123!');

    await page.getByLabel('Хто ви?').selectOption('Student');

    await page.getByRole('button', {
      name: 'Зареєструватися'
    }).click();

    await expect(page.getByText(
      'Паролі не збігаються'
    )).toBeVisible();
  });

  test('Перехід між входом і реєстрацією', async ({ page }) => {
    await page.goto('/Account/Login');

    await page.getByRole('link', {
      name: 'Немає акаунта? Зареєструватися'
    }).click();

    await expect(page.getByRole('button', {
      name: 'Зареєструватися'
    })).toBeVisible();

    await page.getByRole('link', {
      name: 'Вже є акаунт? Увійти'
    }).click();

    await expect(page.getByRole('button', {
      name: 'Увійти'
    })).toBeVisible();
  });

  test('Форма входу містить Email і пароль', async ({ page }) => {
    await page.goto('/Account/Login');

    await expect(page.getByRole('textbox', {
      name: 'Email: *'
    })).toBeVisible();

    await expect(page.getByRole('textbox', {
      name: 'Пароль: *'
    })).toBeVisible();

    await expect(page.getByRole('button', {
      name: 'Увійти'
    })).toBeVisible();
  });

});
