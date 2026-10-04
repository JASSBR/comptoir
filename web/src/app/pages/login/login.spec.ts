import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Login } from './login';

describe('Login', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.unstubAllGlobals();
  });

  function render(returnUrl?: string) {
    const fixture = TestBed.createComponent(Login);
    if (returnUrl) fixture.componentRef.setInput('returnUrl', returnUrl);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = element.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const submit = () => element.querySelector('form')!.dispatchEvent(new Event('submit'));
    return { fixture, element, type, submit };
  }

  it('signs in through the facade and goes back to the new screen the user wanted', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { fixture, type, submit } = render('/app/devis');
    type('#login', 'sophie');
    type('#password', 'comptoir-demo');

    submit();
    const request = http.expectOne('/session');
    expect(request.request.body).toEqual({ login: 'sophie', password: 'comptoir-demo' });
    request.flush({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/devis');
  });

  it('goes back to a 2014 screen with a full page load', async () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { assign });
    const { fixture, type, submit } = render('/#!/commandes');
    type('#login', 'sophie');
    type('#password', 'comptoir-demo');

    submit();
    http
      .expectOne('/session')
      .flush({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });
    await fixture.whenStable();

    expect(assign).toHaveBeenCalledWith('/#!/commandes');
  });

  it('never redirects outside the site after signing in', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { fixture, type, submit } = render('//evil.example/phish');
    type('#login', 'sophie');
    type('#password', 'comptoir-demo');

    submit();
    http
      .expectOne('/session')
      .flush({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/migration');
  });

  it('shows the legacy message when the credentials are wrong, and stays here', async () => {
    const { fixture, element, type, submit } = render();
    type('#login', 'sophie');
    type('#password', 'wrong');

    submit();
    http
      .expectOne('/session')
      .flush(
        { message: 'Identifiant ou mot de passe incorrect.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await fixture.whenStable();

    expect(element.querySelector('.error')!.textContent).toContain(
      'Identifiant ou mot de passe incorrect.',
    );
    expect(element.querySelector<HTMLButtonElement>('.submit')!.disabled).toBe(false);
  });

  it('fills a demo account in one click', async () => {
    const { fixture, element } = render();

    element.querySelector<HTMLButtonElement>('[data-account="marc"]')!.click();
    await fixture.whenStable();

    expect(element.querySelector<HTMLInputElement>('#login')!.value).toBe('marc');
    expect(element.querySelector<HTMLInputElement>('#password')!.value).toBe('comptoir-demo');
  });
});
