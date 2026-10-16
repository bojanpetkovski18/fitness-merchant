import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MetaStore } from '../meta.store';

/** Shown on every page of the public demo, so nobody mistakes it for a real shop (SPEC F17). */
@Component({
  selector: 'app-demo-banner',
  imports: [RouterLink],
  template: `
    @if (meta.demoMode()) {
      <div class="border-b border-accent/40 bg-accent/10 px-4 py-2 text-center text-sm text-fg">
        <strong>Demo store:</strong> invented products, people and orders; no payment is ever taken.
        @if (meta.resetIntervalHours(); as hours) {
          Everything resets every {{ hours }} {{ hours === 1 ? 'hour' : 'hours' }}.
        }
        <a routerLink="/about" class="font-semibold underline underline-offset-4"
          >About this demo</a
        >
      </div>
    }
  `,
})
export class DemoBanner {
  protected readonly meta = inject(MetaStore);
}
