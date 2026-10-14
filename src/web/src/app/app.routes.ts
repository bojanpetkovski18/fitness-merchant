import { CanActivateFn, Route, Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './core/auth/auth.guards';

/** A lazy placeholder for a screen that a later milestone replaces (SPEC section 6). */
function planned(path: string, heading: string, milestone: string, guard?: CanActivateFn): Route {
  return {
    path,
    title: heading,
    canActivate: guard ? [guard] : [],
    loadComponent: () =>
      import('./features/placeholder/placeholder-page').then((m) => m.PlaceholderPage),
    data: { heading, milestone },
  };
}

export const routes: Routes = [
  {
    path: '',
    title: 'Supplements and training gear',
    data: {
      description:
        'Fitness Merchant: protein, pre-workout and training gear in one modern storefront. A portfolio demo with fictional data.',
    },
    loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
  },
  {
    path: 'supplements',
    title: 'Supplements',
    data: { department: 'supplements' },
    loadComponent: () => import('./features/catalog/listing-page').then((m) => m.ListingPage),
  },
  {
    path: 'gear',
    title: 'Gear',
    data: { department: 'gear' },
    loadComponent: () => import('./features/catalog/listing-page').then((m) => m.ListingPage),
  },
  {
    path: 'products/:slug',
    title: 'Product',
    loadComponent: () => import('./features/catalog/product-page').then((m) => m.ProductPage),
  },
  {
    path: 'brands',
    title: 'Brands',
    data: {
      description:
        'All brands at Fitness Merchant: sports nutrition, strength and cardio equipment, apparel.',
    },
    loadComponent: () => import('./features/catalog/brand-pages').then((m) => m.BrandsPage),
  },
  {
    path: 'brands/:slug',
    title: 'Brand',
    loadComponent: () => import('./features/catalog/brand-pages').then((m) => m.BrandPage),
  },
  {
    path: 'cart',
    title: 'Cart',
    loadComponent: () => import('./features/cart/cart-page').then((m) => m.CartPage),
  },
  {
    path: 'checkout',
    title: 'Checkout',
    canActivate: [authGuard],
    loadComponent: () => import('./features/checkout/checkout-page').then((m) => m.CheckoutPage),
  },
  {
    path: 'orders/:number/confirmation',
    title: 'Order confirmed',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/orders/order-pages').then((m) => m.OrderConfirmationPage),
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/account-layout').then((m) => m.AccountLayout),
    children: [
      {
        path: '',
        title: 'Profile',
        loadComponent: () => import('./features/account/profile-page').then((m) => m.ProfilePage),
      },
      {
        path: 'password',
        title: 'Password',
        loadComponent: () => import('./features/account/password-page').then((m) => m.PasswordPage),
      },
      {
        path: 'orders',
        title: 'My orders',
        loadComponent: () => import('./features/orders/order-pages').then((m) => m.OrdersPage),
      },
      {
        path: 'orders/:number',
        title: 'Order details',
        loadComponent: () => import('./features/orders/order-pages').then((m) => m.OrderDetailPage),
      },
    ],
  },
  {
    path: 'sign-in',
    title: 'Sign in',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: 'register',
    title: 'Create account',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register-page').then((m) => m.RegisterPage),
  },
  planned('admin', 'Admin', 'M09', adminGuard),
  planned('about', 'About', 'M12'),
  planned('contact', 'Contact', 'M12'),
  {
    path: '**',
    title: 'Page not found',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
