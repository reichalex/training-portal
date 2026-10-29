import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin">
      <nav class="subnav" aria-label="Admin">
        <a routerLink="/admin" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }"
          >Overview</a
        >
        <a routerLink="/admin/modules" routerLinkActive="active">Modules</a>
        <a routerLink="/admin/content" routerLinkActive="active">Videos &amp; articles</a>
        <a routerLink="/admin/courses" routerLinkActive="active">Courses</a>
        <a routerLink="/admin/users" routerLinkActive="active">Users</a>
        <a routerLink="/admin/reports" routerLinkActive="active">Reports</a>
      </nav>
      <div class="admin-body">
        <router-outlet />
      </div>
    </div>
  `,
})
export class AdminLayout {}
