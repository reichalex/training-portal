// Mirrors the API's DTOs (api/TrainingPortal.Api/Dtos/Dtos.cs).

export type UserRole = 'Learner' | 'Admin';
export type ContentType = 'Video' | 'Article';

export interface User {
  id: number;
  email: string;
  displayName: string;
  department: string | null;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  hasPassword: boolean;
  signsInWithMicrosoft: boolean;
}

export interface AuthConfig {
  localLogin: boolean;
  entra: { clientId: string; authority: string; scope: string } | null;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface Crumb {
  id: number;
  name: string;
}

export interface ModuleTreeNode {
  id: number;
  name: string;
  description: string | null;
  contentCount: number;
  completedCount: number;
  children: ModuleTreeNode[];
}

export interface ContentSummary {
  id: number;
  moduleId: number;
  title: string;
  summary: string | null;
  type: ContentType;
  durationSeconds: number | null;
  estimatedMinutes: number | null;
  completed: boolean;
  percent: number;
  lastViewedAt: string | null;
}

export interface CourseSummary {
  id: number;
  title: string;
  description: string | null;
  moduleId: number | null;
  moduleName: string | null;
  itemCount: number;
  completedItems: number;
  percent: number;
  enrolledAt: string | null;
  completedAt: string | null;
  dueDate: string | null;
  assigned: boolean;
}

export interface ModuleDetail {
  id: number;
  name: string;
  description: string | null;
  breadcrumb: Crumb[];
  children: ModuleTreeNode[];
  content: ContentSummary[];
  courses: CourseSummary[];
}

export interface Progress {
  lastPositionSeconds: number;
  maxPositionSeconds: number;
  secondsWatched: number;
  isCompleted: boolean;
  firstViewedAt: string;
  lastViewedAt: string;
  completedAt: string | null;
}

export interface CourseContext {
  courseId: number;
  courseTitle: string;
  position: number;
  total: number;
  previousContentId: number | null;
  nextContentId: number | null;
  enforceOrder: boolean;
  courseCompleted: boolean;
}

export interface ContentDetail {
  id: number;
  moduleId: number;
  breadcrumb: Crumb[];
  title: string;
  summary: string | null;
  type: ContentType;
  videoUrl: string | null;
  durationSeconds: number | null;
  articleBody: string | null;
  estimatedMinutes: number | null;
  updatedAt: string;
  progress: Progress | null;
  course: CourseContext | null;
}

export interface CourseItem {
  position: number;
  contentId: number;
  title: string;
  type: ContentType;
  durationSeconds: number | null;
  estimatedMinutes: number | null;
  completed: boolean;
  locked: boolean;
}

export interface CourseDetail {
  course: CourseSummary;
  enforceOrder: boolean;
  items: CourseItem[];
}

export interface Dashboard {
  assignedCourses: CourseSummary[];
  inProgressCourses: CourseSummary[];
  continueWatching: ContentSummary[];
  recentlyCompleted: ContentSummary[];
  stats: { completedItems: number; completedCourses: number; minutesWatched: number };
}

// ---- Admin ----

export interface AdminModule {
  id: number;
  parentId: number | null;
  name: string;
  description: string | null;
  sortOrder: number;
  isPublished: boolean;
  contentCount: number;
  childCount: number;
  path: string;
}

export interface ModuleSave {
  parentId: number | null;
  name: string;
  description: string | null;
  sortOrder: number;
  isPublished: boolean;
}

export interface AdminContent {
  id: number;
  moduleId: number;
  modulePath: string;
  title: string;
  summary: string | null;
  type: ContentType;
  videoFileName: string | null;
  videoSizeBytes: number | null;
  externalVideoUrl: string | null;
  durationSeconds: number | null;
  articleBody: string | null;
  estimatedMinutes: number | null;
  sortOrder: number;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
  createdByName: string | null;
  viewerCount: number;
  completedCount: number;
}

export interface ContentSave {
  moduleId: number;
  title: string;
  summary: string | null;
  type: ContentType;
  externalVideoUrl: string | null;
  durationSeconds: number | null;
  articleBody: string | null;
  estimatedMinutes: number | null;
  sortOrder: number;
  isPublished: boolean;
}

export interface AdminCourseItem {
  contentId: number;
  title: string;
  type: ContentType;
  isPublished: boolean;
  modulePath: string;
}

export interface AdminCourse {
  id: number;
  title: string;
  description: string | null;
  moduleId: number | null;
  enforceOrder: boolean;
  isPublished: boolean;
  createdAt: string;
  items: AdminCourseItem[];
  enrolledCount: number;
  completedCount: number;
}

export interface CourseSave {
  title: string;
  description: string | null;
  moduleId: number | null;
  enforceOrder: boolean;
  isPublished: boolean;
  contentIds: number[];
}

export interface UserSave {
  email: string;
  displayName: string;
  department: string | null;
  role: UserRole;
  isActive: boolean;
  password: string | null;
}

export interface OverviewReport {
  learners: number;
  modules: number;
  videos: number;
  articles: number;
  courses: number;
  completionsLast30Days: number;
  activeLearnersLast30Days: number;
  topContent: {
    contentId: number;
    title: string;
    type: ContentType;
    viewers: number;
    completions: number;
  }[];
}

export interface UserReportRow {
  userId: number;
  displayName: string;
  email: string;
  department: string | null;
  itemsStarted: number;
  itemsCompleted: number;
  coursesEnrolled: number;
  coursesCompleted: number;
  coursesOverdue: number;
  minutesWatched: number;
  lastActivityAt: string | null;
}

export interface UserReportDetail {
  user: User;
  content: {
    contentId: number;
    title: string;
    modulePath: string;
    type: ContentType;
    isCompleted: boolean;
    percent: number;
    secondsWatched: number;
    firstViewedAt: string;
    lastViewedAt: string;
    completedAt: string | null;
  }[];
  courses: CourseSummary[];
}

export interface ContentReport {
  contentId: number;
  title: string;
  type: ContentType;
  rows: {
    userId: number;
    displayName: string;
    email: string;
    department: string | null;
    percent: number;
    isCompleted: boolean;
    secondsWatched: number;
    firstViewedAt: string;
    lastViewedAt: string;
    completedAt: string | null;
  }[];
}

export interface CourseReport {
  courseId: number;
  title: string;
  rows: {
    userId: number;
    displayName: string;
    email: string;
    department: string | null;
    completedItems: number;
    totalItems: number;
    percent: number;
    enrolledAt: string;
    completedAt: string | null;
    dueDate: string | null;
    assigned: boolean;
    overdue: boolean;
  }[];
}
