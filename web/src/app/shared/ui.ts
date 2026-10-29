import { Component, Pipe, PipeTransform, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { marked } from 'marked';
import { ContentType, Crumb } from '../core/models';

/** "1:05:09" / "4:07" for a number of seconds. */
@Pipe({ name: 'duration' })
export class DurationPipe implements PipeTransform {
  transform(seconds: number | null | undefined): string {
    if (seconds == null) return '';
    const s = Math.max(0, Math.round(seconds));
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const sec = String(s % 60).padStart(2, '0');
    return h ? `${h}:${String(m).padStart(2, '0')}:${sec}` : `${m}:${sec}`;
  }
}

/** Length shown on content cards: video duration or article reading time. */
@Pipe({ name: 'length' })
export class LengthPipe implements PipeTransform {
  private readonly duration = new DurationPipe();
  transform(item: {
    type: ContentType;
    durationSeconds: number | null;
    estimatedMinutes: number | null;
  }): string {
    if (item.type === 'Video')
      return item.durationSeconds ? this.duration.transform(item.durationSeconds) : '';
    return item.estimatedMinutes ? `${item.estimatedMinutes} min read` : '';
  }
}

/**
 * Markdown to HTML. Bind the result with [innerHTML]; Angular's sanitizer then strips
 * scripts, event handlers and other unsafe markup.
 */
@Pipe({ name: 'markdown' })
export class MarkdownPipe implements PipeTransform {
  transform(md: string | null | undefined): string {
    return md ? (marked.parse(md, { async: false, gfm: true, breaks: false }) as string) : '';
  }
}

@Pipe({ name: 'bytes' })
export class BytesPipe implements PipeTransform {
  transform(n: number | null | undefined): string {
    if (n == null) return '';
    const units = ['B', 'KB', 'MB', 'GB'];
    let i = 0;
    while (n >= 1024 && i < units.length - 1) {
      n /= 1024;
      i++;
    }
    return `${n.toFixed(i ? 1 : 0)} ${units[i]}`;
  }
}

@Component({
  selector: 'app-progress',
  template: `
    <div
      class="progress"
      [class.done]="value() >= 100"
      role="progressbar"
      [attr.aria-valuenow]="value()"
      aria-valuemin="0"
      aria-valuemax="100"
    >
      <div class="progress-fill" [style.width.%]="value()"></div>
    </div>
  `,
})
export class ProgressBar {
  readonly value = input(0);
}

@Component({
  selector: 'app-type-icon',
  template: `<span
    class="type-icon"
    [class.video]="type() === 'Video'"
    [attr.title]="type()"
    aria-hidden="true"
    >{{ type() === 'Video' ? '▶' : '≡' }}</span
  >`,
})
export class TypeIcon {
  readonly type = input.required<ContentType>();
}

@Component({
  selector: 'app-breadcrumb',
  imports: [RouterLink],
  template: `
    <nav class="breadcrumb" aria-label="Breadcrumb">
      <a routerLink="/browse">Browse</a>
      @for (c of trail(); track c.id) {
        <span class="sep">/</span>
        <a [routerLink]="['/modules', c.id]">{{ c.name }}</a>
      }
    </nav>
  `,
})
export class Breadcrumb {
  readonly crumbs = input<Crumb[]>([]);
  /** Drop the last crumb when it is the page's own title. */
  readonly skipLast = input(false);
  protected readonly trail = computed(() =>
    this.skipLast() ? this.crumbs().slice(0, -1) : this.crumbs(),
  );
}
