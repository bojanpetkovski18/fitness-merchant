import { Component, ElementRef, Injector, afterNextRender, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SessionStore } from '../../core/auth/session.store';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { fieldError } from '../../shared/forms/field-error';

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule],
  template: `
    <h1 class="fm-display text-3xl md:text-4xl">Profile</h1>
    <p class="mt-2 text-muted">
      Signed in as <strong class="text-fg">{{ session.user()?.email }}</strong>
    </p>

    <div aria-live="assertive">
      @if (problem(); as problem) {
        @if (problem.status !== 400) {
          <p
            role="alert"
            class="mt-6 rounded-md border border-danger/50 bg-danger/10 px-4 py-3 text-sm font-semibold text-danger"
          >
            {{ problem.title }}
          </p>
        }
      }
    </div>
    <div aria-live="polite">
      @if (saved()) {
        <p
          role="status"
          class="mt-6 rounded-md border border-success/40 bg-success/10 px-4 py-3 text-sm font-semibold text-success"
        >
          Your profile was saved.
        </p>
      }
    </div>

    <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="mt-6 max-w-md space-y-5">
      @let nameError = error();
      <div>
        <label for="displayName" class="block text-sm font-semibold">Display name</label>
        <p id="displayName-hint" class="mt-0.5 text-sm text-muted">
          Shown in the header and on your reviews. 2 to 50 characters.
        </p>
        <input
          id="displayName"
          type="text"
          formControlName="displayName"
          autocomplete="nickname"
          [attr.aria-invalid]="nameError ? 'true' : null"
          [attr.aria-describedby]="
            nameError ? 'displayName-hint displayName-error' : 'displayName-hint'
          "
          class="mt-1 block w-full rounded-md border border-border bg-surface px-3 py-2.5 text-fg aria-invalid:border-danger"
        />
        @if (nameError) {
          <p id="displayName-error" class="mt-1 text-sm text-danger">{{ nameError }}</p>
        }
      </div>

      <button
        type="submit"
        [disabled]="pending()"
        class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong disabled:opacity-60"
      >
        {{ pending() ? 'Saving…' : 'Save profile' }}
      </button>
    </form>
  `,
})
export class ProfilePage {
  protected readonly session = inject(SessionStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    displayName: [
      this.session.user()?.displayName ?? '',
      [Validators.required, Validators.minLength(2), Validators.maxLength(50)],
    ],
  });
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly saved = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);

  protected error(): string | null {
    return fieldError(
      this.form.controls.displayName,
      'Display name',
      this.problem()?.fieldErrors['displayName'],
      this.submitted(),
    );
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.problem.set(null);
    this.saved.set(false);
    // The server trims too and stays authoritative; trimming here makes "  a " fail fast with the same rule.
    const displayName = this.form.controls.displayName.value.trim();
    this.form.controls.displayName.setValue(displayName);
    if (this.form.invalid) {
      this.focusInvalid();
      return;
    }

    this.pending.set(true);
    try {
      await this.session.updateProfile(displayName);
      this.saved.set(true);
      this.submitted.set(false);
    } catch (error) {
      this.problem.set(toApiProblem(error));
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
