import { Component, ElementRef, Injector, afterNextRender, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BusyButton } from '../../shared/ui/busy-button';
import { SessionStore } from '../../core/auth/session.store';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { fieldError } from '../../shared/forms/field-error';

type Field = 'currentPassword' | 'newPassword';

@Component({
  selector: 'app-password-page',
  imports: [ReactiveFormsModule, BusyButton],
  template: `
    <h1 class="fm-display text-3xl md:text-4xl">Password</h1>
    <p class="mt-2 text-muted">
      Changing your password signs you out on every other device and browser.
    </p>

    <div aria-live="assertive">
      @if (problem(); as problem) {
        <p
          role="alert"
          class="mt-6 rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm font-semibold text-danger"
        >
          {{ problem.status === 400 ? 'Please fix the highlighted fields.' : problem.title }}
        </p>
      }
    </div>
    <div aria-live="polite">
      @if (changed()) {
        <p
          role="status"
          class="mt-6 rounded-md border border-success/40 bg-success/10 px-4 py-3 text-sm font-semibold text-success"
        >
          Your password was changed. Other devices have been signed out.
        </p>
      }
    </div>

    <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="mt-6 max-w-md space-y-5">
      @let currentError = error('currentPassword', 'Current password');
      <div>
        <label for="currentPassword" class="block text-sm font-semibold">Current password</label>
        <input
          id="currentPassword"
          [type]="showPasswords() ? 'text' : 'password'"
          formControlName="currentPassword"
          autocomplete="current-password"
          [attr.aria-invalid]="currentError ? 'true' : null"
          [attr.aria-describedby]="currentError ? 'currentPassword-error' : null"
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2.5 text-fg aria-invalid:border-danger"
        />
        @if (currentError) {
          <p id="currentPassword-error" class="mt-1 text-sm text-danger">{{ currentError }}</p>
        }
      </div>

      @let newError = error('newPassword', 'New password');
      <div>
        <label for="newPassword" class="block text-sm font-semibold">New password</label>
        <p id="newPassword-hint" class="mt-0.5 text-sm text-muted">
          At least 10 characters, with upper- and lowercase letters, a number and a symbol.
        </p>
        <input
          id="newPassword"
          [type]="showPasswords() ? 'text' : 'password'"
          formControlName="newPassword"
          autocomplete="new-password"
          [attr.aria-invalid]="newError ? 'true' : null"
          [attr.aria-describedby]="
            newError ? 'newPassword-hint newPassword-error' : 'newPassword-hint'
          "
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2.5 text-fg aria-invalid:border-danger"
        />
        @if (newError) {
          <p id="newPassword-error" class="mt-1 text-sm text-danger">{{ newError }}</p>
        }
      </div>

      <label class="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          class="size-4 accent-accent"
          [checked]="showPasswords()"
          (change)="showPasswords.set(!showPasswords())"
        />
        Show passwords
      </label>

      <button
        type="submit"
        [appBusy]="pending()"
        class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong aria-disabled:opacity-60"
      >
        {{ pending() ? 'Changing password…' : 'Change password' }}
      </button>
    </form>
  `,
})
export class PasswordPage {
  private readonly session = inject(SessionStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  // Client rules mirror the server's basics; Identity's password rules have the final say.
  protected readonly form = inject(FormBuilder).nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    newPassword: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(128)]],
  });
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly changed = signal(false);
  protected readonly showPasswords = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);

  protected error(field: Field, label: string): string | null {
    return fieldError(
      this.form.controls[field],
      label,
      this.problem()?.fieldErrors[field],
      this.submitted(),
    );
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.problem.set(null);
    this.changed.set(false);
    if (this.form.invalid) {
      this.focusInvalid();
      return;
    }

    this.pending.set(true);
    const { currentPassword, newPassword } = this.form.getRawValue();
    try {
      await this.session.changePassword(currentPassword, newPassword);
      // Passwords never linger in the form after use.
      this.form.reset();
      this.submitted.set(false);
      this.changed.set(true);
    } catch (error) {
      const problem = toApiProblem(error);
      this.problem.set(problem);
      if (problem.fieldErrors['currentPassword']) {
        this.form.controls.currentPassword.reset();
      }
      this.focusInvalid();
    } finally {
      this.pending.set(false);
    }
  }

  private focusInvalid(): void {
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
      { injector: this.injector },
    );
  }
}
