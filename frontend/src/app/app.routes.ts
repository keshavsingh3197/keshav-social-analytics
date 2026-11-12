import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'earnings',
    loadComponent: () => import('./features/earnings-calculator/earnings-calculator.component').then(m => m.EarningsCalculatorComponent)
  },
  {
    path: 'advisor',
    loadComponent: () => import('./features/channel-advisor/channel-advisor.component').then(m => m.ChannelAdvisorComponent)
  },
  {
    path: 'funnel',
    loadComponent: () => import('./features/traffic-funnel/traffic-funnel.component').then(m => m.TrafficFunnelComponent)
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
