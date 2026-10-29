import { HttpEventType } from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { lastValueFrom, tap } from 'rxjs';
import { AdminApi, LearnerApi } from '../core/api';
import { errorMessage } from '../core/auth';
import { AdminContent, AdminModule, ContentSave, ContentType } from '../core/models';
import { BytesPipe, DurationPipe, MarkdownPipe } from '../shared/ui';

type VideoSource = 'upload' | 'link';

@Component({
  selector: 'app-content-edit',
  imports: [FormsModule, RouterLink, MarkdownPipe, BytesPipe, DurationPipe],
  template: `
    <nav class="breadcrumb"><a routerLink="/admin/content">Videos &amp; articles</a></nav>
    <div class="page-head row">
      <h1>
        {{
          existing()
            ? 'Edit ' + (f.type === 'Video' ? 'video' : 'article')
            : f.type === 'Video'
              ? 'New video'
              : 'New article'
        }}
      </h1>
      @if (existing(); as c) {
        <div class="actions">
          <a class="btn" [routerLink]="['/content', c.id]">View as learner</a>
          <a class="btn" [routerLink]="['/admin/reports/content', c.id]"
            >Who watched ({{ c.viewerCount }})</a
          >
        </div>
      }
    </div>
    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @if (!ready()) {
      <p class="muted">Loading…</p>
    } @else {
      <form class="card form" (ngSubmit)="save()">
        @if (!existing()) {
          <div class="segmented">
            <button type="button" [class.active]="f.type === 'Video'" (click)="setType('Video')">
              ▶ Video
            </button>
            <button
              type="button"
              [class.active]="f.type === 'Article'"
              (click)="setType('Article')"
            >
              ≡ Article
            </button>
          </div>
        }
        <div class="form-grid">
          <label class="span-2">
            Title
            <input name="title" [(ngModel)]="f.title" required maxlength="300" />
          </label>
          <label>
            Module
            <select name="module" [(ngModel)]="f.moduleId" required>
              <option [ngValue]="0" disabled>Choose a module…</option>
              @for (m of modules(); track m.id) {
                <option [ngValue]="m.id">{{ m.path }}</option>
              }
            </select>
          </label>
          <label>
            Sort order
            <input type="number" name="sortOrder" [(ngModel)]="f.sortOrder" />
          </label>
          <label class="span-2">
            Summary
            <textarea
              name="summary"
              [(ngModel)]="f.summary"
              rows="2"
              maxlength="2000"
              placeholder="One or two sentences shown in lists"
            ></textarea>
          </label>
        </div>

        @if (f.type === 'Video') {
          <fieldset>
            <legend>Video</legend>
            <div class="segmented small">
              <button
                type="button"
                [class.active]="source() === 'upload'"
                (click)="source.set('upload')"
              >
                Upload a file
              </button>
              <button
                type="button"
                [class.active]="source() === 'link'"
                (click)="source.set('link')"
              >
                Link to a video file
              </button>
            </div>
            @if (source() === 'upload') {
              @if (existing()?.videoFileName) {
                <p class="small">
                  Current file: <strong>{{ existing()?.videoFileName }}</strong> ({{
                    existing()?.videoSizeBytes | bytes
                  }})
                  @if (existing()?.durationSeconds) {
                    · {{ existing()?.durationSeconds | duration }}
                  }
                </p>
              }
              <label class="file-drop" [class.has-file]="file()">
                <input
                  type="file"
                  accept="video/mp4,video/webm,video/quicktime,.mp4,.m4v,.webm,.mov"
                  (change)="pickFile($event)"
                />
                @if (file(); as fl) {
                  <span>{{ fl.name }} · {{ fl.size | bytes }}</span>
                } @else {
                  <span
                    >{{
                      existing()?.videoFileName
                        ? 'Choose a file to replace the video'
                        : 'Choose a video file'
                    }}
                    (MP4, WebM or MOV)</span
                  >
                }
              </label>
              @if (uploadPercent() !== null) {
                <div class="progress">
                  <div class="progress-fill" [style.width.%]="uploadPercent()"></div>
                </div>
                <p class="small muted">Uploading… {{ uploadPercent() }}%</p>
              }
            } @else {
              <label>
                Video URL
                <input
                  type="url"
                  name="externalVideoUrl"
                  [(ngModel)]="f.externalVideoUrl"
                  placeholder="https://…/video.mp4"
                />
                <span class="hint"
                  >A direct link to an MP4 or WebM file. Watch tracking works the same as for
                  uploads.</span
                >
              </label>
            }
            @if (previewUrl(); as url) {
              <video class="preview" [src]="url" controls preload="metadata"></video>
            }
          </fieldset>
        } @else {
          <fieldset>
            <legend>Article</legend>
            <div class="editor-toolbar">
              <div class="segmented small">
                <button type="button" [class.active]="!preview()" (click)="preview.set(false)">
                  Write
                </button>
                <button type="button" [class.active]="preview()" (click)="preview.set(true)">
                  Preview
                </button>
              </div>
              <label class="btn small">
                Insert image
                <input type="file" accept="image/*" hidden (change)="insertImage($event)" />
              </label>
              <label class="inline">
                Reading time
                <input
                  type="number"
                  name="minutes"
                  [(ngModel)]="f.estimatedMinutes"
                  min="1"
                  class="tiny"
                />
                min
              </label>
            </div>
            @if (preview()) {
              <article
                class="article preview-pane"
                [innerHTML]="f.articleBody | markdown"
              ></article>
            } @else {
              <textarea
                #body
                name="articleBody"
                class="mono"
                [(ngModel)]="f.articleBody"
                rows="18"
                placeholder="# Heading&#10;&#10;Write in Markdown: **bold**, *italic*, lists, tables, links and images."
              ></textarea>
              <span class="hint"
                >Markdown formatting. Use “Insert image” to upload screenshots.</span
              >
            }
          </fieldset>
        }

        <label class="check">
          <input type="checkbox" name="published" [(ngModel)]="f.isPublished" />
          Published (visible to learners)
        </label>
        <div class="actions">
          <button
            class="btn primary"
            type="submit"
            [disabled]="busy() || !f.title.trim() || !f.moduleId"
          >
            {{ busy() ? 'Saving…' : 'Save' }}
          </button>
          <a class="btn" routerLink="/admin/content">Cancel</a>
          @if (existing()) {
            <span class="spacer"></span>
            <button class="btn danger" type="button" (click)="remove()">Delete</button>
          }
        </div>
        @if (saved()) {
          <p class="success-text">Saved.</p>
        }
      </form>
    }
  `,
})
export class ContentEdit implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly learnerApi = inject(LearnerApi);
  private readonly router = inject(Router);
  // The form model is a plain object, so changes made after an await need a manual refresh.
  private readonly cdr = inject(ChangeDetectorRef);
  readonly id = input<string>();
  readonly type = input<string>();
  readonly moduleId = input<string>();

  private readonly bodyArea = viewChild<ElementRef<HTMLTextAreaElement>>('body');
  protected readonly modules = signal<AdminModule[]>([]);
  protected readonly existing = signal<AdminContent | null>(null);
  protected readonly source = signal<VideoSource>('upload');
  protected readonly file = signal<File | null>(null);
  protected readonly uploadPercent = signal<number | null>(null);
  protected readonly previewUrl = signal<string | null>(null);
  protected readonly preview = signal(false);
  protected readonly ready = signal(false);
  protected readonly busy = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal('');

  protected f: ContentSave = {
    moduleId: 0,
    title: '',
    summary: '',
    type: 'Video',
    externalVideoUrl: null,
    durationSeconds: null,
    articleBody: '',
    estimatedMinutes: null,
    sortOrder: 0,
    isPublished: false,
  };

  async ngOnInit(): Promise<void> {
    if (this.type() === 'Article') this.f.type = 'Article';
    if (this.moduleId()) this.f.moduleId = Number(this.moduleId());
    this.cdr.markForCheck();
    try {
      // Show the form only once the item has loaded, so nothing typed early is overwritten.
      const [modules] = await Promise.all([
        this.api.modules(),
        this.id() ? this.load(Number(this.id())) : null,
      ]);
      this.modules.set(modules);
      this.ready.set(true);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected setType(t: ContentType): void {
    this.f.type = t;
  }

  protected pickFile(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  protected async insertImage(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const image = input.files?.[0];
    input.value = '';
    if (!image) return;
    try {
      const { url } = await this.api.uploadImage(image);
      const snippet = `![${image.name.replace(/\.[^.]+$/, '')}](${url})`;
      const area = this.bodyArea()?.nativeElement;
      const text = this.f.articleBody ?? '';
      const at = area ? area.selectionStart : text.length;
      this.f.articleBody = `${text.slice(0, at)}\n${snippet}\n${text.slice(at)}`;
      this.preview.set(false);
      this.cdr.markForCheck();
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.saved.set(false);
    this.error.set('');
    try {
      // A link replaces any uploaded file; choosing "upload" clears the link.
      const body: ContentSave = {
        ...this.f,
        externalVideoUrl: this.source() === 'link' ? this.f.externalVideoUrl || null : null,
      };
      const current = this.existing();
      let saved = current
        ? await this.api.updateContent(current.id, body)
        : await this.api.createContent(body);

      const file = this.file();
      if (this.f.type === 'Video' && this.source() === 'upload' && file) {
        this.uploadPercent.set(0);
        const events = this.api.uploadVideo(saved.id, file).pipe(
          tap((ev) => {
            if (ev.type === HttpEventType.UploadProgress && ev.total)
              this.uploadPercent.set(Math.round((100 * ev.loaded) / ev.total));
          }),
        );
        const last = await lastValueFrom(events);
        if (last.type === HttpEventType.Response && last.body) saved = last.body;
        this.file.set(null);
        this.uploadPercent.set(null);
      }

      if (!current) {
        this.router.navigate(['/admin/content', saved.id], { replaceUrl: true });
      }
      await this.load(saved.id);
      this.saved.set(true);
    } catch (e) {
      this.uploadPercent.set(null);
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(): Promise<void> {
    const c = this.existing();
    if (!c) return;
    const warning = c.viewerCount
      ? ` Its viewing history (${c.viewerCount} people) will be deleted too.`
      : '';
    if (!confirm(`Delete "${c.title}"?${warning}`)) return;
    try {
      await this.api.deleteContent(c.id);
      this.router.navigate(['/admin/content']);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  private async load(id: number): Promise<void> {
    const c = await this.api.content(id);
    this.existing.set(c);
    this.f = {
      moduleId: c.moduleId,
      title: c.title,
      summary: c.summary,
      type: c.type,
      externalVideoUrl: c.externalVideoUrl,
      durationSeconds: c.durationSeconds,
      articleBody: c.articleBody ?? '',
      estimatedMinutes: c.estimatedMinutes,
      sortOrder: c.sortOrder,
      isPublished: c.isPublished,
    };
    this.source.set(c.externalVideoUrl ? 'link' : 'upload');
    this.cdr.markForCheck();
    this.previewUrl.set(null);
    if (c.type === 'Video' && (c.videoFileName || c.externalVideoUrl)) {
      // The learner endpoint issues a signed playback URL (admins can open drafts too).
      const detail = await this.learnerApi.content(id);
      this.previewUrl.set(detail.videoUrl);
    }
  }
}
