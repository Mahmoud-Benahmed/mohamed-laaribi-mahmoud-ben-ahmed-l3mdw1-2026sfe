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
    clients:  false,
    stock:    false,
    invoices: false
  };

  userName = '';
  userRole = '';
  initials = '';
  readonly PRIVILEGES= PRIVILEGES;


  constructor(private router: Router,
              public authService: AuthService,
              private cdr: ChangeDetectorRef,
              public userSettings: UserSettingsService,
              public translate: TranslateService,
              public tenantService :TenantService
  ) {
  }


  ngOnInit(): void {
    this.subs.add(
      this.router.events.pipe(
        filter(e => e instanceof NavigationEnd)
      ).subscribe((e: any) => {
        const url: string = e.urlAfterRedirects;
        this.breadcrumbs = this.getBreadcrumbs(url);
        if (url.startsWith('/users') || url.startsWith('/permissions')) this.openGroups['auth'] = true;
        if (url.startsWith('/articles')) this.openGroups['articles'] = true;
        if (url.startsWith('/clients'))  this.openGroups['clients']  = true;
        if (url.startsWith('/stock'))    this.openGroups['stock']    = true;
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

    const url = this.router.url;
    this.breadcrumbs = this.getBreadcrumbs(url);
    if (url.startsWith('/users') || url.startsWith('/permissions')) this.openGroups['auth'] = true;
    if (url.startsWith('/articles')) this.openGroups['articles'] = true;
    if (url.startsWith('/clients'))  this.openGroups['clients']  = true;
    if (url.startsWith('/stock'))    this.openGroups['stock']    = true;
    if (url.startsWith('/invoices')) this.openGroups['invoices'] = true;
    if (url.startsWith('/payments')) this.openGroups['payments'] = true;
    window.addEventListener('resize', this.resizeListener);
  }

  private getBreadcrumbs(url: string): { label: string; link?: string }[] {
    if (url.startsWith('/change-password/'))  return [{ label: 'Users', link: '/users' }, { label: 'Reset Password' }];

    if (url.startsWith('/users/register'))    return [{ label: 'Users', link: '/users' }, { label: 'Register' }];
    if (url.startsWith('/users/deactivated')) return [{ label: 'Users', link: '/users' }, { label: 'Deactivated' }];
    if (url.startsWith('/users/deleted'))     return [{ label: 'Users', link: '/users' }, { label: 'Deleted' }];
    if (url.startsWith('/users/categories'))  return [{ label: 'Users', link: '/users' }, { label: 'Controles' }];
    if (url.startsWith('/users/roles'))       return [{ label: 'Users', link: '/roles' }, { label: 'Roles' }];
    if (url.startsWith('/users/'))            return [{ label: 'Users', link: '/users' }, { label: 'Profile' }];
    if (url.startsWith('/users'))             return [{ label: 'Users' }];

    if (url.startsWith('/articles/categories'))   return [{ label: 'Articles', link: '/articles/categories' }, { label: 'Categories' }];
    if (url.startsWith('/articles'))              return [{ label: 'Articles' }];

    if (url.startsWith('/clients/categories'))    return [{ label: 'Clients', link: '/clients/categories' }, { label: 'Categories' }];
    if (url.startsWith('/clients'))               return [{ label: 'Clients' }];

    if (url.startsWith('/invoices'))              return [{ label: this.translate.instant('NAV.INVOICES'), link:'/invoices' }];

    if (url.startsWith('/stock/fournisseurs'))    return [{ label: this.translate.instant('NAV.STOCK') , link:'/stock/fournisseurs'}, {label: this.translate.instant('NAV.FOURNISSEURS')}];
    if (url.startsWith('/stock/bons'))            return [{ label: this.translate.instant('NAV.STOCK') , link:'/stock/bons'}, {label: this.translate.instant('NAV.BONS')}];
    if (url.startsWith('/stock'))                 return [{ label: this.translate.instant('NAV.STOCK') , link:'/stock'}];

    if (url.startsWith('/payments'))              return [{ label: this.translate.instant('NAV.PAYMENTS') , link:'/payments'}];
    if (url.startsWith('/payments/refunds'))      return [{ label: this.translate.instant('NAV.REFUNDS') , link:'/payments/refunds'}];




    if (url.startsWith('/permissions'))       return [{ label: 'Permissions' }];
    if (url.startsWith('/audit-log'))         return [{ label: 'Audit Log' }];
    if (url.startsWith('/profile'))           return [{ label: 'My Profile' }];
    if (url.startsWith('/change-password'))   return [{ label: 'Change Password' }];
    if (url.startsWith('/home'))              return [{ label: 'Home' }];

    return [{ label: 'Dashboard' }];
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
    this.mobileNavOpen    = true;
    this.mobileNavClosing = false;
    document.body.style.overflow = 'hidden';
  }

  closeMobileNav(): void {
    document.body.style.overflow = ''; // ← immediately, before animation
    this.mobileNavClosing = true;
    setTimeout(() => {
      this.mobileNavOpen    = false;
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