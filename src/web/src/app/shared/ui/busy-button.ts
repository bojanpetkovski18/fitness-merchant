import { Directive, ElementRef, inject, input } from '@angular/core';

/**
 * Marks a button busy while its request runs, without the `disabled` attribute (K12). A disabled button cannot
 * keep focus, so a keyboard user who pressed it lands back on the page. This keeps the button focusable, exposes
 * `aria-disabled` and `aria-busy`, and swallows clicks (and so Enter-key form submits) while busy. The listener
 * runs in the capture phase, before the button's own `(click)` handler.
 */
@Directive({
  selector: 'button[appBusy]',
  host: {
    '[attr.aria-disabled]': 'appBusy() ? "true" : null',
    '[attr.aria-busy]': 'appBusy() ? "true" : null',
  },
})
export class BusyButton {
  readonly appBusy = input.required<boolean>();

  constructor() {
    inject<ElementRef<HTMLButtonElement>>(ElementRef).nativeElement.addEventListener(
      'click',
      (event) => {
        if (this.appBusy()) {
          event.preventDefault();
          event.stopImmediatePropagation();
        }
      },
      { capture: true },
    );
  }
}
