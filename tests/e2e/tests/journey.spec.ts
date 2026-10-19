import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';

/**
 * SPEC acceptance criterion 9: one journey through the production image, from a guest's search to the admin
 * shipping the order and the customer seeing it. Every visited page also passes axe (no serious or critical
 * violations). Runs against `docker compose up --build`; the demo admin comes from the same .env.
 */
const adminEmail = required('DEMO_ADMIN_EMAIL');
const adminPassword = required('DEMO_ADMIN_PASSWORD');

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Set ${name} in the repo-root .env (the stack seeds the demo admin from it).`);
  }
  return value;
}

async function expectAccessible(page: Page, label: string): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  const blocking = results.violations
    .filter((v) => v.impact === 'serious' || v.impact === 'critical')
    .map((v) => `${v.id} (${v.nodes.length})`);
  expect(blocking, `axe on ${label}`).toEqual([]);
}

test('guest finds a product, registers, checks out; the admin ships it; the customer sees it', async ({
  page,
  browser,
}) => {
  const email = `e2e-${Date.now()}@test.example`;
  const password = 'E2e-Shopper-Pass-1';

  // 1–2. A guest searches the supplements and narrows to one category.
  await page.goto('/supplements');
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Supplements');
  await page.getByLabel('Search supplements').fill('whey');
  await expect(page).toHaveURL(/q=whey/);
  await page.getByRole('checkbox', { name: 'Protein' }).check();
  await expect(page).toHaveURL(/category=protein/);
  const addButtons = page.getByRole('button', { name: /^Add .+ to cart$/ });
  await expect(addButtons.first()).toBeVisible();
  await expectAccessible(page, 'filtered listing');

  // 3. Adds the first result to the (guest) cart.
  const productName = (await addButtons.first().getAttribute('aria-label'))!.replace(/^Add | to cart$/g, '');
  await addButtons.first().click();
  await expect(page.getByRole('link', { name: 'Cart, 1 item' })).toBeVisible();

  // 4. Registers (from the sign-in page, as a guest would).
  await page.getByRole('link', { name: 'Sign in' }).first().click();
  await page.getByRole('link', { name: 'Create an account' }).click();
  await expectAccessible(page, 'register');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Display name').fill('E2E Shopper');
  await page.getByLabel('Password', { exact: true }).fill(password);
  // The guest badge already says "1 item", so it proves nothing: wait for the merge itself before navigating.
  const merged = page.waitForResponse((r) => r.url().endsWith('/api/cart/merge') && r.ok());
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('button', { name: 'Account menu for E2E Shopper' })).toBeVisible();
  await merged;

  // 5. The guest cart is merged into the new account's cart.
  await expect(page.getByRole('link', { name: 'Cart, 1 item' })).toBeVisible();
  await page.goto('/cart');
  await expect(page.getByRole('main')).toContainText(productName);
  await expectAccessible(page, 'cart');

  // 6. Checks out with the fake test card.
  await page.getByRole('link', { name: 'Proceed to checkout' }).click();
  await expect(page.locator('#step-1-heading')).toBeVisible();
  await page.locator('#ship-line1').fill('10 Example Street');
  await page.locator('#ship-city').fill('Leipzig');
  await page.locator('#ship-postalCode').fill('04109');
  await page.locator('#ship-country').selectOption('Germany');
  await page.locator('#ship-phone').fill('+49 000 000 000');
  await expectAccessible(page, 'checkout shipping');
  await page.getByRole('button', { name: 'Continue to payment' }).click();
  await page.locator('#pay-cardNumber').fill('4242 4242 4242 4242');
  await page.locator('#pay-expiry').fill('12/30');
  await page.locator('#pay-cvc').fill('123');
  await page.getByRole('button', { name: 'Review order' }).click();
  await page.getByRole('button', { name: /^Place order/ }).click();

  // 7. Confirmation, then the order in the history.
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Thank you for your order');
  const orderNumber = (await page.locator('main strong.font-mono').first().textContent())!.trim();
  expect(orderNumber).toMatch(/^FM-\d{6}$/);
  await expectAccessible(page, 'confirmation');
  await page.goto('/account/orders');
  const orderRow = page.getByRole('link', { name: orderNumber });
  await expect(orderRow).toBeVisible();
  await expectAccessible(page, 'order history');

  // 8. The admin marks the order as shipped (a separate browser session).
  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await admin.goto('/sign-in');
  await admin.getByLabel('Email').fill(adminEmail);
  await admin.getByLabel('Password', { exact: true }).fill(adminPassword);
  await admin.getByRole('button', { name: 'Sign in' }).click();
  await expect(admin.getByRole('button', { name: /^Account menu for / })).toBeVisible();
  await admin.goto(`/admin/orders?q=${orderNumber}`);
  await admin.getByRole('link', { name: orderNumber }).first().click();
  await expect(admin.getByRole('heading', { level: 1 })).toHaveText(orderNumber);
  await expectAccessible(admin, 'admin order');
  await admin.getByRole('button', { name: 'Mark as shipped' }).click();
  await admin.getByLabel('Note (optional)').fill('Tracking E2E-1');
  await admin.getByRole('dialog').getByRole('button', { name: 'Mark as shipped' }).click();
  await expect(admin.getByRole('button', { name: 'Mark as delivered' })).toBeVisible();
  await adminContext.close();

  // 9. The customer sees Shipped, with the admin's note.
  await orderRow.click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(orderNumber);
  await expect(page.getByRole('main')).toContainText('Shipped');
  await expect(page.getByRole('main')).toContainText('Tracking E2E-1');
  await expectAccessible(page, 'customer order');
});
