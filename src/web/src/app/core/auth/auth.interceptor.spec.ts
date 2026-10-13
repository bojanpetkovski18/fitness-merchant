import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { SessionStore } from './session.store';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let session: SessionStore;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    const loading = session.load();
    backend
      .expectOne('/api/auth/me')
      .flush({ user: { id: '1', email: 'a@test.example', displayName: 'A', roles: ['Customer'] } });
    await loading;
  });

  it('forgets the user when a protected call returns 401', async () => {
    const call = firstValueFrom(http.get('/api/orders'));
    backend.expectOne('/api/orders').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toBeDefined();
    expect(session.isSignedIn()).toBe(false);
  });

  it('ignores 401s from the auth endpoints themselves', async () => {
    const call = firstValueFrom(http.post('/api/auth/sign-in', {}));
    backend.expectOne('/api/auth/sign-in').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toBeDefined();
    expect(session.isSignedIn()).toBe(true);
  });
});
