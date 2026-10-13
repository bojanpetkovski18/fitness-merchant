import { HttpErrorResponse } from '@angular/common/http';

/** An RFC 7807 problem from the API, reduced to what forms and error states display. */
export interface ApiProblem {
  readonly status: number;
  readonly title: string;
  /** Server validation errors by field, keys in camelCase ("email", "displayName"). */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
}

interface ProblemBody {
  readonly title?: unknown;
  readonly errors?: unknown;
}

export function toApiProblem(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, title: 'Something went wrong. Please try again.', fieldErrors: {} };
  }
  if (error.status === 0) {
    return {
      status: 0,
      title: "Can't reach the server. Check your connection and try again.",
      fieldErrors: {},
    };
  }

  const body = (
    typeof error.error === 'object' && error.error !== null ? error.error : {}
  ) as ProblemBody;
  const title =
    typeof body.title === 'string' && body.title.length > 0
      ? body.title
      : 'Something went wrong. Please try again.';
  return { status: error.status, title, fieldErrors: fieldErrors(body.errors) };
}

function fieldErrors(errors: unknown): Record<string, readonly string[]> {
  if (typeof errors !== 'object' || errors === null) {
    return {};
  }
  const result: Record<string, readonly string[]> = {};
  for (const [key, value] of Object.entries(errors)) {
    if (Array.isArray(value)) {
      const field = key.length > 0 ? key[0]!.toLowerCase() + key.slice(1) : key;
      result[field] = value.filter((message): message is string => typeof message === 'string');
    }
  }
  return result;
}
