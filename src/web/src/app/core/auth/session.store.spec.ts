import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionStore, SessionUser } from './session.store';

const admin: SessionUser = {
  id: '1',
  email: 'admin@test.example',
  displayName: 'Ada Admin',
  roles: ['Admin'],
};

describe('SessionStore', () => {
  let store: SessionStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(SessionStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the signed-in user and derives roles', async () => {
    const loading = store.load();
    http.expectOne('/api/auth/me').flush({ user: admin });
    await loading;

    expect(store.isSignedIn()).toBe(true);
    expect(store.isAdmin()).toBe(true);
    expect(store.user()?.displayName).toBe('Ada Admin');
  });

  it('starts as a guest when the API is unreachable instead of failing app start', async () => {
    const loading = store.load();
    http.expectOne('/api/auth/me').error(new ProgressEvent('error'));
    await loading;

    expect(store.isSignedIn()).toBe(false);
  });

  it('sign-in stores the user; a failed sign-in throws and stays signed out', async () => {
    const failing = store.signIn('a@test.example', 'wrong');
    http
      .expectOne('/api/auth/sign-in')
      .flush({ title: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });
    await expect(failing).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(store.isSignedIn()).toBe(false);

    const signingIn = store.signIn('admin@test.example', 'right');
    const request = http.expectOne('/api/auth/sign-in');
    expect(request.request.body).toEqual({ email: 'admin@test.example', password: 'right' });
    request.flush({ user: admin });
    await signingIn;
    expect(store.isSignedIn()).toBe(true);
  });

  it('sign-out clears the user even if the request fails', async () => {
    const loading = store.load();
    http.expectOne('/api/auth/me').flush({ user: admin });
    await loading;

    const signingOut = store.signOut();
    http.expectOne('/api/auth/sign-out').error(new ProgressEvent('error'));
    await expect(signingOut).rejects.toBeDefined();

    expect(store.isSignedIn()).toBe(false);
  });
});
