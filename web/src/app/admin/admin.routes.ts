import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';
import { Overview } from './overview';
import { ModulesAdmin } from './modules-admin';
import { ContentAdmin } from './content-admin';
import { ContentEdit } from './content-edit';
import { CoursesAdmin } from './courses-admin';
import { CourseEdit } from './course-edit';
import { UsersAdmin } from './users-admin';
import { Reports } from './reports';
import { UserReport } from './user-report';
import { ContentReportPage } from './content-report';

export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', component: Overview, title: 'Admin · Training Portal' },
      { path: 'modules', component: ModulesAdmin, title: 'Modules · Admin' },
      { path: 'content', component: ContentAdmin, title: 'Content · Admin' },
      { path: 'content/new', component: ContentEdit, title: 'New content · Admin' },
      { path: 'content/:id', component: ContentEdit, title: 'Edit content · Admin' },
      { path: 'courses', component: CoursesAdmin, title: 'Courses · Admin' },
      { path: 'courses/new', component: CourseEdit, title: 'New course · Admin' },
      { path: 'courses/:id', component: CourseEdit, title: 'Edit course · Admin' },
      { path: 'users', component: UsersAdmin, title: 'Users · Admin' },
      { path: 'reports', component: Reports, title: 'Reports · Admin' },
      { path: 'reports/users/:id', component: UserReport, title: 'Learner report · Admin' },
      {
        path: 'reports/content/:id',
        component: ContentReportPage,
        title: 'Content report · Admin',
      },
    ],
  },
];
