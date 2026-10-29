import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth';
import { Shell } from './shell';
import { Login } from './pages/login';
import { Home } from './pages/home';
import { Browse } from './pages/browse';
import { ModulePage } from './pages/module-page';
import { ContentPage } from './pages/content-page';
import { Courses } from './pages/courses';
import { CoursePage } from './pages/course-page';
import { Search } from './pages/search';
import { Account } from './pages/account';

export const routes: Routes = [
  { path: 'login', component: Login, title: 'Sign in · Training Portal' },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', component: Home, title: 'Home · Training Portal' },
      { path: 'browse', component: Browse, title: 'Browse · Training Portal' },
      { path: 'modules/:id', component: ModulePage, title: 'Training Portal' },
      { path: 'content/:id', component: ContentPage, title: 'Training Portal' },
      { path: 'courses', component: Courses, title: 'Courses · Training Portal' },
      { path: 'courses/:id', component: CoursePage, title: 'Course · Training Portal' },
      { path: 'search', component: Search, title: 'Search · Training Portal' },
      { path: 'account', component: Account, title: 'My account · Training Portal' },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadChildren: () => import('./admin/admin.routes').then((m) => m.adminRoutes),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
