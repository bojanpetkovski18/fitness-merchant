/**
 * Only same-site paths are allowed after sign-in, so a crafted link like
 * /sign-in?returnUrl=https://evil.example cannot redirect users off-site (open redirect).
 */
export function safeReturnUrl(raw: string | null | undefined): string {
  if (!raw || !raw.startsWith('/') || raw.startsWith('//') || raw.startsWith('/\\')) {
    return '/';
  }
  if (raw.startsWith('/sign-in') || raw.startsWith('/register')) {
    return '/';
  }
  return raw;
}
