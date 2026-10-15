import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BusyButton } from '../../shared/ui/busy-button';
import { safeReturnUrl } from '../../core/auth/return-url';
import { SessionStore } from '../../core/auth/session.store';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { fieldError } from '../../shared/forms/field-error';

@Component({
  selector: 'app-sign-in-page',
  imports: [ReactiveFormsModule, RouterLink, BusyButton],
  templateUrl: './sign-in-page.html',
})
export class SignInPage {
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** From the query string (withComponentInputBinding). */
  readonly returnUrl = input<string>();

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required]],
  });
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly problem = signal<ApiProblem | null>(null);

  protected error(field: 'email' | 'password', label: string): string | null {
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
    if (this.form.invalid) {
      this.focusFirstInvalid();
      return;
    }

    this.pending.set(true);
    try {
      const { email, password } = this.form.getRawValue();
      await this.session.signIn(email, password);
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      this.problem.set(toApiProblem(error));
      this.form.controls.password.reset();
    } finally {
      this.pending.set(false);
    }
  }

  /** Error attributes appear on the next render, so focus moves after it. */
  private focusFirstInvalid(): void {
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
      { injector: this.injector },
    );
  }
}
