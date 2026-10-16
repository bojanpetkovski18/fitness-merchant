import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';

export interface SiteMeta {
  readonly demoMode: boolean;
  /** Set when demo mode resets the data on a schedule. */
  readonly resetIntervalHours: number | null;
}

/** Facts about this deployment from GET /api/meta, loaded once (D030). Missing or failed means "not a demo". */
@Injectable({ providedIn: 'root' })
export class MetaStore {
  private readonly meta = httpResource<SiteMeta>(() => '/api/meta');

  readonly demoMode = computed(() => (this.meta.hasValue() ? this.meta.value().demoMode : false));
  readonly resetIntervalHours = computed(() =>
    this.meta.hasValue() ? this.meta.value().resetIntervalHours : null,
  );
}
