import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { AdminCourse } from '../core/models';

@Component({
  selector: 'app-courses-admin',
  imports: [RouterLink],
  template: `
    <div class="page-head row">
      <div>
        <h1>Courses</h1>
        <p class="muted">
          A course is an ordered list of videos and articles that learners work through and
          complete.
        </p>
      </div>
      <a class="btn primary" routerLink="/admin/courses/new">New course</a>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }
    <div class="card flush">
      <table class="table">
        <thead>
          <tr>
            <th>Course</th>
            <th class="num">Steps</th>
            <th>Order</th>
            <th>Status</th>
            <th class="num">Enrolled</th>
            <th class="num">Completed</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (c of courses(); track c.id) {
            <tr>
              <td>
                <a [routerLink]="['/admin/courses', c.id]"
                  ><strong>{{ c.title }}</strong></a
                >
              </td>
              <td class="num">{{ c.items.length }}</td>
              <td class="small">{{ c.enforceOrder ? 'In sequence' : 'Any order' }}</td>
              <td>
                @if (c.isPublished) {
                  <span class="badge success">Published</span>
                } @else {
                  <span class="badge">Draft</span>
                }
              </td>
              <td class="num">{{ c.enrolledCount }}</td>
              <td class="num">{{ c.completedCount }}</td>
              <td class="row-actions">
                <a class="link" [routerLink]="['/admin/courses', c.id]">Edit &amp; assign</a>
                <a class="link" [routerLink]="['/courses', c.id]">View</a>
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="7" class="muted">No courses yet.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class CoursesAdmin {
  private readonly api = inject(AdminApi);
  protected readonly courses = signal<AdminCourse[]>([]);
  protected readonly error = signal('');

  constructor() {
    this.api.courses().then(
      (c) => this.courses.set(c),
      (e) => this.error.set(errorMessage(e)),
    );
  }
}
