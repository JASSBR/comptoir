import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'migration' },
      {
        path: 'migration',
        title: 'Migration · Comptoir Durand',
        loadComponent: () => import('./pages/migration/migration').then((m) => m.Migration),
      },
      {
        path: 'devis',
        title: 'Devis express · Comptoir Durand',
        loadComponent: () => import('./pages/quote/quote').then((m) => m.QuoteBuilder),
      },
      {
        path: 'clients',
        title: 'Clients · Comptoir Durand',
        loadComponent: () => import('./pages/customers/customers').then((m) => m.Customers),
      },
      {
        path: 'catalogue',
        title: 'Catalogue · Comptoir Durand',
        loadComponent: () => import('./pages/catalog/catalog').then((m) => m.Catalog),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
