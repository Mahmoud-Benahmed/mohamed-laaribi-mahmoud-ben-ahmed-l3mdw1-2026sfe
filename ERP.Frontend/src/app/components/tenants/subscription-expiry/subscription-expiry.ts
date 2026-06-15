import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { take } from 'rxjs';
import { TenantService } from '../../../services/tenant/tenant.service';
import { SubscriptionPlanService } from '../../../services/tenant/subscription-plan.service';
import { AuthService } from '../../../services/auth/auth.service';
import { TenantThemeService, UserSettingsService } from '../../../services/user-settings.service';
import { SubscriptionPeriod, SubscriptionPlanDto } from '../../../interfaces/TenantDto';
import { MatDialog } from '@angular/material/dialog';
import { ModalComponent } from '../../modal/modal';
import { MatIcon } from "@angular/material/icon";

@Component({
  selector: 'app-subscription-expiry',
  standalone: true,
  imports: [CommonModule, TranslateModule, MatIcon],
  templateUrl: './subscription-expiry.html',
  styleUrl: './subscription-expiry.scss'
})
export class SubscriptionExpiryComponent implements OnInit {
  private tenantService  = inject(TenantService);
  private planService    = inject(SubscriptionPlanService);
  private authService    = inject(AuthService);
  private router         = inject(Router);
  public  userSettings   = inject(UserSettingsService);
  public  translate   = inject(TranslateService);
  private themeService = inject(TenantThemeService);

  plans: SubscriptionPlanDto[] = [];
  selectedPlan: SubscriptionPlanDto | null = null;
  billingYearly = false;
  period: SubscriptionPeriod = SubscriptionPeriod.MONTH;
  readonly SubscriptionPeriod = SubscriptionPeriod;
  error = '';
  isSubmitting = false;
  success = false;

  readonly templateTranslationKey='tenants.subscription';
  readonly plansTranslationKey='tenants.plans';
  readonly responseTranslationKey='tenants.responses';

  ngOnInit(): void {
    this.tenantService.getTenantBranding(this.authService.Slug!).pipe(take(1)).subscribe(settings => {
      if (settings?.isActive) {
        this.router.navigate(['/home']);
        return;
      }

      this.planService.getAllPlans(1, 3).pipe(take(1)).subscribe({
        next: (res) => this.plans = res.items,
        error: () => this.error = 'Failed to load plans. Please try again.'
      });
    });
  }

  selectPlan(plan: SubscriptionPlanDto): void {
    this.selectedPlan = plan;
  }

  getPrice(plan: SubscriptionPlanDto): number {
    return this.billingYearly ? plan.yearlyPrice / 12 : plan.monthlyPrice;
  }

  getSavings(plan: SubscriptionPlanDto): number {
    return Math.round((1 - plan.yearlyPrice / (plan.monthlyPrice * 12)) * 100);
  }

  renew(): void {
    if (!this.selectedPlan || this.isSubmitting) return;

    this.isSubmitting = true;
    const tenantId = this.authService.TenantId!;

    this.tenantService.assignSubscription(tenantId, {
      subscriptionPlanId: this.selectedPlan.id,
      startDate: new Date().toISOString(),
      period: this.period
    }).pipe(take(1)).subscribe({
      next: () => {
        this.success = true;
        // Reload tenant settings so isActive is refreshed
        this.tenantService.invalidateSettings();
        this.themeService.loadAndApply().pipe(take(1)).subscribe(() => {
          setTimeout(() => this.router.navigate(['/home']), 5000);
        });
      },
      error: () => {
        this.error = 'Failed to renew subscription. Please try again.';
        this.isSubmitting = false;
      }
    });
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}