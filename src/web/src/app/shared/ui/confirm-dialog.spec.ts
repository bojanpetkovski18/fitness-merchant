import { InteractivityChecker } from '@angular/cdk/a11y';
import { Dialog } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { confirmAction } from './confirm-dialog';

describe('confirmAction', () => {
  let trigger: HTMLButtonElement;

  beforeEach(() => {
    // jsdom has no layout, so CDK would think nothing is visible and focus the container instead.
    TestBed.configureTestingModule({
      providers: [
        {
          provide: InteractivityChecker,
          useValue: {
            isDisabled: (el: Element) => (el as HTMLButtonElement).disabled === true,
            isVisible: () => true,
            isFocusable: (el: HTMLElement) => el.tabIndex >= 0,
            isTabbable: (el: HTMLElement) => el.tabIndex >= 0,
          },
        },
      ],
    });
    trigger = document.createElement('button');
    trigger.textContent = 'Cancel order';
    document.body.appendChild(trigger);
    trigger.focus();
  });

  afterEach(() => {
    TestBed.inject(Dialog).closeAll();
    document.body.innerHTML = '';
  });

  const open = (dismissLabel?: string) =>
    confirmAction(TestBed.inject(Dialog), {
      title: 'Cancel this order?',
      message: 'The items go back on sale.',
      confirmLabel: 'Cancel order',
      ...(dismissLabel ? { dismissLabel } : {}),
    });
  const settle = () => new Promise((resolve) => setTimeout(resolve));
  const dialog = () => document.querySelector<HTMLElement>('[role="alertdialog"]')!;
  const button = (label: string) =>
    Array.from(dialog().querySelectorAll('button')).find((b) => b.textContent?.trim() === label)!;

  it('is a labelled modal alert dialog that starts on the safe choice', async () => {
    void open('Keep order');
    await settle();

    expect(dialog().getAttribute('aria-modal')).toBe('true');
    expect(document.getElementById(dialog().getAttribute('aria-labelledby')!)?.textContent).toBe(
      'Cancel this order?',
    );
    expect(
      document.getElementById(dialog().getAttribute('aria-describedby')!)?.textContent,
    ).toContain('go back on sale');
    expect(document.activeElement).toBe(button('Keep order'));
  });

  it('resolves true only for the confirm button and returns focus to the trigger', async () => {
    const result = open('Keep order');
    await settle();

    button('Cancel order').click();

    expect(await result).toBe(true);
    await settle();
    expect(document.activeElement).toBe(trigger);
  });

  it('resolves false on Escape and returns focus to the trigger', async () => {
    const result = open();
    await settle();
    expect(button('Cancel')).toBeDefined(); // default dismiss label

    document.activeElement!.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }),
    );

    expect(await result).toBe(false);
    await settle();
    expect(document.activeElement).toBe(trigger);
  });
});
