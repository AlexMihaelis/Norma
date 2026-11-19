import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.page').then((module) => module.LoginPage),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/profile/profile.page').then((module) => module.ProfilePage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/diary/diary.page').then((module) => module.DiaryPage),
  },
  { path: '**', redirectTo: '' },
];
