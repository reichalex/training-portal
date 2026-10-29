import { Component, inject, signal } from '@angular/core';
import { errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { ModuleTreeNode } from '../core/models';
import { ModuleCard } from '../shared/cards';

@Component({
  selector: 'app-browse',
  imports: [ModuleCard],
  template: `
    <div class="page-head">
      <h1>Browse training</h1>
      <p class="muted">Pick a topic to see its sections, videos and articles.</p>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    <div class="grid">
      @for (m of modules(); track m.id) {
        <app-module-card [module]="m" />
      } @empty {
        @if (loaded()) {
          <p class="muted">No training has been published yet.</p>
        }
      }
    </div>
  `,
})
export class Browse {
  private readonly api = inject(LearnerApi);
  protected readonly modules = signal<ModuleTreeNode[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal('');

  constructor() {
    this.api.moduleTree().then(
      (m) => {
        this.modules.set(m);
        this.loaded.set(true);
      },
      (e) => this.error.set(errorMessage(e)),
    );
  }
}
