import { CommonModule, Location } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { TenantResponseDto, SubscriptionPlanDto, UpdateTenantRequestDto, AssignSubscriptionRequestDto, SubscriptionPeriod } from '../../../interfaces/TenantDto';
import { catchError, forkJoin, of } from 'rxjs';
import { HttpError } from '../../../interfaces/HttpError';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TenantService } from '../../../services/tenant/tenant.service';
import { SubscriptionPlanService } from '../../../services/tenant/subscription-plan.service';
import { ModalComponent } from '../../modal/modal';

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

  constructor(
    public authService: AuthService,
    private tenantService: TenantService,
    private planService: SubscriptionPlanService,
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    console.log("reached edit component");
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
      name: ['', [Validators.required, Validators.minLength(2)]],
      subdomainSlug: ['', [Validators.required, Validators.minLength(3), Validators.pattern(/^[a-z0-9-]+$/)]],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', [Validators.required, Validators.minLength(8)]],
      address: ['', Validators.required],
      currency: ['TND', Validators.required],
      locale: ['en', Validators.required],
      timezone: ['UTC', Validators.required],
    });

    this.subscriptionForm = this.fb.group({
      subscriptionPlanId: ['', Validators.required],
      period: [SubscriptionPeriod.MONTH, Validators.required],
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
          const errorMsg = (err.error as HttpError)?.message ?? this.translate.instant('TENANTS.ERRORS.LOAD_FAILED');
          this.flash('error', errorMsg);
          this.cancel();
        }
      });
  }

  private populateFormFromTenant(tenant: TenantResponseDto): void {
    this.tenantForm.patchValue({
      name: tenant.name,
      subdomainSlug: tenant.subdomainSlug,
      email: tenant.email,
      phone: tenant.phone,
      address: tenant.address,
      currency: tenant.currency || 'TND',
      locale: tenant.locale || 'en',
      timezone: tenant.timezone || 'UTC',
    });

    if (tenant.subscription?.plan?.id) {
      this.subscriptionForm.patchValue({
        subscriptionPlanId: tenant.subscription.plan.id,
        period: tenant.subscription.period || SubscriptionPeriod.MONTH,
      });
    }

    this.tenantForm.markAsPristine();
    this.subscriptionForm.markAsPristine();
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  getPlanLabel(planId: string): string {
    const plan = this.subscriptionPlans.find(p => p.id === planId);
    return plan ? `${plan.name} (${plan.code})` : '';
  }

  getPlanPrice(planId: string, period: SubscriptionPeriod): string {
    const plan = this.subscriptionPlans.find(p => p.id === planId);
    if (!plan) return '—';
    const price = period === SubscriptionPeriod.MONTH ? plan.monthlyPrice : plan.yearlyPrice;
    return `${price.toFixed(2)} ${this.selectedTenant?.currency}`;
  }

  get canSubmit(): boolean {
    if (this.tenantForm.invalid) return false;
    if (this.isValidating) return false;
    return true;
  }

  getSubmitButtonTooltip(): string {
    if (this.isValidating) return this.translate.instant('COMMON.PROCESSING');
    if (this.tenantForm.invalid) return this.translate.instant('VALIDATION.REQUIRED');
    return '';
  }

  // ── CRUD actions ───────────────────────────────────────────────────────────

  updateTenant(): void {
    if (this.tenantForm.invalid) {
      this.flash('error', this.translate.instant('VALIDATION.REQUIRED'));
      return;
    }

    if (!this.selectedTenant) {
      this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_FAILED'));
      return;
    }

    this.isValidating = true;

    const formValue = this.tenantForm.value;

    const updateDto: UpdateTenantRequestDto = {
      name: formValue.name,
      subdomainSlug: formValue.subdomainSlug,
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
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.UPDATED'));

          setTimeout(() => {
            this.isValidating = false;
            this.cancel();
          }, 2000);
        },
        error: (err) => {
          const errorMsg = (err.error as HttpError)?.message ?? this.translate.instant('TENANTS.ERRORS.UPDATE_FAILED');
          this.flash('error', errorMsg);
          this.isValidating = false;
          this.cdr.markForCheck();
        }
      });
  }

  assignSubscription(): void {
    if (this.subscriptionForm.invalid) {
      this.flash('error', this.translate.instant('VALIDATION.REQUIRED'));
      return;
    }

    if (!this.selectedTenant) {
      this.flash('error', this.translate.instant('TENANTS.ERRORS.LOAD_FAILED'));
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
        next: (response) => {
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.SUBSCRIPTION_ASSIGNED'));
          this.subscriptionForm.markAsPristine();

          setTimeout(() => {
            this.isValidating = false;
            this.reload();
          }, 2000);
        },
        error: (err) => {
          const errorMsg = (err.error as HttpError)?.message ?? this.translate.instant('TENANTS.ERRORS.ASSIGN_SUBSCRIPTION_FAILED');
          this.flash('error', errorMsg);
          this.isValidating = false;
          this.cdr.markForCheck();
        }
      });
  }

  removeSubscription(): void {
    if (!this.selectedTenant) return;

    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'delete',
        iconColor: 'warn',
        title: this.translate.instant('TENANTS.DIALOG.REMOVE_SUBSCRIPTION_TITLE'),
        message: this.translate.instant('TENANTS.DIALOG.REMOVE_SUBSCRIPTION_CONFIRM', { name: this.selectedTenant.name }),
        confirmText: this.translate.instant('TENANTS.DIALOG.REMOVE_CONFIRM'),
        cancelText: this.translate.instant('COMMON.CANCEL'),
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
            this.flash('success', this.translate.instant('TENANTS.SUCCESS.SUBSCRIPTION_REMOVED'));
            setTimeout(() => {
              this.isValidating = false;
              this.reload();
            }, 2000);
          },
          error: (err) => {
            const errorMsg = (err.error as HttpError)?.message ?? this.translate.instant('TENANTS.ERRORS.REMOVE_SUBSCRIPTION_FAILED');
            this.flash('error', errorMsg);
            this.isValidating = false;
            this.cdr.markForCheck();
          }
        });
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

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
}