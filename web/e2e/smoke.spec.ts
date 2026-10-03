import { expect, test } from '@playwright/test';

test('the 2014 screens and the new ones share one session through the facade', async ({ page }) => {
  const scriptErrors: string[] = [];
  page.on('pageerror', (error) => scriptErrors.push(error.message));

  await page.goto('/Account/Login?ReturnUrl=%2f');
  await page.fill('#login', 'sophie');
  await page.fill('#password', 'comptoir-demo');
  await page.click('button[type=submit]');

  // Legacy AngularJS screens: a broken script reference only shows up in a browser.
  await expect(page.locator('.table-orders tbody tr').first()).toBeVisible();
  await page.locator('.table-orders tbody tr').first().click();
  await expect(page.locator('.totals')).toBeVisible();
  expect(scriptErrors).toEqual([]);

  // New screens, same cookie: no second login.
  await page.goto('/app/migration');
  await expect(page.locator('.who strong')).toHaveText('Sophie Moreau');
  // The order screens above were read in shadow: the comparisons show up.
  await expect(page.locator('[data-route="orders-detail"] td.num').first()).not.toHaveText('—');

  await page.goto('/app/devis');
  const customer = page.locator('[data-field=customer]');
  await expect(customer.locator('option')).not.toHaveCount(1);
  await customer.selectOption({ index: 1 });
  await page.locator('[data-field=product]').selectOption({ index: 1 });
  await expect(page.locator('[data-field=total]')).toContainText('€');
});
