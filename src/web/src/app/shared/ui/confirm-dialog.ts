import { DIALOG_DATA, Dialog, DialogRef } from '@angular/cdk/dialog';
import { Component, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ConfirmOptions {
  readonly title: string;
  readonly message: string;
  readonly confirmLabel: string;
}

/** An accessible yes/no dialog (CDK: focus trap, Escape cancels, focus returns to the trigger). */
@Component({
  selector: 'app-confirm-dialog',
  template: `
    <div
      class="w-[min(90vw,26rem)] rounded-xl border border-border bg-surface p-6 text-fg shadow-xl"
    >
      <h2 id="confirm-title" class="fm-display text-xl">{{ data.title }}</h2>
      <p id="confirm-message" class="mt-2 text-muted">{{ data.message }}</p>
      <div class="mt-6 flex justify-end gap-3">
        <button
          type="button"
          class="rounded-md border border-border px-4 py-2 font-bold hover:bg-surface-2"
          (click)="ref.close(false)"
        >
          Cancel
        </button>
        <button
          type="button"
          class="rounded-md bg-danger px-4 py-2 font-bold text-bg"
          (click)="ref.close(true)"
        >
          {{ data.confirmLabel }}
        </button>
      </div>
    </div>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmOptions>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
}

export async function confirmAction(dialog: Dialog, options: ConfirmOptions): Promise<boolean> {
  const ref = dialog.open<boolean>(ConfirmDialog, {
    data: options,
    role: 'alertdialog',
    ariaModal: true,
    ariaLabelledBy: 'confirm-title',
    ariaDescribedBy: 'confirm-message',
    // Start on Cancel: the safe choice for a destructive action.
    autoFocus: 'first-tabbable',
  });
  return (await firstValueFrom(ref.closed)) === true;
}
