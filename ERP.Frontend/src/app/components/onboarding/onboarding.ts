import { Component, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { TenantService } from '../../services/tenant/tenant.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { UserSettingsService } from '../../services/user-settings.service';
import { SubscriptionPeriod } from '../../interfaces/TenantDto';
import { RegexPatterns } from '../../interfaces/RegexPatterns';
import { take } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TranslateModule],
  templateUrl: './onboarding.html',
  styleUrl: './onboarding.scss'
})
export class OnboardingComponent implements OnInit , OnDestroy{
  private readonly destroyRef = inject(DestroyRef);
  private redirectTimer?: ReturnType<typeof setTimeout>;

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
  constructor(
    private fb: FormBuilder,
    private tenantService: TenantService,
    private router: Router,
    private route: ActivatedRoute,
    private translate: TranslateService,
    public  userSettings: UserSettingsService
  ) {}
  ngOnInit() {
    this.route.queryParams.pipe(take(1)).subscribe(params => {
      this.planId   = params['planId']   ?? '';
      this.planName = params['planName'] ?? '';
      this.period = (params['period'] as SubscriptionPeriod) ?? SubscriptionPeriod.MONTH;
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

    ['primaryColor', 'secondaryColor'].forEach(name => {
      this.form.get(name)?.valueChanges
        .pipe(takeUntilDestroyed(this.destroyRef)) // ← prevent leak
        .subscribe(() => this.syncColorPickers());
    });
  }
  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (!this.planId) {
      this.error = this.translate.instant('errors.no_plan_selected');
      return;
    }

    this.loading = true;
    this.error   = '';
    const payload = {
      ...this.form.value,
      subscription: {
        subscriptionPlanId: this.planId,
        startDate: new Date().toISOString().split('T')[0],
        period: this.period
      }
    };
    this.tenantService.createTenant(payload).subscribe({
      next: () => {
        this.loading = false;
        this.success = true;
        this.redirectTimer = setTimeout(() => this.router.navigate(['/login']), 5000);
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.message ?? this.translate.instant('errors.unknown');
      }
    });
  }

  private syncColorPickers(): void {
    (['primaryColor', 'secondaryColor'] as const).forEach(controlName => {
      const value = this.form.get(controlName)?.value;
      if (!value) return;

      const pickers = document.querySelectorAll<HTMLInputElement>(
        `input[type="color"][formControlName="${controlName}"]`
      );
      pickers.forEach(el => (el.value = value));
    });
  }

  fillColorTextInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const controlName = input.getAttribute('formControlName') as string;
    this.form.get(controlName)?.setValue(input.value, { emitEvent: false });
  }

  fillColorInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const controlName = input.getAttribute('formControlName') as string;
    const value = input.value.trim();

    // Only update if it's a valid hex color
    if (RegexPatterns.hexColor.test(value)) {
      this.form.get(controlName)?.setValue(value, { emitEvent: false });
    }
  }

  ngOnDestroy(): void {
    clearTimeout(this.redirectTimer);
  }
}