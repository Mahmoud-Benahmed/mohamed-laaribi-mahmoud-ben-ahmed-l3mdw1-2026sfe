import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { TenantService } from '../../services/tenant/tenant.service';
import { SubscriptionPlanDto } from '../../interfaces/TenantDto';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { UserSettingsService } from '../../services/user-settings.service';
import { SubscriptionPlanService } from '../../services/tenant/subscription-plan.service';

type SubscriptionPeriod= "MONTH" | "YEAR";

@Component({
  selector: 'app-plans',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule],
  templateUrl: './plans.html',
  styleUrl: './plans.scss'
})
export class PlansComponent implements OnInit {
  plans: SubscriptionPlanDto[] = [];
  selectedPlan: SubscriptionPlanDto | null = null;
  period: SubscriptionPeriod = 'MONTH';
  billingYearly= this.period === 'YEAR';
  error = '';

  readonly templateTranslationKey= `tenants.plans.`;
  readonly responseSuccessTranslationKey="tenants.responses.success.";
  readonly responseErrorsTranslationKey="tenants.responses.errors.";

  constructor(
    private tenantService: TenantService,
    private planService: SubscriptionPlanService,
    private router: Router,
    public userSettings: UserSettingsService,
    private translate: TranslateService
  ) {}
  ngOnInit() {
    this.planService.getAllPlans(1, 3).subscribe({
      next: (plans) => {
        this.plans = plans.items.filter(p => p.isActive);
      },
      error: () => {
        this.error = this.translate.instant(`${this.responseErrorsTranslationKey}plans_load_failed`);
      }
    });
  }
  selectPlan(plan: SubscriptionPlanDto) {
    this.selectedPlan = plan;
  }
  selectPeriod(period: SubscriptionPeriod) {
      this.period = period;
      this.billingYearly = period === 'YEAR';
  }
  getPrice(plan: SubscriptionPlanDto): number {
    return this.billingYearly ? plan.yearlyPrice / 12 : plan.monthlyPrice;
  }

  getSavings(plan: SubscriptionPlanDto): number {
    return Math.round((1 - plan.yearlyPrice / (plan.monthlyPrice * 12)) * 100);
  }

  proceed() {
    if (!this.selectedPlan) return;
    this.router.navigate(['/onboarding'], {
      queryParams: { planId: this.selectedPlan.id, planName: this.selectedPlan.name, period: this.period }
    });
  }
}