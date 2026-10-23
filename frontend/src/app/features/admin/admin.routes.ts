import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./dashboard/admin-dashboard').then((m) => m.AdminDashboard) },
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
      { path: 'products/:id', loadComponent: () => import('./stats/product-stats-page').then((m) => m.ProductStatsPage) },
      { path: 'partnerships', loadComponent: () => import('./partnerships/partnership-list').then((m) => m.PartnershipList) },
      { path: 'audit', loadComponent: () => import('./audit/audit-log').then((m) => m.AuditLog) },
      { path: 'coins', loadComponent: () => import('./coins/admin-coins').then((m) => m.AdminCoins) },
      { path: 'payouts', loadComponent: () => import('./payouts/admin-payout-list').then((m) => m.AdminPayoutList) },
      { path: 'reviews', loadComponent: () => import('./reviews/admin-review-list').then((m) => m.AdminReviewList) },
      { path: 'payouts/:id', loadComponent: () => import('./payouts/admin-payout-detail').then((m) => m.AdminPayoutDetailPage) },
      { path: 'billing', loadComponent: () => import('./billing/admin-billing').then((m) => m.AdminBilling) },
      { path: 'billing/invoices/:id', loadComponent: () => import('./billing/admin-invoice-page').then((m) => m.AdminInvoicePage) },
      { path: 'orders', loadComponent: () => import('./orders/admin-order-list').then((m) => m.AdminOrderList) },
      { path: 'orders/:reference', loadComponent: () => import('./orders/admin-order-detail').then((m) => m.AdminOrderDetail) },
    ],
  },
];
