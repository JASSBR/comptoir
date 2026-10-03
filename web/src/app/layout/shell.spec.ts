import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Session } from '../core/session';
import { Shell } from './shell';

describe('Shell', () => {
  function render(user: { login: string; role: string; displayName: string } | null) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: Session, useValue: { user: () => user, checked: () => true } },
      ],
    });
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the legacy user, and links back to the 2014 screens', () => {
    const element = render({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });
    expect(element.querySelector('.who strong')!.textContent).toBe('Sophie Moreau');
    expect(element.querySelector('a[href="/"]')!.textContent).toContain('Interface historique');
  });

  it('offers to sign in on the legacy page when there is no session', () => {
    const element = render(null);
    expect(element.querySelector<HTMLAnchorElement>('a.primary')!.getAttribute('href')).toBe(
      '/Account/Login?ReturnUrl=%2Fapp%2Fdevis',
    );
  });
});
