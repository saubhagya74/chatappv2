import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'chat',
    loadComponent: () =>
      import('./chat/chat.component').then((m) => m.ChatComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./auth/auth.component').then((m) => m.AuthComponent),
  },
];
