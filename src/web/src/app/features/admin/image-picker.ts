import { DIALOG_DATA, Dialog, DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { Component, ElementRef, inject, input, model, signal, viewChild } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AdminImage,
  PickedImage,
  acceptedImageTypes,
  maxUploadBytes,
} from '../../core/admin/admin.models';
import { Page } from '../../core/catalog/catalog.models';
import { BusyButton } from '../../shared/ui/busy-button';
import { toApiProblem } from '../../core/http/api-problem';

let nextId = 0;

/**
 * Picks the image for a product or brand: upload a new file (with progress and a preview) or choose one already
 * stored. The server decides what is acceptable (magic bytes, size, dimensions); the size check here only saves
 * a pointless upload.
 */
@Component({
  selector: 'app-image-picker',
  imports: [BusyButton],
  template: `
    <fieldset [attr.aria-describedby]="hintId">
      <legend class="text-sm font-semibold">{{ label() }}</legend>
      <div class="mt-2 flex flex-wrap items-start gap-4">
        <div
          class="grid size-28 shrink-0 place-items-center overflow-hidden rounded-lg border border-border bg-surface-2"
        >
          @if (value(); as image) {
            <img
              [src]="image.url"
              [alt]="label() + ' preview'"
              class="max-h-full max-w-full object-contain"
            />
          } @else {
            <span class="px-2 text-center text-xs text-muted">No image</span>
          }
        </div>
        <div class="min-w-0 flex-1 space-y-2">
          <input #fileInput type="file" hidden [accept]="accept" (change)="upload(fileInput)" />
          <div class="flex flex-wrap gap-2">
            <button
              #uploadButton
              type="button"
              class="rounded-md border border-border px-3 py-2 text-sm font-bold hover:bg-surface-2 aria-disabled:opacity-60"
              [appBusy]="uploading()"
              (click)="fileInput.click()"
            >
              Upload image
            </button>
            <button
              type="button"
              class="rounded-md border border-border px-3 py-2 text-sm font-bold hover:bg-surface-2 aria-disabled:opacity-60"
              [appBusy]="uploading()"
              (click)="openLibrary()"
            >
              Choose existing
            </button>
            @if (value()) {
              <button
                type="button"
                class="rounded-md px-3 py-2 text-sm font-bold text-danger hover:bg-danger/10"
                (click)="clear()"
              >
                Remove image
              </button>
            }
          </div>
          <p [id]="hintId" class="text-sm text-muted">
            PNG, JPEG or WebP, up to 1 MB and 4000×4000 px. Stored as WebP without metadata.
          </p>
          <div aria-live="polite">
            @if (uploading()) {
              <div role="status" class="flex items-center gap-2 text-sm">
                <progress
                  class="h-2 w-40 accent-accent"
                  max="100"
                  [attr.value]="progress()"
                  aria-label="Upload progress"
                ></progress>
                <span>Uploading… {{ progress() === null ? '' : progress() + ' %' }}</span>
              </div>
            }
          </div>
          <div aria-live="assertive">
            @if (error() ?? serverError(); as message) {
              <p role="alert" class="text-sm font-semibold text-danger">{{ message }}</p>
            }
          </div>
        </div>
      </div>
    </fieldset>
  `,
})
export class ImagePicker {
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly uploadButton = viewChild.required<ElementRef<HTMLButtonElement>>('uploadButton');

  readonly label = input('Image');
  readonly value = model<PickedImage | null>(null);
  /** A server field error for the image ID, from the surrounding form. */
  readonly serverError = input<string | null>(null);

  protected readonly accept = acceptedImageTypes;
  protected readonly hintId = `image-hint-${nextId++}`;
  protected readonly uploading = signal(false);
  protected readonly progress = signal<number | null>(null);
  protected readonly error = signal<string | null>(null);

  protected upload(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = ''; // choosing the same file again still fires change
    if (!file) return;
    this.error.set(null);
    if (file.size > maxUploadBytes) {
      this.error.set('The image is larger than 1 MB.');
      return;
    }

    const form = new FormData();
    form.append('file', file);
    this.uploading.set(true);
    this.progress.set(0);
    this.http
      .post<AdminImage>('/api/admin/images', form, { reportProgress: true, observe: 'events' })
      .subscribe({
        next: (event) => {
          if (event.type === HttpEventType.UploadProgress) {
            this.progress.set(event.total ? Math.round((event.loaded / event.total) * 100) : null);
          } else if (event.type === HttpEventType.Response && event.body) {
            this.value.set({ id: event.body.id, url: event.body.url });
          }
        },
        error: (error: unknown) => {
          const problem = toApiProblem(error);
          this.error.set(problem.fieldErrors['file']?.[0] ?? problem.title);
          this.uploading.set(false);
        },
        complete: () => this.uploading.set(false),
      });
  }

  protected async openLibrary(): Promise<void> {
    const ref = this.dialog.open<PickedImage>(ImageLibraryDialog, {
      ariaModal: true,
      ariaLabelledBy: 'image-library-title',
      data: this.value()?.id ?? null,
      autoFocus: 'first-tabbable',
    });
    const picked = await firstValueFrom(ref.closed);
    if (picked) {
      this.error.set(null);
      this.value.set(picked);
    }
  }

  protected clear(): void {
    this.value.set(null);
    this.error.set(null);
    // The Remove button disappears; keep focus in the picker.
    this.uploadButton().nativeElement.focus();
  }
}

/** Stored images, newest first, as a grid of buttons; "Load more" pages through the rest. */
@Component({
  selector: 'app-image-library-dialog',
  imports: [DatePipe],
  template: `
    <div
      class="flex max-h-[85vh] w-[min(94vw,44rem)] flex-col rounded-xl border border-border bg-surface p-5 text-fg shadow-xl"
    >
      <div class="flex items-center justify-between gap-3">
        <h2 id="image-library-title" class="fm-display text-xl">Choose an image</h2>
        <button
          type="button"
          class="rounded-md px-3 py-1.5 text-sm font-bold hover:bg-surface-2"
          (click)="ref.close()"
        >
          Close
        </button>
      </div>
      <div class="mt-4 min-h-0 flex-1 overflow-y-auto">
        @if (error()) {
          <p role="alert" class="text-sm text-danger">{{ error() }}</p>
        }
        <ul class="grid grid-cols-3 gap-2 sm:grid-cols-4 md:grid-cols-6">
          @for (image of images(); track image.id) {
            <li>
              <button
                type="button"
                class="block aspect-square w-full overflow-hidden rounded-lg border-2 bg-surface-2 p-1 hover:border-accent"
                [class.border-accent]="image.id === selectedId"
                [class.border-transparent]="image.id !== selectedId"
                [attr.aria-pressed]="image.id === selectedId"
                (click)="ref.close({ id: image.id, url: image.url })"
              >
                <img [src]="image.url" alt="" loading="lazy" class="size-full object-contain" />
                <span class="sr-only"
                  >Image {{ image.width }}×{{ image.height }}, added
                  {{ image.createdAt | date: 'medium' }}</span
                >
              </button>
            </li>
          }
        </ul>
        <div aria-live="polite" class="mt-3 text-center text-sm text-muted">
          @if (loading()) {
            <p role="status">Loading images…</p>
          } @else if (images().length < total()) {
            <button
              type="button"
              class="rounded-md border border-border px-4 py-2 font-bold text-fg hover:bg-surface-2"
              (click)="load()"
            >
              Load more
            </button>
          } @else if (total() === 0) {
            <p>No images yet. Upload one instead.</p>
          }
        </div>
      </div>
    </div>
  `,
})
export class ImageLibraryDialog {
  private readonly http = inject(HttpClient);
  protected readonly ref = inject<DialogRef<PickedImage>>(DialogRef);
  protected readonly selectedId = inject<string | null>(DIALOG_DATA);

  protected readonly images = signal<readonly AdminImage[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  private page = 0;

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const next = await firstValueFrom(
        this.http.get<Page<AdminImage>>('/api/admin/images', { params: { page: this.page + 1 } }),
      );
      this.page = next.page;
      this.total.set(next.totalCount);
      this.images.update((list) => [...list, ...next.items]);
    } catch (error) {
      this.error.set(toApiProblem(error).title);
    } finally {
      this.loading.set(false);
    }
  }
}
