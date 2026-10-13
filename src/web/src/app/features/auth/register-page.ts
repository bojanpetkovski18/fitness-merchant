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
import { safeReturnUrl } from '../../core/auth/return-url';
import { SessionStore } from '../../core/auth/session.store';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { fieldError } from '../../shared/forms/field-error';

type Field = 'email' | 'displayName' | 'password';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register-page.html',
})
export class RegisterPage {
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly returnUrl = input<string>();

  // Client rules mirror the server's basics; the server (Identity password rules) has the final say.
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(128)]],
  });
  protected readonly submitted = signal(false);
  protected readonly pending = signal(false);
  protected readonly showPassword = signal(false);
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
    if (this.form.invalid) {
      this.focusFirstInvalid();
      return;
    }

    this.pending.set(true);
    try {
      await this.session.register(this.form.getRawValue());
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      this.problem.set(toApiProblem(error));
      this.focusFirstInvalid();
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
