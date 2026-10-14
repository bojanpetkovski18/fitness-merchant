import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { SessionStore } from '../../core/auth/session.store';
import { AccountLayout } from './account-layout';
import { PasswordPage } from './password-page';
import { ProfilePage } from './profile-page';

const user = { id: '1', email: 'mila@test.example', displayName: 'Mila', roles: ['Customer'] };

/** Lets the component's awaited request finish (a macrotask), then renders. */
async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
}

@Component({ template: '<h1>Section</h1>' })
class Section {}

describe('Account pages', () => {
  let backend: HttpTestingController;

  async function render<T>(component: Type<T>): Promise<{
    fixture: ComponentFixture<T>;
    element: HTMLElement;
    type: (id: string, value: string) => void;
    submit: () => Promise<void>;
  }> {
    const fixture = TestBed.createComponent(component);
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    await fixture.whenStable();
    const field = (id: string) => element.querySelector<HTMLInputElement>(`#${id}`)!;
    return {
      fixture,
      element,
      type: (id, value) => {
        field(id).value = value;
        field(id).dispatchEvent(new Event('input'));
      },
      submit: async () => {
        element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
        await fixture.whenStable();
      },
    };
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          {
            path: 'account',
            component: AccountLayout,
            children: [
              { path: '', component: Section },
              { path: 'password', component: Section },
              { path: 'orders', component: Section },
              { path: 'orders/:number', component: Section },
            ],
          },
        ]),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    const loading = TestBed.inject(SessionStore).load();
    backend.expectOne('/api/auth/me').flush({ user });
    await loading;
  });

  afterEach(() => {
    backend.verify();
    document.body.innerHTML = '';
  });

  describe('layout', () => {
    it('is a labelled navigation landmark that marks the current section', async () => {
      const harness = await RouterTestingHarness.create('/account/password');
      const nav = harness.routeNativeElement!.querySelector('nav[aria-label="Account"]')!;
      const current = () =>
        Array.from(nav.querySelectorAll('a[aria-current]')).map((a) => [
          a.textContent?.trim(),
          a.getAttribute('aria-current'),
        ]);

      expect(current()).toEqual([['Password', 'page']]);

      await harness.navigateByUrl('/account');
      expect(current()).toEqual([['Profile', 'page']]);

      // An order's page is inside the Orders section, but it is not the Orders page itself.
      await harness.navigateByUrl('/account/orders/FM-000123');
      expect(current()).toEqual([['Orders', 'true']]);
    });
  });

  describe('ProfilePage', () => {
    it('starts with the current name, saves the trimmed value and updates the session', async () => {
      const { element, type, submit, fixture } = await render(ProfilePage);
      expect(element.querySelector<HTMLInputElement>('#displayName')!.value).toBe('Mila');

      type('displayName', '  Mila Novak  ');
      await submit();
      const request = backend.expectOne('/api/account/profile');
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ displayName: 'Mila Novak' });
      request.flush({ user: { ...user, displayName: 'Mila Novak' } });
      await settle(fixture);

      expect(TestBed.inject(SessionStore).user()?.displayName).toBe('Mila Novak');
      expect(element.querySelector('[role="status"]')?.textContent).toContain(
        'Your profile was saved.',
      );
    });

    it('rejects a name that is too short after trimming, without a request', async () => {
      const { element, type, submit } = await render(ProfilePage);

      type('displayName', '  a  ');
      await submit();

      const field = element.querySelector<HTMLInputElement>('#displayName')!;
      expect(field.getAttribute('aria-invalid')).toBe('true');
      expect(field.getAttribute('aria-describedby')).toBe('displayName-hint displayName-error');
      expect(element.querySelector('#displayName-error')?.textContent).toContain(
        'at least 2 characters',
      );
      expect(document.activeElement).toBe(field);
      backend.expectNone('/api/account/profile');
    });

    it('shows the server validation message on the field', async () => {
      const { element, type, submit, fixture } = await render(ProfilePage);

      type('displayName', 'Mila N');
      await submit();
      backend
        .expectOne('/api/account/profile')
        .flush(
          { status: 400, title: 'Validation', errors: { displayName: ['Server says no.'] } },
          { status: 400, statusText: 'Bad Request' },
        );
      await settle(fixture);

      expect(element.querySelector('#displayName-error')?.textContent).toContain('Server says no.');
      expect(document.activeElement?.id).toBe('displayName');
    });
  });

  describe('PasswordPage', () => {
    it('sends both passwords, clears the form and confirms other devices were signed out', async () => {
      const { element, type, submit, fixture } = await render(PasswordPage);

      type('currentPassword', 'Correct-Horse-9');
      type('newPassword', 'Brand-New-Pass-7');
      await submit();
      backend
        .expectOne((r) => r.url === '/api/account/password' && r.method === 'POST')
        .flush(null, { status: 204, statusText: 'No Content' });
      await settle(fixture);

      expect(element.querySelector('[role="status"]')?.textContent).toContain(
        'Other devices have been signed out.',
      );
      expect(element.querySelector<HTMLInputElement>('#currentPassword')!.value).toBe('');
      expect(element.querySelector<HTMLInputElement>('#newPassword')!.value).toBe('');
      expect(element.querySelector('[aria-invalid="true"]')).toBeNull();
    });

    it('marks a wrong current password, clears only that field and focuses it', async () => {
      const { element, type, submit, fixture } = await render(PasswordPage);

      type('currentPassword', 'Wrong-Password-1');
      type('newPassword', 'Brand-New-Pass-7');
      await submit();
      backend.expectOne('/api/account/password').flush(
        {
          status: 400,
          title: 'One or more validation errors occurred.',
          errors: { currentPassword: ['Your current password is not correct.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
      await settle(fixture);

      const current = element.querySelector<HTMLInputElement>('#currentPassword')!;
      expect(element.querySelector('#currentPassword-error')?.textContent).toContain(
        'Your current password is not correct.',
      );
      expect(current.value).toBe('');
      expect(element.querySelector<HTMLInputElement>('#newPassword')!.value).toBe(
        'Brand-New-Pass-7',
      );
      expect(document.activeElement).toBe(current);
      expect(element.querySelector('[role="alert"]')?.textContent).toContain(
        'Please fix the highlighted fields.',
      );
    });

    it('requires both fields and a 10-character new password before sending', async () => {
      const { element, type, submit } = await render(PasswordPage);

      type('newPassword', 'short');
      await submit();

      expect(element.querySelector('#currentPassword-error')?.textContent).toContain(
        'Current password is required.',
      );
      expect(element.querySelector('#newPassword-error')?.textContent).toContain(
        'at least 10 characters',
      );
      expect(element.querySelector('#newPassword')!.getAttribute('aria-describedby')).toBe(
        'newPassword-hint newPassword-error',
      );
      backend.expectNone('/api/account/password');
    });

    it('shows a rate-limit answer as an alert', async () => {
      const { element, type, submit, fixture } = await render(PasswordPage);

      type('currentPassword', 'Correct-Horse-9');
      type('newPassword', 'Brand-New-Pass-7');
      await submit();
      backend
        .expectOne('/api/account/password')
        .flush(
          { status: 429, title: 'Too many requests.' },
          { status: 429, statusText: 'Too Many Requests' },
        );
      await settle(fixture);

      expect(element.querySelector('[role="alert"]')?.textContent).toContain('Too many requests.');
    });
  });
});
