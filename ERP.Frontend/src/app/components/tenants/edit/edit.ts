import { CommonModule, Location } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { TenantResponseDto, SubscriptionPlanDto, UpdateTenantRequestDto, AssignSubscriptionRequestDto, SubscriptionPeriod, LocaleEnum, TimeZoneEnum, CurrencyEnum } from '../../../interfaces/TenantDto';
import { catchError, forkJoin, of } from 'rxjs';
import { HttpError } from '../../../interfaces/HttpError';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TenantService } from '../../../services/tenant/tenant.service';
import { SubscriptionPlanService } from '../../../services/tenant/subscription-plan.service';
import { ModalComponent } from '../../modal/modal';
import { RegexPatterns } from '../../../interfaces/RegexPatterns';

@Component({
  selector: 'app-tenants-edit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe,
  ],
  templateUrl: './edit.html',
  styleUrl: './edit.scss',
})
export class EditTenantComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);
  private location = inject(Location);

  // ── Forms ──────────────────────────────────────────────────────────────────
  tenantForm!: FormGroup;
  subscriptionForm!: FormGroup;

  // ── State ──────────────────────────────────────────────────────────────────
  selectedTenant: TenantResponseDto | null = null;
  tenantIdFromRoute: string | null = null;
  subscriptionPlans: SubscriptionPlanDto[] = [];
  isValidating = false;
  loading = false;

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null;

  readonly PRIVILEGES = PRIVILEGES;
  readonly SubscriptionPeriod = SubscriptionPeriod;

  readonly LocaleEnum = LocaleEnum;
  readonly CurrencyEnum = CurrencyEnum;
  readonly TimeZoneEnum = TimeZoneEnum;

  constructor(
    public authService: AuthService,
    private tenantService: TenantService,
    private planService: SubscriptionPlanService,
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.tenantIdFromRoute = this.route.snapshot.paramMap.get('id');

    if (!this.tenantIdFromRoute) {
      this.cancel();
      return;
    }

    this.buildForms();
    this.reload();
  }

  private buildForms(): void {
    this.tenantForm = this.fb.group({
      name:           ['', [Validators.required, Validators.maxLength(200), Validators.pattern(RegexPatterns.safeText)]],
      email:          ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
      phone:          ['', [Validators.required, Validators.pattern(RegexPatterns.phone)]],
      address:        ['', [ Validators.maxLength(200), Validators.pattern(RegexPatterns.safeText)]],
      logoUrl:        ['', [Validators.maxLength(500)]],
      primaryColor:   ['', [Validators.pattern(RegexPatterns.hexColor)]],
      secondaryColor: ['', [Validators.pattern(RegexPatterns.hexColor)]],
      currency: [CurrencyEnum.TND, Validators.required],
      locale: [LocaleEnum.FR, Validators.required],
      timezone: [TimeZoneEnum.AFRICA_TUNIS, Validators.required],
    });

    this.subscriptionForm = this.fb.group({
      subscriptionPlanId: ['', Validators.required],
      period: [SubscriptionPeriod.MONTH, Validators.required]
    });
  }

  reload(): void {
    if (!this.tenantIdFromRoute) return;

    this.loading = true;

    forkJoin({
      tenant: this.tenantService.getTenantById(this.tenantIdFromRoute),
      plans: this.planService.getAllPlans(1, 100).pipe(catchError(() => of({ items: [] }))),
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ tenant, plans }) => {
          this.selectedTenant = tenant;
          this.subscriptionPlans = plans.items || [];

          this.populateFormFromTenant(tenant);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.loading = false;
          const errorMsg = (err.error as HttpError)?.message ?? this.translate.instant('tenants.responses.errors.load_failed');
          this.flash('error', errorMsg);
          this.cancel();
        }
      });
  }

  private populateFormFromTenant(tenant: TenantResponseDto): void {
    if (tenant.subscription?.plan?.id) {
      this.subscriptionForm.patchValue({
        subscriptionPlanId: tenant.subscription.plan.id,
        period: tenant.subscription.period || SubscriptionPeriod.MONTH
      });
    }
    this.tenantForm.patchValue({
      name: tenant.name,
      email: tenant.email,
      phone: tenant.phone,
      address: tenant.address,
      currency: tenant.currency || CurrencyEnum.TND,
      locale: this.localeToEnum(tenant.locale),
      timezone: tenant.timezone || TimeZoneEnum.AFRICA_TUNIS,
    });

    this.tenantForm.markAsPristine();
    this.subscriptionForm.markAsPristine();
  }

  getPlanPrice(planId: string, period: SubscriptionPeriod): string {
    const plan = this.subscriptionPlans.find(p => p.id === planId);
    if (!plan) return '—';
    const price = period === SubscriptionPeriod.MONTH ? plan.monthlyPrice : plan.yearlyPrice;
    return `${price.toFixed(2)} ${this.selectedTenant?.currency}`;
  }

  get canSubmit(): boolean {
    return this.tenantForm.valid && !this.isValidating;
  }

  getSubmitButtonTooltip(): string {
    if (this.isValidating) return this.translate.instant('common.processing');
    if (this.tenantForm.invalid) return this.translate.instant('validation.required');
    return '';
  }

  updateTenant(): void {
    if (this.tenantForm.invalid) {
      this.flash('error', this.translate.instant('validation.required'));
      return;
    }

    if (!this.selectedTenant) {
      this.flash('error', this.translate.instant('tenants.responses.errors.load_failed'));
      return;
    }

    this.isValidating = true;
    const formValue = this.tenantForm.value;

    const updateDto: UpdateTenantRequestDto = {
      name: formValue.name,
      email: formValue.email,
      phone: formValue.phone,
      address: formValue.address,
      currency: formValue.currency,
      locale: formValue.locale,
      timezone: formValue.timezone,
    };

    this.tenantService.updateTenant(this.selectedTenant.id, updateDto)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.selectedTenant = updated;
          this.flash('success', this.translate.instant('tenants.responses.success.updated'));
          setTimeout(() => {
            this.isValidating = false;
            this.cancel();
          }, 2000);
        },
        error: (err) => this.handleApiError(err)
      });
  }

  assignSubscription(): void {
    if (this.subscriptionForm.invalid) {
      this.flash('error', this.translate.instant('validation.required'));
      return;
    }

    if (!this.selectedTenant) {
      this.flash('error', this.translate.instant('tenants.responses.errors.load_failed'));
      return;
    }

    this.isValidating = true;

    const formValue = this.subscriptionForm.value;
    const startDate = new Date().toISOString().split('T')[0];

    const assignDto: AssignSubscriptionRequestDto = {
      subscriptionPlanId: formValue.subscriptionPlanId,
      startDate,
      period: formValue.period,
    };

    this.tenantService.assignSubscription(this.selectedTenant.id, assignDto)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.flash('success', this.translate.instant('tenants.responses.success.subscription_assigned'));
          this.subscriptionForm.markAsPristine();
          setTimeout(() => {
            this.isValidating = false;
            this.reload();
          }, 2000);
        },
        error: (err) => this.handleApiError(err)
      });
  }

  removeSubscription(): void {
    if (!this.selectedTenant) return;

    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'delete',
        iconColor: 'warn',
        title: this.translate.instant('tenants.list.dialog.remove_subscription_title'),
        message: this.translate.instant('tenants.list.dialog.remove_subscription_confirm', { name: this.selectedTenant.name }),
        confirmText: this.translate.instant('tenants.list.dialog.remove_confirm'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });

    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;

      this.isValidating = true;
      this.tenantService.removeSubscription(this.selectedTenant!.id)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => {
            this.flash('success', this.translate.instant('tenants.responses.success.subscription_removed'));
            setTimeout(() => {
              this.isValidating = false;
              this.reload();
            }, 2000);
          },
          error: (err) => this.handleApiError(err)
        });
    });
  }

  private handleApiError(error: any): void {
    const errorCode = error?.error?.code;
    if (errorCode && this.translate.instant(`tenants.responses.errors.${errorCode}`) !== `tenants.responses.errors.${errorCode}`) {
      this.flash('error', this.translate.instant(`tenants.responses.errors.${errorCode}`, error?.error?.params || {}));
    } else {
      this.flash('error', this.translate.instant('tenants.responses.errors.update_failed'));
    }
    this.isValidating = false;
    this.cdr.markForCheck();
  }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; }, 4000);
    }
  }

  dismissError(): void {
    this.errors = [];
  }

  cancel(): void {
    this.location.back();
  }

  ngOnDestroy(): void {
    // cleanup
  }

  private localeToEnum(locale: string | undefined): LocaleEnum {
    const map: Record<string, LocaleEnum> = {
      'en': LocaleEnum.EN,
      'en-US': LocaleEnum.EN,
      'fr': LocaleEnum.FR,
      'fr-FR': LocaleEnum.FR,
      'ar': LocaleEnum.AR,
      'ar-TN': LocaleEnum.AR,
    };
    return map[locale ?? ''] ?? LocaleEnum.FR;
  }
}