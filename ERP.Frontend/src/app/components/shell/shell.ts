import { ThemeType, LanguageType, Language, Theme } from './../../interfaces/AuthDto';
import { AuthService, PRIVILEGES } from './../../services/auth/auth.service';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { UserSettingsService } from '../../services/user-settings.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TenantService } from '../../services/tenant/tenant.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatTooltipModule, TranslatePipe],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  encapsulation: ViewEncapsulation.None,
})
export class ShellComponent implements OnInit, OnDestroy {

  mobileNavOpen = false;
  mobileNavClosing = false;

  breadcrumbs: { label: string; link?: string }[] = [];
  private subs = new Subscription();

  collapsed = false;
  openGroups: Record<string, boolean> = {
    auth: false,
    articles: false,
    clients: false,
    stock: false,
    invoices: false,
    payments: false
  };

  userName = '';
  userRole = '';
  initials = '';

  readonly PRIVILEGES = PRIVILEGES;
  readonly THEME= Theme;
  readonly LANGUAGE=  Language;


  constructor(
    private router: Router,
    public authService: AuthService,
    private cdr: ChangeDetectorRef,
    public userSettings: UserSettingsService,
    public translate: TranslateService,
    public tenantService: TenantService,
    public themeService: UserSettingsService
  ) {}

  ngOnInit(): void {
    this.subs.add(
      this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe((e: any) => {
        const url: string = e.urlAfterRedirects;
        this.breadcrumbs = this.getBreadcrumbs(url);
        if (url.startsWith('/users') || url.startsWith('/permissions')) this.openGroups['auth'] = true;
        if (url.startsWith('/articles')) this.openGroups['articles'] = true;
        if (url.startsWith('/clients')) this.openGroups['clients'] = true;
        if (url.startsWith('/stock')) this.openGroups['stock'] = true;
        if (url.startsWith('/invoices')) this.openGroups['invoices'] = true;
        if (url.startsWith('/payments')) this.openGroups['payments'] = true;
      })
    );

    this.subs.add(
      this.authService.userProfile$.subscribe(profile => {
        if (profile) {
          this.userName = profile.fullName ?? profile.email ?? '';
          this.userRole = profile.roleName ?? '';
          this.initials = this.buildInitials(this.userName || profile.email || 'U');
          this.cdr.markForCheck();
        }
      })
    );

    this.subs.add(
      this.translate.onLangChange.subscribe(() => {
        this.breadcrumbs = this.getBreadcrumbs(this.router.url);
      })
    );

    const url = this.router.url;
    this.breadcrumbs = this.getBreadcrumbs(url);
    if (url.startsWith('/users') || url.startsWith('/permissions')) this.openGroups['auth'] = true;
    if (url.startsWith('/articles')) this.openGroups['articles'] = true;
    if (url.startsWith('/clients')) this.openGroups['clients'] = true;
    if (url.startsWith('/stock')) this.openGroups['stock'] = true;
    if (url.startsWith('/invoices')) this.openGroups['invoices'] = true;
    if (url.startsWith('/payments')) this.openGroups['payments'] = true;

    window.addEventListener('resize', this.resizeListener);
  }

  private getBreadcrumbs(url: string): { label: string; link?: string }[] {
    const t = (key: string) => this.translate.instant(key);

    // Special dynamic routes (with parameters)
    if (url.startsWith('/change-password/')) {
      return [
        { label: 'nav.auth.list', link: '/users' },
        { label: 'auth.changePassword.title' } // Use lowercase key
      ];
    }

    if (url.startsWith('/users/register')) {
      return [
        { label: 'nav.auth.list', link: '/users' },
        { label: 'nav.auth.register' }
      ];
    }
    if (url.startsWith('/users/deactivated')) {
      return [
        { label: 'nav.auth.list', link: '/users' },
        { label: 'nav.auth.deactivated' }
      ];
    }
    if (url.startsWith('/users/deleted')) {
      return [
        { label: 'nav.auth.list', link: '/users' },
        { label: 'nav.auth.deleted' }
      ];
    }
    if (url.startsWith('/users/categories')) {
      return [
        { label: 'nav.auth.list', link: '/users' },
        { label: 'nav.auth.controles' }
      ];
    }
    if (url.startsWith('/users/roles')) {
      return [
        { label: 'nav.auth.roles', link: '/roles' },
        { label: 'nav.auth.roles' }
      ];
    }
    if (url.startsWith('/users/')) {
      return [
        { label: 'nav.auth.list', link: '/users' },
        { label: 'auth.profile.edit_profile.title' } // Use lowercase key from your JSON
      ];
    }
    if (url.startsWith('/users')) {
      return [{ label: 'nav.auth.list' }];
    }

    // Articles
    if (url.startsWith('/articles/categories')) {
      return [
        { label: 'nav.articles.main', link: '/articles' },
        { label: 'nav.articles.categories' }
      ];
    }
    if (url.startsWith('/articles')) {
      return [{ label: 'nav.articles.main' }];
    }

    // Clients
    if (url.startsWith('/clients/categories')) {
      return [
        { label: 'nav.clients.main', link: '/clients' },
        { label: 'nav.clients.categories' }
      ];
    }
    if (url.startsWith('/clients')) {
      return [{ label: 'nav.clients.main' }];
    }

    // Invoices
    if (url.startsWith('/invoices')) {
      return [{ label: 'nav.invoices.main' }];
    }

    // Stock
    if (url.startsWith('/stock/fournisseurs')) {
      return [
        { label: 'nav.stock.main', link: '/stock' },
        { label: 'nav.stock.suppliers' }
      ];
    }
    if (url.startsWith('/stock/bons')) {
      return [
        { label: 'nav.stock.main', link: '/stock' },
        { label: 'nav.stock.bons' }
      ];
    }
    if (url.startsWith('/stock')) {
      return [{ label: 'nav.stock.main' }];
    }

    // Payments
    if (url.startsWith('/payments/refunds')) {
      return [
        { label: 'nav.payments.main', link: '/payments' },
        { label: 'nav.payments.refunds' }
      ];
    }
    if (url.startsWith('/payments')) {
      return [{ label: 'nav.payments.main' }];
    }

    // Other top-level routes
    if(url.startsWith('/system-settings')) return [{label: 'auth.profile.actions.system_settings'}];
    if(url.startsWith('/subscription-expiry')) return [{label: 'tenants.subscription.renew_title'}];
    if (url.startsWith('/permissions')) return [{ label: 'nav.auth.permissions' }];
    if (url.startsWith('/audit-log')) return [{ label: 'nav.auth.audit_log' }];
    if (url.startsWith('/profile')) return [{ label: 'home.quick_access.my_profile' }];
    if (url.startsWith('/change-password')) return [{ label: 'auth.profile.changePassword.title_change' }];
    if (url.startsWith('/tenants')) return [{ label: 'nav.tenants.main' }];
    if (url.startsWith('/home')) return [{ label: 'nav.home' }];

    return [{ label: t('common.select') }];
  }

  private resizeListener = () => {
    if (window.innerWidth > 768) this.closeMobileNav();
  };

  toggleNav(): void {
    if (window.innerWidth <= 768) {
      this.mobileNavOpen ? this.closeMobileNav() : this.openMobileNav();
    } else {
      this.toggleSidebar();
    }
  }

  toggleSidebar(): void {
    this.collapsed = !this.collapsed;
  }

  toggleGroup(key: string): void {
    this.openGroups[key] = !this.openGroups[key];
  }

  openMobileNav(): void {
    this.mobileNavOpen = true;
    this.mobileNavClosing = false;
    document.body.style.overflow = 'hidden';
  }

  closeMobileNav(): void {
    document.body.style.overflow = '';
    this.mobileNavClosing = true;
    setTimeout(() => {
      this.mobileNavOpen = false;
      this.mobileNavClosing = false;
    }, 220);
  }

  onLogout(): void {
    this.authService.logout();
  }

  private buildInitials(name: string): string {
    return name.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase() || 'U';
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    document.body.style.overflow = '';
    window.removeEventListener('resize', this.resizeListener);
  }
}