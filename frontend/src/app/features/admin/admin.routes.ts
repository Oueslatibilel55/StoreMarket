import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'users' },
      { path: 'users', loadComponent: () => import('./users/user-list').then((m) => m.UserList) },
      { path: 'users/:id', loadComponent: () => import('./users/user-detail').then((m) => m.UserDetail) },
      { path: 'role-requests', loadComponent: () => import('./role-requests/role-request-list').then((m) => m.RoleRequestList) },
      { path: 'settings', loadComponent: () => import('./settings/settings').then((m) => m.Settings) },
      {
        path: 'store-categories',
        loadComponent: () => import('./store-categories/store-category-list').then((m) => m.StoreCategoryList),
      },
      {
        path: 'product-categories',
        loadComponent: () => import('./product-categories/product-category-admin').then((m) => m.ProductCategoryAdmin),
      },
      { path: 'stores', loadComponent: () => import('./moderation/store-moderation-list').then((m) => m.StoreModerationList) },
      { path: 'stores/:id', loadComponent: () => import('./moderation/store-moderation-detail').then((m) => m.StoreModerationDetail) },
      { path: 'products', loadComponent: () => import('./moderation/product-moderation-list').then((m) => m.ProductModerationList) },
      { path: 'partnerships', loadComponent: () => import('./partnerships/partnership-list').then((m) => m.PartnershipList) },
      { path: 'audit', loadComponent: () => import('./audit/audit-log').then((m) => m.AuditLog) },
      { path: 'coins', loadComponent: () => import('./coins/admin-coins').then((m) => m.AdminCoins) },
      { path: 'payouts', loadComponent: () => import('./payouts/admin-payout-list').then((m) => m.AdminPayoutList) },
      { path: 'payouts/:id', loadComponent: () => import('./payouts/admin-payout-detail').then((m) => m.AdminPayoutDetailPage) },
      { path: 'orders', loadComponent: () => import('./orders/admin-order-list').then((m) => m.AdminOrderList) },
      { path: 'orders/:reference', loadComponent: () => import('./orders/admin-order-detail').then((m) => m.AdminOrderDetail) },
    ],
  },
];
