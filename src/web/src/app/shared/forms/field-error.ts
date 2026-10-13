import { AbstractControl } from '@angular/forms';

/**
 * The message to show under a field: the server's error for it if any (authoritative), otherwise the first
 * client-side validation error once the user has interacted with the field or tried to submit.
 */
export function fieldError(
  control: AbstractControl,
  label: string,
  serverErrors: readonly string[] | undefined,
  submitted: boolean,
): string | null {
  if (serverErrors && serverErrors.length > 0) {
    return serverErrors[0]!;
  }
  if (!control.errors || !(control.touched || submitted)) {
    return null;
  }
  const errors = control.errors;
  if (errors['required']) {
    return `${label} is required.`;
  }
  if (errors['email']) {
    return 'Enter a valid email address.';
  }
  if (errors['minlength']) {
    const { requiredLength } = errors['minlength'] as { requiredLength: number };
    return `${label} must be at least ${requiredLength} characters.`;
  }
  if (errors['maxlength']) {
    const { requiredLength } = errors['maxlength'] as { requiredLength: number };
    return `${label} must be at most ${requiredLength} characters.`;
  }
  return `${label} is not valid.`;
}
