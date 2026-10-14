import { guestCartStorage } from './guest-cart-storage';

const a = '0199a000-0000-7000-8000-0000000000aa';
const b = '0199a000-0000-7000-8000-0000000000bb';

describe('guestCartStorage', () => {
  beforeEach(() => localStorage.clear());

  it('stores only product IDs and quantities', () => {
    guestCartStorage.write([{ productId: a, quantity: 2, name: 'x', price: 1 } as never]);

    expect(localStorage.getItem('fm-cart')).toBe(JSON.stringify([{ productId: a, quantity: 2 }]));
  });

  it('drops malformed entries, combines duplicates and caps quantities at 10', () => {
    localStorage.setItem(
      'fm-cart',
      JSON.stringify([
        { productId: a, quantity: 6 },
        { productId: a, quantity: 7 },
        { productId: 'not-a-uuid', quantity: 1 },
        { productId: b, quantity: 0 },
        { productId: b, quantity: 1.5 },
        'junk',
        null,
      ]),
    );

    expect(guestCartStorage.read()).toEqual([{ productId: a, quantity: 10 }]);
  });

  it('treats unreadable storage as an empty cart', () => {
    localStorage.setItem('fm-cart', '{not json');

    expect(guestCartStorage.read()).toEqual([]);
  });

  it('removes the key when the cart is emptied', () => {
    guestCartStorage.write([{ productId: a, quantity: 1 }]);
    guestCartStorage.clear();

    expect(localStorage.getItem('fm-cart')).toBeNull();
  });
});
