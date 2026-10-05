import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Session, loginUrl } from './session';
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

  it('builds the login URL that comes back here', () => {
    expect(loginUrl('/app/devis')).toBe('/app/connexion?returnUrl=%2Fapp%2Fdevis');
  });

  it('signs in through the facade and keeps who is signed in', async () => {
    const session = TestBed.inject(Session);
    const signIn = session.signIn('sophie', 'comptoir-demo');
    const request = http.expectOne('/session');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ login: 'sophie', password: 'comptoir-demo' });
    request.flush({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });
    await signIn;

    expect(session.user()?.displayName).toBe('Sophie Moreau');
  });

  it('does not bounce to the login page when the credentials are wrong', async () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/app/connexion', assign });
    const session = TestBed.inject(Session);

    const signIn = session.signIn('sophie', 'wrong');
    http
      .expectOne('/session')
      .flush(
        { message: 'Identifiant ou mot de passe incorrect.' },
        { status: 401, statusText: 'Unauthorized' },
      );

    await expect(signIn).rejects.toBeTruthy();
    expect(assign).not.toHaveBeenCalled();
    expect(session.signedIn()).toBe(false);
  });

  it('signs out', async () => {
    const session = TestBed.inject(Session);
    session.user.set({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });

    const signOut = session.signOut();
    const request = http.expectOne('/session');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await signOut;

    expect(session.signedIn()).toBe(false);
  });

  it('sends the user to the login screen when the facade answers 401', () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/app/devis', assign });

    TestBed.inject(HttpClient)
      .get('/api/products')
      .subscribe({ error: () => undefined });
    http
      .expectOne('/api/products')
      .flush({ message: 'Session expirée' }, { status: 401, statusText: 'Unauthorized' });

    expect(assign).toHaveBeenCalledWith('/app/connexion?returnUrl=%2Fapp%2Fdevis');
    vi.unstubAllGlobals();
  });

  it('learns it is signed out from a 401 on /api/session, without leaving the page', async () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/app/migration', assign });
    const session = TestBed.inject(Session);

    const refresh = session.refresh();
    http
      .expectOne('/api/session')
      .flush(
        { message: 'Authorization has been denied for this request.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await refresh;

    expect(session.signedIn()).toBe(false);
    expect(assign).not.toHaveBeenCalled();
  });

  it('never redirects to the sign-in screen from the sign-in screen itself', () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/app/connexion', assign });

    TestBed.inject(HttpClient)
      .get('/api/products')
      .subscribe({ error: () => undefined });
    http
      .expectOne('/api/products')
      .flush({ message: 'Session expirée' }, { status: 401, statusText: 'Unauthorized' });

    expect(assign).not.toHaveBeenCalled();
  });
});
