import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.page').then((module) => module.LoginPage),
  },
  {
    path: 'recipes',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/recipes/recipes.page').then((module) => module.RecipesPage),
  },
  {
    path: 'diary',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/diary/diary.page').then((module) => module.DiaryPage),
  },
  {
    path: 'stats',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/stats/stats.page').then((module) => module.StatsPage),
  },
  {
    path: 'weight',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/weight/weight.page').then((module) => module.WeightPage),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/profile/profile.page').then((module) => module.ProfilePage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/home/home.page').then((module) => module.HomePage),
  },
  { path: '**', redirectTo: '' },
];
