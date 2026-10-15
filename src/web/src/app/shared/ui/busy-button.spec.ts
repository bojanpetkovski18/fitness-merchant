import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BusyButton } from './busy-button';

@Component({
  imports: [BusyButton],
  template: `
    <form (submit)="$event.preventDefault(); submits = submits + 1">
      <input id="field" />
      <button type="submit" [appBusy]="busy()" (click)="clicks = clicks + 1">Save</button>
    </form>
  `,
})
class Host {
  readonly busy = signal(false);
  clicks = 0;
  submits = 0;
}

describe('BusyButton', () => {
  it('keeps focus while busy, tells assistive tech, and ignores presses until done', async () => {
    const fixture = TestBed.createComponent(Host);
    const element = fixture.nativeElement as HTMLElement;
    document.body.appendChild(element);
    await fixture.whenStable();
    const button = element.querySelector('button')!;
    button.focus();

    fixture.componentInstance.busy.set(true);
    await fixture.whenStable();

    expect(button.disabled).toBe(false);
    expect(document.activeElement).toBe(button);
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.getAttribute('aria-busy')).toBe('true');
    button.click();
    expect([fixture.componentInstance.clicks, fixture.componentInstance.submits]).toEqual([0, 0]);

    fixture.componentInstance.busy.set(false);
    await fixture.whenStable();
    expect(button.hasAttribute('aria-disabled')).toBe(false);
    button.click();
    expect([fixture.componentInstance.clicks, fixture.componentInstance.submits]).toEqual([1, 1]);
    element.remove();
  });
});
