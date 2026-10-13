import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-3xl px-4 py-24 text-center">
      <p class="fm-display text-7xl text-accent" aria-hidden="true">404</p>
      <h1 class="fm-display mt-4 text-3xl md:text-4xl">Page not found</h1>
      <p class="mt-4 text-muted">The page you are looking for does not exist or has moved.</p>
      <div class="mt-8 flex flex-wrap justify-center gap-3">
        <a
          routerLink="/"
          class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong"
          >Go to home</a
        >
        <a
          routerLink="/supplements"
          class="rounded-md border border-border px-5 py-3 font-bold text-fg hover:bg-surface-2"
          >Browse supplements</a
        >
      </div>
    </section>
  `,
})
export class NotFoundPage {}
