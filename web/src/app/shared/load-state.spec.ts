import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Loadable, LoadState } from './load-state';

function fakeResource() {
  const error = signal<unknown>(undefined);
  const loading = signal(true);
  const value = signal(false);
  const reload = vi.fn(() => true);
  const resource: Loadable = { error, isLoading: loading, hasValue: value, reload };
  return { resource, error, loading, value, reload };
}

describe('LoadState', () => {
  beforeEach(() => vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] }));
  afterEach(() => vi.useRealTimers());

  function render(resource: Loadable) {
    const fixture = TestBed.createComponent(LoadState);
    fixture.componentRef.setInput('resource', resource);
    fixture.componentRef.setInput('rows', 3);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows placeholder lines while the first answer is awaited', () => {
    const { element } = render(fakeResource().resource);
    expect(element.querySelectorAll('.skeleton')).toHaveLength(3);
  });

  it('explains a failure, retries by itself, and lets the user retry at once', () => {
    const fake = fakeResource();
    const { element, fixture } = render(fake.resource);

    fake.loading.set(false);
    fake.error.set(new Error('503'));
    fixture.detectChanges();
    TestBed.tick();
    expect(element.querySelector('[role=alert]')!.textContent).toContain('se mettent en veille');

    vi.advanceTimersByTime(6_000);
    expect(fake.reload).toHaveBeenCalledTimes(1);

    element.querySelector<HTMLButtonElement>('button')!.click();
    expect(fake.reload).toHaveBeenCalledTimes(2);
  });

  it('stops retrying once the data arrives', () => {
    const fake = fakeResource();
    const { fixture } = render(fake.resource);
    fake.error.set(new Error('503'));
    fixture.detectChanges();
    TestBed.tick();

    fake.error.set(undefined);
    fake.value.set(true);
    fixture.detectChanges();
    TestBed.tick();
    vi.advanceTimersByTime(30_000);

    expect(fake.reload).not.toHaveBeenCalled();
  });
});
