import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContentSummary, CourseSummary, ModuleTreeNode } from '../core/models';
import { LengthPipe, ProgressBar, TypeIcon } from './ui';

@Component({
  selector: 'app-content-list',
  imports: [RouterLink, TypeIcon, LengthPipe],
  template: `
    <ul class="content-list">
      @for (c of items(); track c.id) {
        <li>
          <a [routerLink]="['/content', c.id]" class="content-row">
            <app-type-icon [type]="c.type" />
            <div class="grow">
              <div class="title">{{ c.title }}</div>
              @if (c.summary) {
                <div class="muted small clamp">{{ c.summary }}</div>
              }
            </div>
            <div class="meta">
              <span class="muted small">{{ c | length }}</span>
              @if (c.completed) {
                <span class="badge success">✓ Done</span>
              } @else if (c.percent > 0) {
                <span class="badge">{{ c.percent }}%</span>
              } @else if (c.lastViewedAt) {
                <span class="badge">Started</span>
              }
            </div>
          </a>
        </li>
      } @empty {
        <li class="muted empty">{{ emptyText() }}</li>
      }
    </ul>
  `,
})
export class ContentList {
  readonly items = input<ContentSummary[]>([]);
  readonly emptyText = input('Nothing here yet.');
}

@Component({
  selector: 'app-course-card',
  imports: [RouterLink, ProgressBar, DatePipe],
  template: `
    @let c = course();
    <a class="card course-card" [routerLink]="['/courses', c.id]">
      <div class="eyebrow">
        Course · {{ c.itemCount }} {{ c.itemCount === 1 ? 'step' : 'steps' }}
        @if (c.moduleName) {
          · {{ c.moduleName }}
        }
      </div>
      <h3>{{ c.title }}</h3>
      @if (c.description) {
        <p class="muted small clamp">{{ c.description }}</p>
      }
      <div class="spacer"></div>
      @if (c.dueDate && !c.completedAt) {
        <div class="small" [class.overdue]="overdue()">
          Due {{ c.dueDate | date: 'mediumDate' }}{{ overdue() ? ' · overdue' : '' }}
        </div>
      }
      <app-progress [value]="c.percent" />
      <div class="small muted">
        @if (c.completedAt) {
          Completed {{ c.completedAt | date: 'mediumDate' }}
        } @else if (c.enrolledAt) {
          {{ c.completedItems }} of {{ c.itemCount }} done
        } @else {
          Not started
        }
      </div>
    </a>
  `,
})
export class CourseCard {
  readonly course = input.required<CourseSummary>();
  protected readonly overdue = computed(() => {
    const c = this.course();
    return !!c.dueDate && !c.completedAt && new Date(c.dueDate) < new Date();
  });
}

@Component({
  selector: 'app-module-card',
  imports: [RouterLink, ProgressBar],
  template: `
    @let m = module();
    <a class="card module-card" [routerLink]="['/modules', m.id]">
      <h3>{{ m.name }}</h3>
      @if (m.description) {
        <p class="muted small clamp">{{ m.description }}</p>
      }
      @if (m.children.length) {
        <div class="chips">
          @for (s of m.children.slice(0, 4); track s.id) {
            <span class="chip">{{ s.name }}</span>
          }
          @if (m.children.length > 4) {
            <span class="chip">+{{ m.children.length - 4 }}</span>
          }
        </div>
      }
      <div class="spacer"></div>
      <app-progress [value]="m.contentCount ? (100 * m.completedCount) / m.contentCount : 0" />
      <div class="small muted">{{ m.completedCount }} of {{ m.contentCount }} completed</div>
    </a>
  `,
})
export class ModuleCard {
  readonly module = input.required<ModuleTreeNode>();
}
