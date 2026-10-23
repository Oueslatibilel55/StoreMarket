import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/home/home').then((m) => m.Home) },
  { path: 'products', loadComponent: () => import('./features/shop/product-catalog').then((m) => m.ProductCatalog) },
  { path: 'products/:slug', loadComponent: () => import('./features/shop/product-page').then((m) => m.ProductPage) },
  { path: 'stores', loadComponent: () => import('./features/shop/store-list').then((m) => m.StoreList) },
  { path: 'stores/:slug', loadComponent: () => import('./features/shop/store-page').then((m) => m.StorePage) },
  { path: 'cart', loadComponent: () => import('./features/cart/cart-page').then((m) => m.CartPage) },
  { path: 'checkout', loadComponent: () => import('./features/checkout/checkout-page').then((m) => m.CheckoutPage) },
  { path: 'orders', canActivate: [authGuard], loadComponent: () => import('./features/orders/order-list').then((m) => m.OrderList) },
  { path: 'orders/track', loadComponent: () => import('./features/orders/order-track').then((m) => m.OrderTrack) },
  { path: 'reviews', canActivate: [authGuard], loadComponent: () => import('./features/reviews/my-reviews').then((m) => m.MyReviews) },
  { path: 'orders/:reference', loadComponent: () => import('./features/orders/order-page').then((m) => m.OrderPage) },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/account').then((m) => m.Account),
  },
  {
    path: 'affiliate',
    canActivate: [roleGuard('Affiliate')],
    children: [
      { path: '', loadComponent: () => import('./features/affiliate/my-partnerships').then((m) => m.MyPartnerships) },
      { path: 'wallet', loadComponent: () => import('./features/affiliate/wallet/wallet').then((m) => m.Wallet) },
    ],
  },
  {
    path: 'my-store',
    canActivate: [roleGuard('StoreOwner')],
    children: [
      { path: '', loadComponent: () => import('./features/my-store/my-store').then((m) => m.MyStore) },
      { path: 'products', loadComponent: () => import('./features/my-store/products/product-list').then((m) => m.ProductList) },
      { path: 'affiliates', loadComponent: () => import('./features/my-store/affiliates/store-affiliates').then((m) => m.StoreAffiliates) },
      { path: 'stats', loadComponent: () => import('./features/my-store/stats/store-dashboard').then((m) => m.StoreDashboard) },
      { path: 'billing', loadComponent: () => import('./features/my-store/billing/store-billing-page').then((m) => m.StoreBillingPage) },
      { path: 'billing/invoices/:id', loadComponent: () => import('./features/my-store/billing/my-invoice-page').then((m) => m.MyInvoicePage) },
      { path: 'orders', loadComponent: () => import('./features/my-store/orders/store-order-list').then((m) => m.StoreOrderList) },
      { path: 'orders/:id', loadComponent: () => import('./features/my-store/orders/store-order-detail').then((m) => m.StoreOrderDetail) },
      { path: 'reviews', loadComponent: () => import('./features/my-store/reviews/store-reviews').then((m) => m.StoreReviewsPage) },
      { path: 'products/new', loadComponent: () => import('./features/my-store/products/product-editor').then((m) => m.ProductEditor) },
      { path: 'products/:id', loadComponent: () => import('./features/my-store/products/product-editor').then((m) => m.ProductEditor) },
    ],
  },
  {
    path: 'admin',
    canActivate: [roleGuard('Admin')],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
