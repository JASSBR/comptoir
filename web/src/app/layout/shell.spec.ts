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

  it('shows the legacy user with their job, and links back to the 2014 screens', () => {
    const element = render({ login: 'sophie', role: 'commercial', displayName: 'Sophie Moreau' });
    expect(element.querySelector('.user strong')!.textContent).toBe('Sophie Moreau');
    expect(element.querySelector('.user small')!.textContent).toBe('Commerciale');
    expect(element.querySelector('a.legacy')!.getAttribute('href')).toBe('/');
    expect(element.querySelectorAll('.tabs a')).toHaveLength(5);
  });

  it('offers to sign in on the new screen when there is no session', () => {
    const element = render(null);
    expect(element.querySelector<HTMLAnchorElement>('a.primary')!.getAttribute('href')).toBe(
      '/connexion',
    );
  });
});
