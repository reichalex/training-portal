import { Component, effect, inject, input, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { ModuleDetail } from '../core/models';
import { ContentList, CourseCard, ModuleCard } from '../shared/cards';
import { Breadcrumb } from '../shared/ui';

@Component({
  selector: 'app-module-page',
  imports: [Breadcrumb, ContentList, CourseCard, ModuleCard],
  template: `
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (module(); as m) {
      <app-breadcrumb [crumbs]="m.breadcrumb" [skipLast]="true" />
      <div class="page-head">
        <h1>{{ m.name }}</h1>
        @if (m.description) {
          <p class="muted">{{ m.description }}</p>
        }
      </div>

      @if (m.courses.length) {
        <section>
          <h2>Courses</h2>
          <div class="grid">
            @for (c of m.courses; track c.id) {
              <app-course-card [course]="c" />
            }
          </div>
        </section>
      }
      @if (m.children.length) {
        <section>
          <h2>Sections</h2>
          <div class="grid">
            @for (s of m.children; track s.id) {
              <app-module-card [module]="s" />
            }
          </div>
        </section>
      }
      @if (m.content.length || !m.children.length) {
        <section>
          <h2>Videos &amp; articles</h2>
          <app-content-list [items]="m.content" emptyText="No videos or articles here yet." />
        </section>
      }
    }
  `,
})
export class ModulePage {
  private readonly api = inject(LearnerApi);
  private readonly title = inject(Title);
  readonly id = input.required<string>();
  protected readonly module = signal<ModuleDetail | null>(null);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const id = Number(this.id());
      this.error.set('');
      this.api.module(id).then(
        (m) => {
          this.module.set(m);
          this.title.setTitle(`${m.name} · Training Portal`);
        },
        (e) => {
          this.module.set(null);
          this.error.set(errorMessage(e));
        },
      );
    });
  }
}
