import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

export const repositoryUrl = 'https://github.com/bojanpetkovski18/fitness-merchant';

/** What this site is: a portfolio project with invented data, built with AI assistance (SPEC F18). */
@Component({
  selector: 'app-about-page',
  imports: [RouterLink],
  template: `
    <article class="mx-auto max-w-3xl px-4 py-12 md:py-16">
      <p class="text-sm font-semibold tracking-widest text-accent uppercase">About</p>
      <h1 class="fm-display mt-2 text-4xl md:text-5xl">A portfolio project, not a shop</h1>
      <p class="mt-6 text-lg">
        Fitness Merchant is a full-stack online store for supplements and training gear, built to
        show how a modern shop works end to end: catalog, cart, checkout, order tracking, reviews
        and an admin area with orders, a sales dashboard and catalog management.
      </p>

      <h2 class="fm-display mt-10 text-2xl">Everything here is invented</h2>
      <p class="mt-3">
        The brands, products, artwork, customers, orders and reviews are fictional. Payments use
        test cards only; no money is ever taken and nothing is shipped. On the public demo, all data
        resets regularly.
      </p>

      <h2 class="fm-display mt-10 text-2xl">How it was built</h2>
      <p class="mt-3">
        It is a rebuild of the author's 2024 thesis project, made with AI assistance (Claude Code)
        under the author's review: every change went through tests, a real-browser check and a pull
        request.
      </p>
      <ul class="mt-4 list-disc space-y-1 pl-6">
        <li>ASP.NET Core on .NET 10 (Minimal APIs), EF Core and PostgreSQL</li>
        <li>Angular with signals, Tailwind CSS and the Angular CDK</li>
        <li>
          Cookie authentication with CSRF protection, a strict Content-Security-Policy and rate
          limits
        </li>
        <li>Integration tests against a real PostgreSQL (Testcontainers) and component tests</li>
      </ul>

      <div class="mt-10 flex flex-wrap gap-3">
        <a
          [href]="repositoryUrl"
          class="rounded-md bg-accent px-5 py-3 font-bold text-on-accent hover:bg-accent-strong"
          rel="noopener"
          >Source code on GitHub</a
        >
        <a
          routerLink="/contact"
          class="rounded-md border border-border px-5 py-3 font-bold hover:bg-surface-2"
          >Contact and FAQ</a
        >
      </div>
    </article>
  `,
})
export class AboutPage {
  protected readonly repositoryUrl = repositoryUrl;
}
