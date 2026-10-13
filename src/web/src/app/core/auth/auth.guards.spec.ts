import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { adminGuard, authGuard, guestGuard } from './auth.guards';
import { SessionStore, SessionUser } from './session.store';

@Component({ template: 'page' })
class Page {}

const customer: SessionUser = {
  id: '1',
  email: 'c@test.example',
  displayName: 'C',
  roles: ['Customer'],
};
const admin: SessionUser = { ...customer, roles: ['Admin'] };

describe('auth guards', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: '', component: Page },
          { path: 'sign-in', component: Page, canActivate: [guestGuard] },
          { path: 'account', component: Page, canActivate: [authGuard] },
          { path: 'admin', component: Page, canActivate: [adminGuard] },
        ]),
      ],
    });
  });

  async function signInAs(user: SessionUser | null): Promise<void> {
    const loading = TestBed.inject(SessionStore).load();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/me').flush({ user });
    await loading;
  }

  async function navigate(url: string): Promise<string> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return TestBed.inject(Router).url;
  }

  it('sends guests to sign-in with the return URL', async () => {
    await signInAs(null);

    expect(await navigate('/account?tab=orders')).toBe(
      '/sign-in?returnUrl=%2Faccount%3Ftab%3Dorders',
    );
  });

  it('lets signed-in users through', async () => {
    await signInAs(customer);

    expect(await navigate('/account')).toBe('/account');
  });

  it('keeps customers out of admin', async () => {
    await signInAs(customer);

    expect(await navigate('/admin')).toBe('/');
  });

  it('lets admins into admin', async () => {
    await signInAs(admin);

    expect(await navigate('/admin')).toBe('/admin');
  });

  it('sends signed-in users away from the sign-in page', async () => {
    await signInAs(customer);

    expect(await navigate('/sign-in')).toBe('/');
  });
});
