import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { AuthService } from '../core/auth';
import { LearnerApi } from '../core/api';
import { Progress } from '../core/models';

const HEARTBEAT_MS = 10_000;

/**
 * Plays a training video and reports viewing to the API. Until the video is completed,
 * the learner cannot seek past the furthest point they have actually watched; the server
 * applies the same rule independently, so completion cannot be faked from the browser.
 */
@Component({
  selector: 'app-video-player',
  template: `
    <div class="player">
      <video
        #video
        [src]="src()"
        controls
        controlsList="nodownload"
        disablePictureInPicture
        playsinline
        preload="metadata"
        (contextmenu)="$event.preventDefault()"
        (loadedmetadata)="onLoaded()"
        (play)="onPlay()"
        (pause)="onStop()"
        (ended)="onStop()"
        (timeupdate)="onTimeUpdate()"
        (seeking)="onSeeking()"
      ></video>
      @if (notice()) {
        <div class="player-notice" role="status">{{ notice() }}</div>
      }
    </div>
  `,
})
export class VideoPlayer implements OnInit, OnDestroy {
  private readonly api = inject(LearnerApi);
  private readonly auth = inject(AuthService);

  readonly contentId = input.required<number>();
  readonly src = input.required<string>();
  readonly progress = input<Progress | null>(null);
  /** Every saved heartbeat. */
  readonly progressChange = output<Progress>();
  /** Once, when the server first counts the video as watched. */
  readonly completed = output<Progress>();

  private readonly video = viewChild.required<ElementRef<HTMLVideoElement>>('video');
  protected readonly notice = signal('');

  private furthest = 0;
  private isCompleted = false;
  private playedSeconds = 0;
  private lastTick: number | null = null;
  private lastTime = 0;
  private timer: ReturnType<typeof setInterval> | null = null;
  private noticeTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    const p = this.progress();
    this.furthest = p?.maxPositionSeconds ?? 0;
    this.isCompleted = p?.isCompleted ?? false;
  }

  ngOnDestroy(): void {
    if (this.lastTick !== null) {
      this.accumulate();
      this.send();
    }
    this.stopTimer();
    if (this.noticeTimer) clearTimeout(this.noticeTimer);
  }

  /** Saves the position if the tab is closed mid-video. */
  @HostListener('window:pagehide')
  onPageHide(): void {
    if (this.lastTick === null) return;
    this.accumulate();
    const token = this.auth.token;
    fetch(`/api/progress/${this.contentId()}/heartbeat`, {
      method: 'POST',
      keepalive: true,
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(this.body()),
    }).catch(() => undefined);
    this.playedSeconds = 0;
  }

  protected onLoaded(): void {
    const v = this.video().nativeElement;
    const p = this.progress();
    // Resume where the learner stopped, unless that was at the very start or end.
    if (
      p &&
      !p.isCompleted &&
      p.lastPositionSeconds > 5 &&
      p.lastPositionSeconds < v.duration - 5
    ) {
      v.currentTime = Math.min(p.lastPositionSeconds, this.furthest);
    }
    this.lastTime = v.currentTime;
  }

  protected onPlay(): void {
    this.lastTick = performance.now();
    this.send(); // marks the start, so the server can measure time between heartbeats
    this.stopTimer();
    this.timer = setInterval(() => {
      this.accumulate();
      this.send();
    }, HEARTBEAT_MS);
  }

  protected onStop(): void {
    if (this.lastTick === null) return;
    this.accumulate();
    this.lastTick = null;
    this.stopTimer();
    this.send();
  }

  protected onTimeUpdate(): void {
    const v = this.video().nativeElement;
    const step = v.currentTime - this.lastTime;
    // Normal playback moves forward in small steps; bigger jumps are seeks.
    if (step > 0 && step < 3) this.furthest = Math.max(this.furthest, v.currentTime);
    this.lastTime = v.currentTime;
    if (!v.paused) this.accumulate();
  }

  protected onSeeking(): void {
    const v = this.video().nativeElement;
    if (!this.isCompleted && v.currentTime > this.furthest + 1) {
      v.currentTime = this.furthest;
      this.showNotice("You can skip ahead once you've watched the video through.");
    }
    this.lastTime = v.currentTime;
  }

  private accumulate(): void {
    const now = performance.now();
    if (this.lastTick !== null) this.playedSeconds += (now - this.lastTick) / 1000;
    this.lastTick = now;
  }

  private body() {
    const v = this.video().nativeElement;
    return {
      positionSeconds: Math.floor(v.currentTime),
      durationSeconds: Number.isFinite(v.duration) ? Math.round(v.duration) : null,
      playedSeconds: Math.round(this.playedSeconds * 100) / 100,
    };
  }

  private send(): void {
    const body = this.body();
    this.playedSeconds = 0;
    this.api.heartbeat(this.contentId(), body).then(
      (p) => {
        this.progressChange.emit(p);
        if (p.isCompleted && !this.isCompleted) {
          this.isCompleted = true;
          this.completed.emit(p);
        }
      },
      () => this.showNotice('Your progress could not be saved. Check your connection.'),
    );
  }

  private stopTimer(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }

  private showNotice(text: string): void {
    this.notice.set(text);
    if (this.noticeTimer) clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.notice.set(''), 4000);
  }
}
