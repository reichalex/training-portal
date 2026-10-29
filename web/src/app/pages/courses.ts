import { Component, computed, inject, signal } from '@angular/core';
import { errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { CourseSummary } from '../core/models';
import { CourseCard } from '../shared/cards';

type Filter = 'all' | 'mine' | 'completed';

@Component({
  selector: 'app-courses',
  imports: [CourseCard],
  template: `
    <div class="page-head row">
      <div>
        <h1>Courses</h1>
        <p class="muted">
          Step-by-step series of videos and articles. Complete each step to unlock the next.
        </p>
      </div>
      <div class="tabs">
        <button type="button" [class.active]="filter() === 'all'" (click)="filter.set('all')">
          All
        </button>
        <button type="button" [class.active]="filter() === 'mine'" (click)="filter.set('mine')">
          My courses
        </button>
        <button
          type="button"
          [class.active]="filter() === 'completed'"
          (click)="filter.set('completed')"
        >
          Completed
        </button>
      </div>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    <div class="grid">
      @for (c of shown(); track c.id) {
        <app-course-card [course]="c" />
      } @empty {
        @if (loaded()) {
          <p class="muted">No courses to show.</p>
        }
      }
    </div>
  `,
})
export class Courses {
  private readonly api = inject(LearnerApi);
  protected readonly courses = signal<CourseSummary[]>([]);
  protected readonly filter = signal<Filter>('all');
  protected readonly loaded = signal(false);
  protected readonly error = signal('');
  protected readonly shown = computed(() => {
    const all = this.courses();
    switch (this.filter()) {
      case 'mine':
        return all.filter((c) => c.enrolledAt && !c.completedAt);
      case 'completed':
        return all.filter((c) => c.completedAt);
      default:
        return all;
    }
  });

  constructor() {
    this.api.courses().then(
      (c) => {
        this.courses.set(c);
        this.loaded.set(true);
      },
      (e) => this.error.set(errorMessage(e)),
    );
  }
}
