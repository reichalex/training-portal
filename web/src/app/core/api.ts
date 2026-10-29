import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import {
  AdminContent,
  AdminCourse,
  AdminModule,
  ContentDetail,
  ContentReport,
  ContentSave,
  ContentSummary,
  CourseDetail,
  CourseReport,
  CourseSave,
  CourseSummary,
  Dashboard,
  ModuleDetail,
  ModuleSave,
  ModuleTreeNode,
  OverviewReport,
  Progress,
  User,
  UserReportDetail,
  UserReportRow,
  UserSave,
} from './models';

/** Calls available to every signed-in user. */
@Injectable({ providedIn: 'root' })
export class LearnerApi {
  private readonly http = inject(HttpClient);

  dashboard = () => this.get<Dashboard>('/api/me/dashboard');
  moduleTree = () => this.get<ModuleTreeNode[]>('/api/modules/tree');
  module = (id: number) => this.get<ModuleDetail>(`/api/modules/${id}`);
  courses = () => this.get<CourseSummary[]>('/api/courses');
  course = (id: number) => this.get<CourseDetail>(`/api/courses/${id}`);
  enroll = (id: number) => firstValueFrom(this.http.post<void>(`/api/courses/${id}/enroll`, {}));
  search = (q: string) =>
    this.get<ContentSummary[]>('/api/content/search', new HttpParams().set('q', q));

  content(id: number, courseId?: number | null): Promise<ContentDetail> {
    const params = courseId ? new HttpParams().set('courseId', courseId) : undefined;
    return this.get<ContentDetail>(`/api/content/${id}`, params);
  }

  heartbeat(
    contentId: number,
    body: { positionSeconds: number; durationSeconds: number | null; playedSeconds: number },
  ) {
    return firstValueFrom(this.http.post<Progress>(`/api/progress/${contentId}/heartbeat`, body));
  }

  article(contentId: number, completed: boolean) {
    return firstValueFrom(
      this.http.post<Progress>(
        `/api/progress/${contentId}/article`,
        {},
        { params: new HttpParams().set('completed', completed) },
      ),
    );
  }

  changePassword(currentPassword: string, newPassword: string) {
    return firstValueFrom(
      this.http.post<void>('/api/auth/change-password', { currentPassword, newPassword }),
    );
  }

  private get<T>(url: string, params?: HttpParams): Promise<T> {
    return firstValueFrom(this.http.get<T>(url, { params }));
  }
}

/** Calls for the admin area. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  modules = () => this.get<AdminModule[]>('/api/admin/modules');
  createModule = (m: ModuleSave) => this.send<AdminModule>('POST', '/api/admin/modules', m);
  updateModule = (id: number, m: ModuleSave) =>
    this.send<AdminModule>('PUT', `/api/admin/modules/${id}`, m);
  deleteModule = (id: number) => this.send<void>('DELETE', `/api/admin/modules/${id}`);

  contentList(moduleId?: number | null, includeSubmodules = true): Promise<AdminContent[]> {
    let params = new HttpParams();
    if (moduleId)
      params = params.set('moduleId', moduleId).set('includeSubmodules', includeSubmodules);
    return this.get<AdminContent[]>('/api/admin/content', params);
  }
  content = (id: number) => this.get<AdminContent>(`/api/admin/content/${id}`);
  createContent = (c: ContentSave) => this.send<AdminContent>('POST', '/api/admin/content', c);
  updateContent = (id: number, c: ContentSave) =>
    this.send<AdminContent>('PUT', `/api/admin/content/${id}`, c);
  deleteContent = (id: number) => this.send<void>('DELETE', `/api/admin/content/${id}`);

  /** Uploads a video, reporting progress events so the page can show a progress bar. */
  uploadVideo(id: number, file: File): Observable<HttpEvent<AdminContent>> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<AdminContent>(`/api/admin/content/${id}/video`, form, {
      reportProgress: true,
      observe: 'events',
    });
  }

  uploadImage(file: File): Promise<{ url: string }> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(this.http.post<{ url: string }>('/api/admin/content/images', form));
  }

  courses = () => this.get<AdminCourse[]>('/api/admin/courses');
  course = (id: number) => this.get<AdminCourse>(`/api/admin/courses/${id}`);
  createCourse = (c: CourseSave) => this.send<AdminCourse>('POST', '/api/admin/courses', c);
  updateCourse = (id: number, c: CourseSave) =>
    this.send<AdminCourse>('PUT', `/api/admin/courses/${id}`, c);
  deleteCourse = (id: number) => this.send<void>('DELETE', `/api/admin/courses/${id}`);
  assign = (id: number, userIds: number[], dueDate: string | null) =>
    this.send<void>('POST', `/api/admin/courses/${id}/assign`, { userIds, dueDate });
  unassign = (id: number, userId: number) =>
    this.send<void>('DELETE', `/api/admin/courses/${id}/assign/${userId}`);

  users = () => this.get<User[]>('/api/admin/users');
  createUser = (u: UserSave) => this.send<User>('POST', '/api/admin/users', u);
  updateUser = (id: number, u: UserSave) => this.send<User>('PUT', `/api/admin/users/${id}`, u);

  overview = () => this.get<OverviewReport>('/api/admin/reports/overview');
  userReports = () => this.get<UserReportRow[]>('/api/admin/reports/users');
  userReport = (id: number) => this.get<UserReportDetail>(`/api/admin/reports/users/${id}`);
  contentReport = (id: number) => this.get<ContentReport>(`/api/admin/reports/content/${id}`);
  courseReport = (id: number) => this.get<CourseReport>(`/api/admin/reports/courses/${id}`);

  /** Downloads a CSV export (fetched with the login token, then saved by the browser). */
  async download(kind: 'content' | 'courses'): Promise<void> {
    const blob = await firstValueFrom(
      this.http.get(`/api/admin/reports/export/${kind}.csv`, { responseType: 'blob' }),
    );
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `${kind}-progress-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  private get<T>(url: string, params?: HttpParams): Promise<T> {
    return firstValueFrom(this.http.get<T>(url, { params }));
  }

  private send<T>(method: string, url: string, body?: unknown): Promise<T> {
    return firstValueFrom(this.http.request<T>(method, url, { body }));
  }
}
