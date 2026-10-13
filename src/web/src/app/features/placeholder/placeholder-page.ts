import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Stand-in for screens built in later milestones. Inputs come from route data
 * (withComponentInputBinding), so each route says what it will become and when.
 */
@Component({
  selector: 'app-placeholder-page',
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-3xl px-4 py-16">
      <p class="text-sm font-semibold uppercase tracking-widest text-accent">
        Coming in {{ milestone() }}
      </p>
      <h1 class="fm-display mt-2 text-4xl md:text-5xl">{{ heading() }}</h1>
      <p class="mt-4 text-muted">This page is part of the build plan and is not implemented yet.</p>
      <a
        routerLink="/"
        class="mt-8 inline-block font-semibold text-accent underline underline-offset-4"
        >Back to home</a
      >
    </section>
  `,
})
export class PlaceholderPage {
  readonly heading = input.required<string>();
  readonly milestone = input.required<string>();
}
