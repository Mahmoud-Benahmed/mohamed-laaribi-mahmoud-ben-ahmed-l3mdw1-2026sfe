import { Injectable, signal, inject, effect, untracked } from '@angular/core';
import { take } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { AuthService } from './auth/auth.service';
import { TranslateService } from '@ngx-translate/core';
import { LanguageType, ThemeType } from '../interfaces/AuthDto';
import { toSignal } from '@angular/core/rxjs-interop';
import { environment } from '../environment';
import { CurrencyConfigService } from './currency-config.service';
import { catchError, of, tap } from 'rxjs';
import { TenantService } from './tenant/tenant.service';

// ── Branding DTO ──────────────────────────────────────────────────────────────

export interface TenantBrandingDto {
  name:           string;
  logoUrl?:       string;
  primaryColor?:  string;
  secondaryColor?: string;
  currency:       string;
  locale:         string;
  timezone:       string;
  isActive:       boolean;
}

// ── TenantThemeService ────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class TenantThemeService {
  private http           = inject(HttpClient);
  private authService    = inject(AuthService);
  private currencyConfig = inject(CurrencyConfigService);

  private _theme    = signal<ThemeType>('light');
  private _language = signal<LanguageType>('en');

  readonly theme    = this._theme.asReadonly();
  readonly language = this._language.asReadonly();

  private _primaryColor   = signal<string>('');
  private _secondaryColor = signal<string>('');
  private _logoUrl        = signal<string>('');
  private _isActive       = signal<boolean>(false);
  private _name = signal<string>('');
  private _currency = signal<string>('');
  private _locale = signal<string>('');
  private _timezone = signal<string>('');

  readonly primaryColor   = this._primaryColor.asReadonly();
  readonly secondaryColor = this._secondaryColor.asReadonly();
  readonly logoUrl        = this._logoUrl.asReadonly();
  readonly isActive       = this._isActive.asReadonly();
  readonly name = this._name.asReadonly();
  readonly currency = this._currency.asReadonly();
  readonly locale = this._locale.asReadonly();
  readonly timezone = this._timezone.asReadonly();

  applyBranding(branding: TenantBrandingDto): void {
    const root = document.documentElement;

    if (branding.primaryColor) {
      root.style.setProperty('--tenant-primary', branding.primaryColor);
      root.style.setProperty('--tenant-primary-bg',     `color-mix(in srgb, ${branding.primaryColor} 10%, transparent)`);
      root.style.setProperty('--tenant-primary-border', `color-mix(in srgb, ${branding.primaryColor} 25%, transparent)`);
      this._primaryColor.set(branding.primaryColor);
    }

    if (branding.secondaryColor) {
      root.style.setProperty('--tenant-secondary', branding.secondaryColor);
      root.style.setProperty('--tenant-secondary-bg',     `color-mix(in srgb, ${branding.secondaryColor} 10%, transparent)`);
      root.style.setProperty('--tenant-secondary-border', `color-mix(in srgb, ${branding.secondaryColor} 25%, transparent)`);
      this._secondaryColor.set(branding.secondaryColor);
    }

    if (branding.logoUrl)  this._logoUrl.set(branding.logoUrl);
    if (branding.currency) this.currencyConfig.saveFromBranding(branding.currency, branding.locale);
    if (branding.name) this._name.set(branding.name);
    if (branding.currency) this._currency.set(branding.currency);
    if (branding.locale) this._locale.set(branding.locale);
    if (branding.timezone) this._timezone.set(branding.timezone);

    if (branding.isActive) this._isActive.set(branding.isActive ?? false);
  }

  loadAndApply() {
    const slug = this.authService.Slug;
    if (!slug) return of(null);

    return this.http.get<TenantBrandingDto>(
      `${environment.routes.tenants}/branding/${slug}`
    ).pipe(
      tap(branding => this.applyBranding(branding)),
      catchError(() => of(null))
    );
  }
}

// ── UserSettingsService ───────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private readonly SETTINGS_KEY  = 'userSettings';
  private readonly translate     = inject(TranslateService);
  private readonly authService   = inject(AuthService);
  private readonly themeService  = inject(TenantThemeService);
  private readonly tenantService = inject(TenantService);

  private _pendingSync = false;
  private  _theme    = signal<ThemeType>('light');
  private _language = signal<LanguageType>('en');

  readonly theme    = this._theme.asReadonly();
  readonly language = this._language.asReadonly();

  // ── Delegate branding signals from TenantThemeService ───────────────────────
  readonly primaryColor   = this.themeService.primaryColor;
  readonly secondaryColor = this.themeService.secondaryColor;
  readonly logoUrl        = this.themeService.logoUrl;

  // ── Convenience boolean getters (matching isDark / isEn pattern) ────────────
  get isDark():          boolean { return this._theme() === 'dark'; }
  get isEn():            boolean { return this._language() === 'en'; }
  get currentTheme():    ThemeType    { return this._theme(); }
  get currentLanguage(): LanguageType { return this._language(); }
  get currentPrimary():  string { return this.themeService.primaryColor(); }
  get currentSecondary():string { return this.themeService.secondaryColor(); }
  get currentLogoUrl():  string { return this.themeService.logoUrl(); }
  get currentTenantName(): string { return this.themeService.name(); }
  get currentCurrency(): string { return this.themeService.currency(); }
  get currentLocale(): string { return this.themeService.locale(); }
  get currentTimezone(): string { return this.themeService.timezone(); }

  private readonly userProfile = toSignal(this.authService.userProfile$, { initialValue: null });


  constructor() {
    // 1. Apply cached settings immediately (no flicker on reload)
    const cached = this.loadCachedSettings();
    if (cached) {
      this._theme.set(cached.theme);
      this._language.set(cached.language);
      this.applyThemeToDom(cached.theme);
      this.applyLanguageToDom(cached.language);
    }

    // 2. Sync from server profile when it arrives — skip if local change in flight
    effect(() => {
      const settings = this.userProfile()?.settings;
      if (!settings || this._pendingSync) return;

      untracked(() => {
        this._theme.set(settings.theme);
        this._language.set(settings.language);
        this.applyThemeToDom(settings.theme);
        this.applyLanguageToDom(settings.language);
        this.cacheSettings();

        this.themeService.loadAndApply().pipe(take(1)).subscribe();
        const tenantId = this.authService.TenantId;
        if (tenantId) {
          this.tenantService.loadTenantSettings(tenantId).pipe(take(1)).subscribe();
        }
      });
    });

    // 3. Apply tenant branding after profile is available (slug is in JWT)
    effect(() => {
      const profile = this.userProfile();
      if (!profile) return;

      untracked(() => {
        this.themeService.loadAndApply().pipe(take(1)).subscribe();
      });
    });
  }

  // ── Toggle actions ──────────────────────────────────────────────────────────

  toggleTheme(): void {
    const next = this._theme() === 'dark' ? 'light' : 'dark';
    this._theme.set(next);
    this.applyThemeToDom(next);
    this.cacheSettings();
    this.persistToServer();
  }

  toggleLanguage(): void {
    const next = this._language() === 'en' ? 'fr' : 'en';
    this._language.set(next);
    this.applyLanguageToDom(next);
    this.cacheSettings();
    this.persistToServer();
  }

  // ── DOM helpers ─────────────────────────────────────────────────────────────

  private applyThemeToDom(theme: ThemeType): void {
    document.documentElement.setAttribute('data-theme', theme);
  }

  private applyLanguageToDom(language: LanguageType): void {
    this.translate.use(language);
    document.documentElement.setAttribute('lang', language);
  }

  // ── Persistence ─────────────────────────────────────────────────────────────

  private cacheSettings(): void {
    localStorage.setItem(this.SETTINGS_KEY, JSON.stringify({
      theme:    this._theme(),
      language: this._language(),
    }));
  }

  private loadCachedSettings(): { theme: ThemeType; language: LanguageType } | null {
    try {
      const raw = localStorage.getItem(this.SETTINGS_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  persistToServer(): void {
    const userId = this.authService.UserId;
    if (!userId) return;

    const snapshot = { theme: this._theme(), language: this._language() };
    this._pendingSync = true;

    this.authService.updateSettings(userId, snapshot)
      .pipe(take(1))
      .subscribe({
        next:     () => { this._pendingSync = false; },
        error:    () => {
          // Revert signals and cache to stay consistent with server
          this._theme.set(snapshot.theme);
          this._language.set(snapshot.language);
          this.cacheSettings();
          this._pendingSync = false;
        },
      });
  }
}