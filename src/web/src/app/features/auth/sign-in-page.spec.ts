import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SignInPage } from './sign-in-page';

@Component({ template: '' })
class Blank {}

describe('SignInPage', () => {
  let fixture: ComponentFixture<SignInPage>;
  let element: HTMLElement;
  let backend: HttpTestingController;

  const input = (id: string) => element.querySelector<HTMLInputElement>(`#${id}`)!;
  const type = (id: string, value: string) => {
    input(id).value = value;
    input(id).dispatchEvent(new Event('input'));
  };
  const submit = () => element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [SignInPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: Blank }]),
      ],
    });
    fixture = TestBed.createComponent(SignInPage);
    fixture.componentRef.setInput('returnUrl', '/account');
    element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    backend = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => element.remove());

  it('shows client errors linked to their fields and sends nothing when empty', async () => {
    submit();
    await fixture.whenStable();

    expect(input('email').getAttribute('aria-invalid')).toBe('true');
    expect(input('email').getAttribute('aria-describedby')).toBe('email-error');
    expect(element.querySelector('#email-error')?.textContent).toContain('Email is required.');
    expect(element.querySelector('#password-error')?.textContent).toContain(
      'Password is required.',
    );
    expect(document.activeElement).toBe(input('email'));
    backend.expectNone('/api/auth/sign-in');
  });

  it('announces a server rejection in an alert and clears the password', async () => {
    type('email', 'shopper@test.example');
    type('password', 'Wrong-Password-1');
    submit();
    backend
      .expectOne('/api/auth/sign-in')
      .flush(
        { title: 'Invalid email or password.', status: 401 },
        { status: 401, statusText: 'Unauthorized' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Invalid email or password.',
    );
    expect(input('password').value).toBe('');
  });

  it('navigates to the return URL after signing in', async () => {
    const router = TestBed.inject(Router);
    type('email', 'shopper@test.example');
    type('password', 'Correct-Horse-9');
    submit();
    backend.expectOne('/api/auth/sign-in').flush({
      user: { id: '1', email: 'shopper@test.example', displayName: 'S', roles: ['Customer'] },
    });
    await fixture.whenStable();

    expect(router.url).toBe('/account');
  });

  it('toggles password visibility with an accessible pressed state', async () => {
    const toggle = element.querySelector<HTMLButtonElement>('button[aria-controls="password"]')!;
    expect(input('password').type).toBe('password');

    toggle.click();
    await fixture.whenStable();

    expect(input('password').type).toBe('text');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
  });
});
