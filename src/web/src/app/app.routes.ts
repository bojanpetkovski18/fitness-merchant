import { Route, Routes } from '@angular/router';

/** A lazy placeholder for a screen that a later milestone replaces (SPEC section 6). */
function planned(path: string, heading: string, milestone: string): Route {
  return {
    path,
    title: heading,
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
  planned('checkout', 'Checkout', 'M06'),
  planned('orders/:number/confirmation', 'Order confirmation', 'M06'),
  planned('sign-in', 'Sign in', 'M03'),
  planned('register', 'Create account', 'M03'),
  planned('account', 'My account', 'M08'),
  planned('account/orders/:number', 'Order details', 'M08'),
  planned('admin', 'Admin', 'M09'),
  planned('about', 'About', 'M12'),
  planned('contact', 'Contact', 'M12'),
  {
    path: '**',
    title: 'Page not found',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
