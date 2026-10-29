import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { User, UserSave } from '../core/models';

@Component({
  selector: 'app-users-admin',
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <div class="page-head row">
      <h1>Users</h1>
      <button class="btn primary" type="button" (click)="startNew()">Add user</button>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (form(); as f) {
      <form class="card form" (ngSubmit)="save()">
        <h2>{{ editingId() ? 'Edit user' : 'Add user' }}</h2>
        <div class="form-grid">
          <label
            >Name <input name="name" [(ngModel)]="f.displayName" required maxlength="200"
          /></label>
          <label
            >Email <input type="email" name="email" [(ngModel)]="f.email" required maxlength="256"
          /></label>
          <label
            >Department <input name="department" [(ngModel)]="f.department" maxlength="200"
          /></label>
          <label>
            Role
            <select name="role" [(ngModel)]="f.role">
              <option value="Learner">Learner</option>
              <option value="Admin">Admin (can manage content, courses, users and reports)</option>
            </select>
          </label>
          <label>
            {{ editingId() ? 'New password (leave blank to keep)' : 'Password' }}
            <input
              type="password"
              name="password"
              [(ngModel)]="f.password"
              [required]="!editingId()"
              minlength="8"
              autocomplete="new-password"
            />
          </label>
          <label class="check"
            ><input type="checkbox" name="active" [(ngModel)]="f.isActive" /> Active (can sign
            in)</label
          >
        </div>
        <div class="actions">
          <button class="btn primary" type="submit" [disabled]="busy()">Save</button>
          <button class="btn" type="button" (click)="form.set(null)">Cancel</button>
        </div>
      </form>
    }

    <div class="filters">
      <input
        type="search"
        placeholder="Filter by name, email or department…"
        [ngModel]="filter()"
        (ngModelChange)="filter.set($event)"
        aria-label="Filter users"
      />
    </div>
    <div class="card flush">
      <table class="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Email</th>
            <th>Department</th>
            <th>Role</th>
            <th>Last sign-in</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (u of shown(); track u.id) {
            <tr [class.inactive]="!u.isActive">
              <td>
                <strong>{{ u.displayName }}</strong
                >{{ u.isActive ? '' : ' (inactive)' }}
              </td>
              <td>{{ u.email }}</td>
              <td>{{ u.department }}</td>
              <td>
                @if (u.role === 'Admin') {
                  <span class="badge accent">Admin</span>
                } @else {
                  Learner
                }
              </td>
              <td class="small muted">
                {{ u.lastLoginAt ? (u.lastLoginAt | date: 'medium') : 'Never' }}
              </td>
              <td class="row-actions">
                <button class="link" type="button" (click)="edit(u)">Edit</button>
                <a class="link" [routerLink]="['/admin/reports/users', u.id]">Training record</a>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class UsersAdmin {
  private readonly api = inject(AdminApi);
  protected readonly users = signal<User[]>([]);
  protected readonly form = signal<UserSave | null>(null);
  protected readonly editingId = signal<number | null>(null);
  protected readonly filter = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly shown = computed(() => {
    const t = this.filter().trim().toLowerCase();
    return this.users().filter(
      (u) => !t || `${u.displayName} ${u.email} ${u.department ?? ''}`.toLowerCase().includes(t),
    );
  });

  constructor() {
    this.load();
  }

  protected startNew(): void {
    this.editingId.set(null);
    this.form.set({
      email: '',
      displayName: '',
      department: '',
      role: 'Learner',
      isActive: true,
      password: '',
    });
  }

  protected edit(u: User): void {
    this.editingId.set(u.id);
    this.form.set({
      email: u.email,
      displayName: u.displayName,
      department: u.department,
      role: u.role,
      isActive: u.isActive,
      password: '',
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const body = { ...f, password: f.password || null };
      const id = this.editingId();
      if (id) await this.api.updateUser(id, body);
      else await this.api.createUser(body);
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.users.set(await this.api.users());
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
