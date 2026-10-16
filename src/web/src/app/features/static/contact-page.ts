import { CdkAccordion, CdkAccordionItem } from '@angular/cdk/accordion';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MetaStore } from '../../core/meta.store';
import { repositoryUrl } from './about-page';

interface Faq {
  readonly id: string;
  readonly question: string;
  readonly answer: string;
}

/**
 * Contact and FAQ (SPEC F19). There is no shop to contact, so questions go to the repository; no personal
 * contact data on the page. The FAQ is a CDK accordion: buttons with aria-expanded/aria-controls and regions.
 */
@Component({
  selector: 'app-contact-page',
  imports: [CdkAccordion, CdkAccordionItem, RouterLink],
  template: `
    <div class="mx-auto max-w-3xl px-4 py-12 md:py-16">
      <p class="text-sm font-semibold tracking-widest text-accent uppercase">Contact</p>
      <h1 class="fm-display mt-2 text-4xl md:text-5xl">Questions and answers</h1>

      <section
        aria-labelledby="contact-heading"
        class="mt-8 rounded-xl border border-border bg-surface p-5"
      >
        <h2 id="contact-heading" class="font-bold">Get in touch</h2>
        <p class="mt-2">
          Fitness Merchant is a portfolio project, so there is no customer service. For questions
          about the code or the project, open an issue on
          <a
            [href]="repositoryUrl"
            rel="noopener"
            class="font-semibold text-accent underline underline-offset-4"
            >GitHub</a
          >.
        </p>
      </section>

      <section aria-labelledby="faq-heading" class="mt-10">
        <h2 id="faq-heading" class="fm-display text-2xl">FAQ</h2>
        <cdk-accordion
          class="mt-4 block divide-y divide-border rounded-xl border border-border bg-surface"
        >
          @for (faq of faqs; track faq.id) {
            <cdk-accordion-item #item="cdkAccordionItem" class="block">
              <h3>
                <button
                  type="button"
                  class="flex w-full items-center justify-between gap-4 px-5 py-4 text-left font-semibold hover:bg-surface-2"
                  [id]="'faq-button-' + faq.id"
                  [attr.aria-expanded]="item.expanded"
                  [attr.aria-controls]="'faq-panel-' + faq.id"
                  (click)="item.toggle()"
                >
                  {{ faq.question }}
                  <span aria-hidden="true" class="shrink-0 text-xl leading-none text-muted">{{
                    item.expanded ? '−' : '+'
                  }}</span>
                </button>
              </h3>
              <div
                role="region"
                [id]="'faq-panel-' + faq.id"
                [attr.aria-labelledby]="'faq-button-' + faq.id"
                [hidden]="!item.expanded"
                class="px-5 pb-4 text-muted"
              >
                {{ faq.answer }}
              </div>
            </cdk-accordion-item>
          }
        </cdk-accordion>
      </section>

      <p class="mt-8 text-sm text-muted">
        More about the project on the
        <a routerLink="/about" class="font-semibold underline underline-offset-4">About</a> page.
      </p>
    </div>
  `,
})
export class ContactPage {
  private readonly meta = inject(MetaStore);
  protected readonly repositoryUrl = repositoryUrl;

  protected readonly faqs: readonly Faq[] = [
    {
      id: 'real',
      question: 'Is this a real shop?',
      answer:
        'No. It is a portfolio demo: the products, brands, people and orders are invented, and nothing is sold or shipped.',
    },
    {
      id: 'pay',
      question: 'How do I pay?',
      answer:
        'Use the test card 4242 4242 4242 4242 with any future expiry date and any 3-digit security code. The card 4000 0000 0000 0002 shows what a declined payment looks like. No money is ever taken.',
    },
    {
      id: 'reviews',
      question: 'Who can write a review?',
      answer:
        'Only customers who bought the product in a paid order, once per product. Reviews show your display name, never your email.',
    },
    {
      id: 'data',
      question: 'What happens to my account and orders?',
      answer: this.meta.demoMode()
        ? 'On this public demo everything you create is removed when the data resets, and passwords are stored only as secure hashes. Do not use a password you use anywhere else.'
        : 'Passwords are stored only as secure hashes. On the public demo, everything visitors create is removed when the data resets.',
    },
    {
      id: 'shipping',
      question: 'How much is shipping?',
      answer: 'A flat €4.90, and free from €60. In this demo nothing is actually shipped.',
    },
  ];
}
