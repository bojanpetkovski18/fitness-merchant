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
  planned('', 'Home', 'M04'),
  planned('supplements', 'Supplements', 'M04'),
  planned('gear', 'Gear', 'M04'),
  planned('products/:slug', 'Product', 'M04'),
  planned('brands', 'Brands', 'M04'),
  planned('brands/:slug', 'Brand', 'M04'),
  planned('cart', 'Cart', 'M05'),
  planned('checkout', 'Checkout', 'M06', authGuard),
  planned('orders/:number/confirmation', 'Order confirmation', 'M06', authGuard),
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
  planned('account', 'My account', 'M08', authGuard),
  planned('account/orders/:number', 'Order details', 'M08', authGuard),
  planned('admin', 'Admin', 'M09', adminGuard),
  planned('about', 'About', 'M12'),
  planned('contact', 'Contact', 'M12'),
  {
    path: '**',
    title: 'Page not found',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
