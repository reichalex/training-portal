import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { AdminContent, AdminModule } from '../core/models';
import { TypeIcon } from '../shared/ui';

@Component({
  selector: 'app-content-admin',
  imports: [FormsModule, RouterLink, DatePipe, TypeIcon],
  template: `
    <div class="page-head row">
      <h1>Videos &amp; articles</h1>
      <div class="actions">
        <a
          class="btn primary"
          routerLink="/admin/content/new"
          [queryParams]="{ type: 'Video', moduleId: moduleId() }"
          >Upload a video</a
        >
        <a
          class="btn"
          routerLink="/admin/content/new"
          [queryParams]="{ type: 'Article', moduleId: moduleId() }"
          >Write an article</a
        >
      </div>
    </div>
    <div class="filters">
      <select [ngModel]="moduleNumber()" (ngModelChange)="setModule($event)" aria-label="Module">
        <option [ngValue]="null">All modules</option>
        @for (m of modules(); track m.id) {
          <option [ngValue]="m.id">{{ m.path }}</option>
        }
      </select>
      <input
        type="search"
        placeholder="Filter by title…"
        [ngModel]="text()"
        (ngModelChange)="text.set($event)"
        aria-label="Filter by title"
      />
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    <div class="card flush">
      <table class="table">
        <thead>
          <tr>
            <th></th>
            <th>Title</th>
            <th>Module</th>
            <th>Status</th>
            <th class="num">Viewed</th>
            <th class="num">Completed</th>
            <th>Updated</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (c of shown(); track c.id) {
            <tr>
              <td class="icon-cell"><app-type-icon [type]="c.type" /></td>
              <td>
                <a [routerLink]="['/admin/content', c.id]"
                  ><strong>{{ c.title }}</strong></a
                >
                @if (c.type === 'Video' && !c.videoFileName && !c.externalVideoUrl) {
                  <div class="small warn">No video file yet</div>
                }
              </td>
              <td class="muted small">{{ c.modulePath }}</td>
              <td>
                @if (c.isPublished) {
                  <span class="badge success">Published</span>
                } @else {
                  <span class="badge">Draft</span>
                }
              </td>
              <td class="num">{{ c.viewerCount }}</td>
              <td class="num">{{ c.completedCount }}</td>
              <td class="small muted">{{ c.updatedAt | date: 'mediumDate' }}</td>
              <td class="row-actions">
                <a class="link" [routerLink]="['/admin/content', c.id]">Edit</a>
                <a class="link" [routerLink]="['/admin/reports/content', c.id]">Who watched</a>
                <a class="link" [routerLink]="['/content', c.id]">View</a>
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="8" class="muted">No content found.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class ContentAdmin {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  readonly moduleId = input<string>();
  protected readonly moduleNumber = computed(() =>
    this.moduleId() ? Number(this.moduleId()) : null,
  );
  protected readonly modules = signal<AdminModule[]>([]);
  protected readonly items = signal<AdminContent[]>([]);
  protected readonly text = signal('');
  protected readonly error = signal('');
  protected readonly shown = computed(() => {
    const t = this.text().trim().toLowerCase();
    return t ? this.items().filter((c) => c.title.toLowerCase().includes(t)) : this.items();
  });

  constructor() {
    this.api.modules().then(
      (m) => this.modules.set(m),
      (e) => this.error.set(errorMessage(e)),
    );
    effect(() => {
      this.api.contentList(this.moduleNumber()).then(
        (c) => this.items.set(c),
        (e) => this.error.set(errorMessage(e)),
      );
    });
  }

  protected setModule(id: number | null): void {
    this.router.navigate([], { queryParams: { moduleId: id } });
  }
}
