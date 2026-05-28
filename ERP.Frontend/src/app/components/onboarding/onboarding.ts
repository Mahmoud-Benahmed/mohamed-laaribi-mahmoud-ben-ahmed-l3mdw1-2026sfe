import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { TenantService } from '../../services/tenant/tenant.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { UserSettingsService } from '../../services/user-settings.service';
import { SubscriptionPeriod } from '../../interfaces/TenantDto';
import { RegexPatterns } from '../../interfaces/RegexPatterns';

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TranslateModule],
  templateUrl: './onboarding.html',
  styleUrl: './onboarding.scss'
})
export class OnboardingComponent implements OnInit {
  form!: FormGroup;
  planId   = '';
  planName = '';
  period: SubscriptionPeriod = SubscriptionPeriod.MONTH;
  loading  = false;
  error    = '';
  success  = false;
  timezones = [
    'Africa/Tunis', 'Africa/Cairo', 'Europe/Paris',
    'Europe/London', 'America/New_York', 'Asia/Dubai'
  ];
  currencies = ['TND', 'EUR', 'USD', 'GBP', 'MAD', 'DZD'];
  locales    = ['fr-TN', 'en-US', 'fr-FR', 'ar-TN'];
  readonly addressPattern = /^[\p{L}0-9\s,.'\-]+$/u;
  constructor(
    private fb: FormBuilder,
    private tenantService: TenantService,
    private router: Router,
    private route: ActivatedRoute,
    private translate: TranslateService,
    public  userSettings: UserSettingsService
  ) {}
  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.planId   = params['planId']   ?? '';
      this.planName = params['planName'] ?? '';
      this.period   = params['period']   ?? 'MONTH';
    });
    this.form = this.fb.group({
      // Company
      name:           ['', [Validators.required, Validators.maxLength(200), Validators.pattern(RegexPatterns.safeText)]],
      email:          ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
      phone:          ['', [Validators.required, Validators.pattern(RegexPatterns.phone)]],
      subdomainSlug:  ['', [Validators.required, Validators.maxLength(100), Validators.pattern(RegexPatterns.subdomainSlug)]],
      address:        ['', [Validators.required, Validators.maxLength(200), Validators.pattern(RegexPatterns.safeText)]],
      logoUrl:        ['', [Validators.maxLength(500)]],
      // Branding
      primaryColor:   ['', [Validators.pattern(RegexPatterns.hexColor)]],
      secondaryColor: ['', [Validators.pattern(RegexPatterns.hexColor)]],
      // Regional
      currency:       ['TND',          Validators.required],
      locale:         ['fr-TN',        Validators.required],
      timezone:       ['Africa/Tunis', Validators.required],
    });
  }
  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.error   = '';
    const payload = {
      ...this.form.value,
      subscription: {
        subscriptionPlanId: this.planId,
        startDate: new Date().toISOString(),
        period: this.period
      }
    };
    this.tenantService.createTenant(payload).subscribe({
      next: () => {
        this.loading = false;
        this.success = true;
        setTimeout(() => this.router.navigate(['/login']), 2500);
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.message ?? this.translate.instant('ERRORS.UNKNOWN');
      }
    });
  }
}