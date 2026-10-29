import { Component, effect, inject, input, signal } from '@angular/core';
import { errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { ContentSummary } from '../core/models';
import { ContentList } from '../shared/cards';

@Component({
  selector: 'app-search',
  imports: [ContentList],
  template: `
    <div class="page-head">
      <h1>Search results</h1>
      <p class="muted">For “{{ q() }}”</p>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (results(); as r) {
      <app-content-list [items]="r" emptyText="No videos or articles match your search." />
    }
  `,
})
export class Search {
  private readonly api = inject(LearnerApi);
  readonly q = input('');
  protected readonly results = signal<ContentSummary[] | null>(null);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const q = this.q();
      this.results.set(null);
      this.api.search(q).then(
        (r) => this.results.set(r),
        (e) => this.error.set(errorMessage(e)),
      );
    });
  }
}
