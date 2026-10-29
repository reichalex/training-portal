import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService, errorMessage } from '../core/auth';
import { LearnerApi } from '../core/api';

@Component({
  selector: 'app-account',
  imports: [FormsModule],
  template: `
    <div class="page-head">
      <h1>My account</h1>
    </div>
    <div class="card narrow">
      <dl class="facts">
        <dt>Name</dt>
        <dd>{{ auth.user()?.displayName }}</dd>
        <dt>Email</dt>
        <dd>{{ auth.user()?.email }}</dd>
        @if (auth.user()?.department) {
          <dt>Department</dt>
          <dd>{{ auth.user()?.department }}</dd>
        }
        <dt>Role</dt>
        <dd>{{ auth.user()?.role }}</dd>
      </dl>
    </div>
    <form class="card narrow" (ngSubmit)="change()" #f="ngForm">
      <h2>Change password</h2>
      <label
        >Current password
        <input
          type="password"
          name="current"
          [(ngModel)]="current"
          required
          autocomplete="current-password"
      /></label>
      <label
        >New password
        <input
          type="password"
          name="next"
          [(ngModel)]="next"
          required
          minlength="8"
          autocomplete="new-password"
      /></label>
      <label
        >Repeat new password
        <input
          type="password"
          name="repeat"
          [(ngModel)]="repeat"
          required
          autocomplete="new-password"
      /></label>
      @if (message()) {
        <p [class.error]="isError()" [class.success-text]="!isError()">{{ message() }}</p>
      }
      <button class="btn primary" type="submit" [disabled]="f.invalid || busy()">
        Change password
      </button>
    </form>
  `,
})
export class Account {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(LearnerApi);
  protected current = '';
  protected next = '';
  protected repeat = '';
  protected readonly busy = signal(false);
  protected readonly message = signal('');
  protected readonly isError = signal(false);

  async change(): Promise<void> {
    if (this.next !== this.repeat) {
      this.isError.set(true);
      this.message.set('The new passwords do not match.');
      return;
    }
    this.busy.set(true);
    try {
      await this.api.changePassword(this.current, this.next);
      this.isError.set(false);
      this.message.set('Password changed.');
      this.current = this.next = this.repeat = '';
    } catch (e) {
      this.isError.set(true);
      this.message.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
