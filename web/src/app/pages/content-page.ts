import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';
import { ContentDetail, Progress } from '../core/models';
import { Breadcrumb, DurationPipe, MarkdownPipe } from '../shared/ui';
import { VideoPlayer } from '../shared/video-player';

@Component({
  selector: 'app-content-page',
  imports: [RouterLink, Breadcrumb, VideoPlayer, MarkdownPipe, DurationPipe, DatePipe],
  template: `
    @if (error()) {
      <div class="card">
        <p class="error">{{ error() }}</p>
        @if (courseId()) {
          <a class="btn" [routerLink]="['/courses', courseId()]">Back to the course</a>
        }
      </div>
    }
    @if (content(); as c) {
      @if (c.course; as course) {
        <div class="course-bar">
          <a [routerLink]="['/courses', course.courseId]">← {{ course.courseTitle }}</a>
          <span class="muted">Step {{ course.position }} of {{ course.total }}</span>
        </div>
      } @else {
        <app-breadcrumb [crumbs]="c.breadcrumb" />
      }

      <div class="page-head">
        <h1>{{ c.title }}</h1>
        <div class="muted small">
          {{ c.type === 'Video' ? 'Video' : 'Article' }}
          @if (c.type === 'Video' && c.durationSeconds) {
            · {{ c.durationSeconds | duration }}
          }
          @if (c.type === 'Article' && c.estimatedMinutes) {
            · {{ c.estimatedMinutes }} min read
          }
          · Updated {{ c.updatedAt | date: 'mediumDate' }}
        </div>
        @if (c.summary) {
          <p class="lead">{{ c.summary }}</p>
        }
      </div>

      @if (c.type === 'Video') {
        @if (c.videoUrl) {
          <app-video-player
            [contentId]="c.id"
            [src]="c.videoUrl"
            [progress]="c.progress"
            (progressChange)="progress.set($event)"
            (completed)="onCompleted($event)"
          />
        } @else {
          <div class="card muted">This video has not been uploaded yet.</div>
        }
      } @else {
        <article class="card article" [innerHTML]="c.articleBody | markdown"></article>
      }

      <div class="completion">
        @if (progress()?.isCompleted) {
          <span class="badge success large"
            >✓ Completed {{ progress()?.completedAt | date: 'medium' }}</span
          >
        } @else if (c.type === 'Article') {
          <button class="btn primary" type="button" (click)="markRead(c.id)" [disabled]="busy()">
            I've read this
          </button>
        } @else {
          <span class="muted small"
            >Watch the video to the end to complete it. Your place is saved as you go.</span
          >
        }
      </div>

      @if (courseDone()) {
        <div class="card celebrate">
          <h3>🎉 Course complete</h3>
          <p>
            You have finished every step of <strong>{{ c.course?.courseTitle }}</strong
            >.
          </p>
          <a class="btn primary" [routerLink]="['/courses', c.course?.courseId]"
            >Back to the course</a
          >
        </div>
      }

      @if (c.course; as course) {
        <div class="course-nav">
          @if (course.previousContentId) {
            <a
              class="btn"
              [routerLink]="['/content', course.previousContentId]"
              [queryParams]="{ courseId: course.courseId }"
              >← Previous</a
            >
          } @else {
            <span></span>
          }
          @if (course.nextContentId) {
            @if (!course.enforceOrder || progress()?.isCompleted) {
              <a
                class="btn primary"
                [routerLink]="['/content', course.nextContentId]"
                [queryParams]="{ courseId: course.courseId }"
                >Next step →</a
              >
            } @else {
              <button
                class="btn"
                type="button"
                disabled
                title="Complete this step to unlock the next one"
              >
                Next step 🔒
              </button>
            }
          }
        </div>
      }
    }
  `,
})
export class ContentPage {
  private readonly api = inject(LearnerApi);
  private readonly title = inject(Title);
  readonly id = input.required<string>();
  readonly courseId = input<string>();

  protected readonly content = signal<ContentDetail | null>(null);
  protected readonly progress = signal<Progress | null>(null);
  protected readonly courseDone = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const id = Number(this.id());
      const courseId = this.courseId() ? Number(this.courseId()) : null;
      this.content.set(null);
      this.error.set('');
      this.courseDone.set(false);
      this.api.content(id, courseId).then(
        (c) => {
          this.content.set(c);
          this.progress.set(c.progress);
          this.title.setTitle(`${c.title} · Training Portal`);
          if (c.type === 'Article') {
            // Records that the article was opened.
            this.api.article(c.id, false).then(
              (p) => this.progress.set(p),
              () => undefined,
            );
          }
        },
        (e) => this.error.set(errorMessage(e)),
      );
    });
  }

  protected async markRead(id: number): Promise<void> {
    this.busy.set(true);
    try {
      this.onCompleted(await this.api.article(id, true));
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected onCompleted(p: Progress): void {
    this.progress.set(p);
    const course = this.content()?.course;
    if (course && !course.courseCompleted) {
      this.api.course(course.courseId).then(
        (d) => this.courseDone.set(!!d.course.completedAt),
        () => undefined,
      );
    }
  }
}
