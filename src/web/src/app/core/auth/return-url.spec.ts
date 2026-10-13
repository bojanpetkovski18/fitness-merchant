import { HttpErrorResponse } from '@angular/common/http';
import { toApiProblem } from '../http/api-problem';
import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it.each([
    ['/account?tab=orders', '/account?tab=orders'],
    ['/checkout', '/checkout'],
    [undefined, '/'],
    ['', '/'],
    ['https://evil.example', '/'],
    ['//evil.example', '/'],
    ['/\\evil.example', '/'],
    ['javascript:alert(1)', '/'],
    ['/sign-in?returnUrl=/x', '/'],
    ['/register', '/'],
  ])('%s -> %s', (input, expected) => {
    expect(safeReturnUrl(input)).toBe(expected);
  });
});

describe('toApiProblem', () => {
  it('reads the title and camel-cases field error keys', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'One or more validation errors occurred.',
        errors: { Email: ['Bad email.'], password: ['Too short.'] },
      },
    });

    expect(toApiProblem(error)).toEqual({
      status: 400,
      title: 'One or more validation errors occurred.',
      fieldErrors: { email: ['Bad email.'], password: ['Too short.'] },
    });
  });

  it('explains a network failure', () => {
    expect(toApiProblem(new HttpErrorResponse({ status: 0 })).title).toContain(
      "Can't reach the server",
    );
  });

  it('falls back to a generic message for unknown errors', () => {
    expect(toApiProblem(new Error('boom')).title).toBe('Something went wrong. Please try again.');
  });
});
