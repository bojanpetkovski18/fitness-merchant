import { Injectable, signal } from '@angular/core';

export interface Toast {
  readonly id: number;
  readonly message: string;
  readonly tone: 'info' | 'error';
  readonly action?: { readonly label: string; readonly link: string };
}

/** Short confirmations and errors, read out by screen readers through the host's live regions. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly items = signal<readonly Toast[]>([]);
  readonly toasts = this.items.asReadonly();

  show(message: string, options: { tone?: Toast['tone']; action?: Toast['action'] } = {}): void {
    const toast: Toast = {
      id: this.nextId++,
      message,
      tone: options.tone ?? 'info',
      action: options.action,
    };
    // Keep the newest few; older ones would only pile up on small screens.
    this.items.update((list) => [...list.slice(-2), toast]);
    setTimeout(() => this.dismiss(toast.id), toast.tone === 'error' ? 8000 : 5000);
  }

  error(message: string): void {
    this.show(message, { tone: 'error' });
  }

  dismiss(id: number): void {
    this.items.update((list) => list.filter((t) => t.id !== id));
  }
}
