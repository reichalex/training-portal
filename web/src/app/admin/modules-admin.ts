import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { AdminModule, ModuleSave } from '../core/models';

@Component({
  selector: 'app-modules-admin',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="page-head row">
      <div>
        <h1>Modules</h1>
        <p class="muted">
          Top-level modules (e.g. Power BI, Genie) and the sections inside them. Sections can be
          nested as deep as you need.
        </p>
      </div>
      <button class="btn primary" type="button" (click)="startNew(null)">New module</button>
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (form(); as f) {
      <form class="card form" (ngSubmit)="save()">
        <h2>{{ editingId() ? 'Edit module' : f.parentId ? 'New section' : 'New module' }}</h2>
        <div class="form-grid">
          <label>
            Name
            <input name="name" [(ngModel)]="f.name" required maxlength="200" />
          </label>
          <label>
            Inside
            <select name="parent" [(ngModel)]="f.parentId">
              <option [ngValue]="null">— Top level —</option>
              @for (m of parentOptions(); track m.id) {
                <option [ngValue]="m.id">{{ m.path }}</option>
              }
            </select>
          </label>
          <label class="span-2">
            Description
            <textarea
              name="description"
              [(ngModel)]="f.description"
              rows="2"
              maxlength="2000"
            ></textarea>
          </label>
          <label>
            Sort order
            <input type="number" name="sortOrder" [(ngModel)]="f.sortOrder" />
          </label>
          <label class="check">
            <input type="checkbox" name="published" [(ngModel)]="f.isPublished" />
            Published (visible to learners)
          </label>
        </div>
        <div class="actions">
          <button class="btn primary" type="submit" [disabled]="busy() || !f.name.trim()">
            Save
          </button>
          <button class="btn" type="button" (click)="form.set(null)">Cancel</button>
        </div>
      </form>
    }

    <div class="card flush">
      <table class="table">
        <thead>
          <tr>
            <th>Name</th>
            <th class="num">Items</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (m of modules(); track m.id) {
            <tr>
              <td>
                <span [style.padding-left.rem]="depth(m) * 1.5" class="tree-name">
                  @if (depth(m) > 0) {
                    <span class="muted">↳</span>
                  }
                  <strong>{{ m.name }}</strong>
                </span>
              </td>
              <td class="num">
                <a routerLink="/admin/content" [queryParams]="{ moduleId: m.id }">{{
                  m.contentCount
                }}</a>
              </td>
              <td>
                @if (m.isPublished) {
                  <span class="badge success">Published</span>
                } @else {
                  <span class="badge">Hidden</span>
                }
              </td>
              <td class="row-actions">
                <button class="link" type="button" (click)="startNew(m.id)">+ Section</button>
                <a class="link" routerLink="/admin/content/new" [queryParams]="{ moduleId: m.id }"
                  >+ Content</a
                >
                <button class="link" type="button" (click)="edit(m)">Edit</button>
                <button class="link danger" type="button" (click)="remove(m)">Delete</button>
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="4" class="muted">No modules yet. Create one to get started.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class ModulesAdmin {
  private readonly api = inject(AdminApi);
  protected readonly modules = signal<AdminModule[]>([]);
  protected readonly form = signal<ModuleSave | null>(null);
  protected readonly editingId = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  /** A module cannot be placed inside itself or one of its own sections. */
  protected readonly parentOptions = computed(() => {
    const id = this.editingId();
    if (!id) return this.modules();
    const excluded = new Set<number>([id]);
    for (const m of this.modules()) if (m.parentId && excluded.has(m.parentId)) excluded.add(m.id);
    return this.modules().filter((m) => !excluded.has(m.id));
  });

  constructor() {
    this.load();
  }

  protected depth(m: AdminModule): number {
    return m.path.split(' / ').length - 1;
  }

  protected startNew(parentId: number | null): void {
    this.editingId.set(null);
    this.form.set({ parentId, name: '', description: '', sortOrder: 0, isPublished: true });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected edit(m: AdminModule): void {
    this.editingId.set(m.id);
    this.form.set({
      parentId: m.parentId,
      name: m.name,
      description: m.description,
      sortOrder: m.sortOrder,
      isPublished: m.isPublished,
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const id = this.editingId();
      if (id) await this.api.updateModule(id, f);
      else await this.api.createModule(f);
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(m: AdminModule): Promise<void> {
    if (!confirm(`Delete "${m.name}"?`)) return;
    try {
      await this.api.deleteModule(m.id);
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  private async load(): Promise<void> {
    try {
      this.modules.set(await this.api.modules());
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
