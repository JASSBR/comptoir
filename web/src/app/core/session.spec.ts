import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Session, legacyLoginUrl } from './session';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

describe('Session', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads who is signed in on the legacy application', async () => {
    const session = TestBed.inject(Session);
    const refresh = session.refresh();
    http
      .expectOne('/api/session')
      .flush(JSON.stringify({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' }));
    await refresh;

    expect(session.signedIn()).toBe(true);
    expect(session.user()?.displayName).toBe('Sophie Moreau');
    expect(session.checked()).toBe(true);
  });

  it('treats the legacy login page (a redirect followed by the browser) as signed out', async () => {
    const session = TestBed.inject(Session);
    const refresh = session.refresh();
    http.expectOne('/api/session').flush('<!DOCTYPE html><title>Connexion</title>');
    await refresh;

    expect(session.signedIn()).toBe(false);
    expect(session.checked()).toBe(true);
  });

  it('builds the legacy login URL that comes back here', () => {
    expect(legacyLoginUrl('/app/devis')).toBe('/Account/Login?ReturnUrl=%2Fapp%2Fdevis');
  });

  it('sends the user to the legacy login when the facade answers 401', () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/app/devis', assign });

    TestBed.inject(HttpClient)
      .get('/api/products')
      .subscribe({ error: () => undefined });
    http
      .expectOne('/api/products')
      .flush({ message: 'Session expirée' }, { status: 401, statusText: 'Unauthorized' });

    expect(assign).toHaveBeenCalledWith('/Account/Login?ReturnUrl=%2Fapp%2Fdevis');
    vi.unstubAllGlobals();
  });
});
