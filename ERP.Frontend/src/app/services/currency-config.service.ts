// services/currency-config.service.ts
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class CurrencyConfigService {
  private readonly KEY = 'app_currency';
  private readonly LOCALE_KEY = 'app_locale';

  get code(): string {
    return localStorage.getItem(this.KEY) ?? 'EUR';
  }

  get locale(): string {
    return localStorage.getItem(this.LOCALE_KEY) ?? 'fr-MA';
  }

  save(code: string, locale: string): void {
    localStorage.setItem(this.KEY, code);
    localStorage.setItem(this.LOCALE_KEY, locale);
  }

  saveFromBranding(currency: string, locale: string): void {
    this.save(currency, locale);
  }
}
