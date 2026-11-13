import { expect, test } from '@playwright/test';

const branch = { id: 'b1', name: 'Downtown Branch', address: '123 Main Street', phoneNumber: '555-0100' };
const service = { id: 's1', name: 'General Consultation', description: '', durationMinutes: 30 };

test.beforeEach(async ({ page }) => {
  await page.route('**/api/branches', route => route.fulfill({ json: [branch] }));
  await page.route('**/api/branches/b1/services', route => route.fulfill({ json: [service] }));
  await page.route('**/api/appointments/available-slots*', route => route.fulfill({ json: ['09:00', '09:30'] }));
});

test('books an appointment and cancels it', async ({ page }) => {
  let bookingBody: any;
  await page.route('**/api/appointments', async route => {
    bookingBody = route.request().postDataJSON();
    await route.fulfill({
      status: 201,
      json: {
        id: 'a1', confirmationCode: 'APT-12345678', status: 'Confirmed',
        startTime: '2030-01-02T07:30:00Z', endTime: '2030-01-02T08:00:00Z'
      }
    });
  });
  await page.route('**/api/appointments/cancel', route => route.fulfill({
    json: {
      id: 'a1', confirmationCode: 'APT-12345678', status: 'Cancelled',
      startTime: '2030-01-02T07:30:00Z', endTime: '2030-01-02T08:00:00Z'
    }
  }));

  await page.goto('/');
  await page.getByLabel('Branch', { exact: false }).first().selectOption('b1');
  await page.getByLabel('Service', { exact: false }).first().selectOption('s1');
  await page.getByLabel('Date').fill('2030-01-02');
  await page.getByRole('button', { name: '09:30' }).click();
  await page.getByLabel('Name').fill('Jane Doe');
  await page.getByLabel('Email').fill('jane@example.com');
  await page.getByLabel('Phone').fill('0821234567');
  await page.getByRole('button', { name: 'Book appointment' }).click();

  await expect(page.getByText('Booking confirmed')).toBeVisible();
  await expect(page.getByText('APT-12345678')).toBeVisible();
  await expect(page.getByText('Wednesday, 2 January 2030, 09:30')).toBeVisible();
  expect(bookingBody.startTime).toBe('2030-01-02T09:30:00+02:00');

  await page.getByRole('button', { name: 'Cancel appointment' }).click();
  await page.getByRole('button', { name: 'Yes, cancel it' }).click();
  await expect(page.getByText('Appointment cancelled')).toBeVisible();
});

test('shows validation messages for a bad email and phone', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Email').fill('not-an-email');
  await page.getByLabel('Phone').fill('12345');
  await page.getByLabel('Name').click();

  await expect(page.getByText('Enter a valid email address')).toBeVisible();
  await expect(page.getByText('exactly 10 digits')).toBeVisible();
});
