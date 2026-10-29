import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { CourseDetail } from '../core/models';
import { LengthPipe, ProgressBar, TypeIcon } from '../shared/ui';

@Component({
  selector: 'app-course-page',
  imports: [RouterLink, ProgressBar, TypeIcon, LengthPipe, DatePipe],
  template: `
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    @if (detail(); as d) {
      <nav class="breadcrumb"><a routerLink="/courses">Courses</a></nav>
      <div class="page-head">
        <h1>{{ d.course.title }}</h1>
        @if (d.course.description) {
          <p class="lead">{{ d.course.description }}</p>
        }
      </div>

      <div class="card course-summary">
        <div class="grow">
          <app-progress [value]="d.course.percent" />
          <div class="small muted">
            {{ d.course.completedItems }} of {{ d.course.itemCount }} steps done
            @if (d.course.dueDate && !d.course.completedAt) {
              · due {{ d.course.dueDate | date: 'mediumDate' }}
            }
            @if (d.course.completedAt) {
              · completed {{ d.course.completedAt | date: 'mediumDate' }}
            }
          </div>
        </div>
        @if (nextStep(); as next) {
          <button class="btn primary" type="button" (click)="open(next.contentId)">
            {{ d.course.enrolledAt ? 'Continue' : 'Start course' }}
          </button>
        } @else if (d.course.completedAt) {
          <span class="badge success large">✓ Course complete</span>
        }
      </div>

      @if (d.enforceOrder) {
        <p class="muted small">Steps unlock in order: finish each one to open the next.</p>
      }
      <ol class="steps">
        @for (i of d.items; track i.contentId) {
          <li [class.done]="i.completed" [class.locked]="i.locked">
            <span class="step-num">{{ i.completed ? '✓' : i.locked ? '🔒' : i.position }}</span>
            @if (i.locked) {
              <div class="grow">
                <div class="title">{{ i.title }}</div>
                <div class="muted small">Locked</div>
              </div>
            } @else {
              <a
                class="grow"
                [routerLink]="['/content', i.contentId]"
                [queryParams]="{ courseId: d.course.id }"
              >
                <div class="title">{{ i.title }}</div>
                <div class="muted small">
                  {{ i.type }}{{ (i | length) ? ' · ' + (i | length) : '' }}
                </div>
              </a>
            }
            <app-type-icon [type]="i.type" />
          </li>
        }
      </ol>
    }
  `,
})
export class CoursePage {
  private readonly api = inject(LearnerApi);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  readonly id = input.required<string>();
  protected readonly detail = signal<CourseDetail | null>(null);
  protected readonly error = signal('');
  protected readonly nextStep = computed(
    () => this.detail()?.items.find((i) => !i.completed && !i.locked) ?? null,
  );

  constructor() {
    effect(() => {
      const id = Number(this.id());
      this.api.course(id).then(
        (d) => {
          this.detail.set(d);
          this.title.setTitle(`${d.course.title} · Training Portal`);
        },
        (e) => this.error.set(errorMessage(e)),
      );
    });
  }

  protected open(contentId: number): void {
    this.router.navigate(['/content', contentId], {
      queryParams: { courseId: this.detail()?.course.id },
    });
  }
}
