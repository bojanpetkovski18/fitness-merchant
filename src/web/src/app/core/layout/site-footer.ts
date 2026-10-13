import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink],
  template: `
    <footer class="mt-16 border-t border-border bg-surface">
      <div
        class="mx-auto flex max-w-7xl flex-col gap-4 px-4 py-8 text-sm text-muted md:flex-row md:items-center md:justify-between"
      >
        <p>
          <span class="fm-display text-fg">Fitness <span class="text-accent">Merchant</span></span>
          · A portfolio demo with a fictional store. No real orders are placed.
        </p>
        <nav aria-label="Footer">
          <ul class="flex gap-4">
            <li><a routerLink="/about" class="hover:text-fg">About</a></li>
            <li><a routerLink="/contact" class="hover:text-fg">Contact</a></li>
          </ul>
        </nav>
      </div>
    </footer>
  `,
})
export class SiteFooter {}
