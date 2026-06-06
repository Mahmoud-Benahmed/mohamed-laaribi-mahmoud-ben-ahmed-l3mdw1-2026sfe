import { CommonModule, Location } from '@angular/common';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { TenantResponseDto, SubscriptionPlanDto, AssignSubscriptionRequestDto, SubscriptionPeriod } from '../../../interfaces/TenantDto';
import { catchError, firstValueFrom, forkJoin, map, Observable, of, tap } from 'rxjs';
import { HttpError } from '../../../interfaces/HttpError';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ModalComponent } from '../../modal/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LoadingOverlayComponent } from "../../loading-overlay/loading-overlay";
import { SubscriptionPlanService } from '../../../services/tenant/subscription-plan.service';
import { TenantService } from '../../../services/tenant/tenant.service';

@Component({
  selector: 'app-tenants-view',
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
    RouterLink,
    LoadingOverlayComponent
  ],
  templateUrl: './view.html',
  styleUrl: './view.scss',
})
export class ViewTenantComponent implements OnInit, OnDestroy {
  private themeObserver: MutationObserver | null = null;
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);
  private location = inject(Location);

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null;

  selectedTenant: TenantResponseDto | null = null;
  tenantIdFromRoute: string | null = null;
  subscriptionPlan: SubscriptionPlanDto | null = null;
  loading = false;


  subscriptionForm!: FormGroup;
  isValidating = false;
  subscriptionPlans: SubscriptionPlanDto[] = [];
  readonly SubscriptionPeriod = SubscriptionPeriod;


  readonly PRIVILEGES = PRIVILEGES;

  constructor(
    public authService: AuthService,
    private tenantService: TenantService,
    private planService: SubscriptionPlanService,
    private route: ActivatedRoute,
    private router: Router,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    if (!this.authService.hasPrivilege(PRIVILEGES.TENANTS.VIEW_TENANTS)) {
      this.cancel();
      return;
    }

    this.tenantIdFromRoute = this.route.snapshot.paramMap.get('id');

    if (!this.tenantIdFromRoute) {
      this.cancel();
      return;
    }

    this.reload();
  }

  reload(): void {
    if (!this.tenantIdFromRoute) return;

    this.loading = true;

    forkJoin({
      tenant: this.tenantService.getTenantById(this.tenantIdFromRoute),
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ tenant }) => {
          this.selectedTenant = tenant;

          // Load subscription plan if exists
          if (tenant.subscription?.plan?.id) {
            this.planService.getPlanById(tenant.subscription.plan.id)
              .pipe(takeUntilDestroyed(this.destroyRef))
              .subscribe({
                next: (plan) => {
                  this.subscriptionPlan = plan;
                  this.loading = false;
                  this.cdr.markForCheck();
                },
                error: () => {
                  this.loading = false;
                  this.cdr.markForCheck();
                }
              });
          } else {
            this.loading = false;
            this.cdr.markForCheck();
          }
        },
        error: (err) => {
          this.loading = false;
          const errorMsg = (err.error as HttpError)?.message
            ?? this.translate.instant('TENANTS.ERRORS.LOAD_FAILED');
          this.flash('error', errorMsg);
          this.cancel();
        }
      });
  }

  updateTenant(tenant: TenantResponseDto): void {
    this.router.navigate(['/admin/tenants', tenant.id, 'edit']);
  }

  suspend(tenant: TenantResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'pause_circle',
        iconColor: 'warn',
        title: this.translate.instant('TENANTS.DIALOG.SUSPEND_TENANT_TITLE'),
        message: this.translate.instant('TENANTS.DIALOG.SUSPEND_TENANT_MESSAGE', { name: tenant.name }),
        confirmText: this.translate.instant('TENANTS.DIALOG.SUSPEND_CONFIRM'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });
    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;
      this.tenantService.suspendTenant(tenant.id).subscribe({
        next: () => {
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.SUSPENDED'));
          this.reload();
        },
        error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.SUSPEND_FAILED')),
      });
    });
  }

  activate(tenant: TenantResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'play_circle',
        iconColor: 'success',
        title: this.translate.instant('TENANTS.DIALOG.ACTIVATE_TENANT_TITLE'),
        message: this.translate.instant('TENANTS.DIALOG.ACTIVATE_TENANT_MESSAGE', { name: tenant.name }),
        confirmText: this.translate.instant('TENANTS.DIALOG.ACTIVATE_CONFIRM'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });
    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;
      this.tenantService.activateTenant(tenant.id).subscribe({
        next: () => {
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.ACTIVATED'));
          this.reload();
        },
        error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.ACTIVATE_FAILED')),
      });
    });
  }

  delete(tenant: TenantResponseDto): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '420px',
      data: {
        icon: 'delete',
        iconColor: 'warn',
        title: this.translate.instant('TENANTS.DIALOG.DELETE_TENANT_TITLE'),
        message: this.translate.instant('TENANTS.DIALOG.DELETE_TENANT_MESSAGE', { name: tenant.name }),
        confirmText: this.translate.instant('TENANTS.DIALOG.DELETE_CONFIRM'),
        cancelText: this.translate.instant('common.cancel'),
        showCancel: true,
      },
    });
    dialogRef.afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed) return;
      this.tenantService.deleteTenant(tenant.id).subscribe({
        next: () => {
          this.flash('success', this.translate.instant('TENANTS.SUCCESS.DELETED'));
          setTimeout(() => {
            this.cancel();
          }, 2000);
        },
        error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.DELETE_FAILED')),
      });
    });
  }

  restore(tenant: TenantResponseDto): void {
    this.tenantService.restoreTenant(tenant.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('TENANTS.SUCCESS.RESTORED'));
        setTimeout(() => {
          this.cancel();
        }, 2000);
      },
      error: () => this.flash('error', this.translate.instant('TENANTS.ERRORS.RESTORE_FAILED')),
    });
  }

  // subscription
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

  statusClass(tenant: TenantResponseDto): Record<string, boolean> {
    return {
      'badge--green': tenant.isActive && !tenant.isDeleted,
      'badge--amber': !tenant.isActive && !tenant.isDeleted,
      'badge--grey': tenant.isDeleted,
    };
  }

  getStatusLabel(tenant: TenantResponseDto): string {
    if (tenant.isDeleted) return 'TENANTS.STATUS.DELETED';
    return tenant.isActive ? 'TENANTS.STATUS.ACTIVE' : 'TENANTS.STATUS.SUSPENDED';
  }

  getPlanPrice(planId: string, period: SubscriptionPeriod): string {
    const plan = this.subscriptionPlans.find(p => p.id === planId);
    if (!plan) return '—';
    const price = period === SubscriptionPeriod.MONTH ? plan.monthlyPrice : plan.yearlyPrice;
    return `${price.toFixed(2)} ${this.selectedTenant?.currency}`;
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
    this.themeObserver?.disconnect();
  }
}