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

// ── Branding DTO ──────────────────────────────────────────────────────────────

export interface TenantBrandingDto {
  name:           string;
  logoUrl?:       string;
  primaryColor?:  string;
  secondaryColor?: string;
  currency:       string;
  locale:         string;
  timezone:       string;
}

// ── TenantThemeService ────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class TenantThemeService {
  private http           = inject(HttpClient);
  private authService    = inject(AuthService);
  private currencyConfig = inject(CurrencyConfigService);

  applyBranding(branding: TenantBrandingDto): void {
    const root = document.documentElement;
    if (branding.primaryColor)   root.style.setProperty('--tenant-primary',   branding.primaryColor);
    if (branding.secondaryColor) root.style.setProperty('--tenant-secondary', branding.secondaryColor);
    if (branding.currency)       this.currencyConfig.saveFromBranding(branding.currency, branding.locale);
  }

  loadAndApply() {
    const slug = this.authService.Slug;
    if (!slug) return of(null);

    return this.http.get<TenantBrandingDto>(
      `${environment.apiUrl}/tenants/branding/${slug}`
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

  private _theme    = signal<ThemeType>('light');
  private _language = signal<LanguageType>('en');
  private _pendingSync = false;

  readonly theme    = this._theme.asReadonly();
  readonly language = this._language.asReadonly();

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

  // ── Getters ─────────────────────────────────────────────────────────────────

  get isDark(): boolean { return this._theme() === 'dark'; }
  get isEn():   boolean { return this._language() === 'en'; }

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