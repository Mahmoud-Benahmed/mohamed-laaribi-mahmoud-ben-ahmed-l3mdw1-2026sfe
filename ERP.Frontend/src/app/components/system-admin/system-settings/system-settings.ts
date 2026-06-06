import { CommonModule, Location } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { TenantResponseDto, SubscriptionPlanDto, UpdateTenantRequestDto, AssignSubscriptionRequestDto, SubscriptionPeriod, LocaleEnum, TimeZoneEnum, CurrencyEnum, TenantSettingsDto } from '../../../interfaces/TenantDto';
import { catchError, forkJoin, of, tap } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TenantService } from '../../../services/tenant/tenant.service';
import { SubscriptionPlanService } from '../../../services/tenant/subscription-plan.service';
import { ModalComponent } from '../../modal/modal';
import { RegexPatterns } from '../../../interfaces/RegexPatterns';
import { CurrencyConfigService } from '../../../services/currency-config.service';

@Component({
  selector: 'app-settings',
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
  templateUrl: './system-settings.html',
  styleUrl: './system-settings.scss',
})
export class SystemSettingsComponent implements OnInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);
  private location = inject(Location);

  // Translation prefix
  readonly templateTranslationKey = 'tenant.settings.';
  readonly errorsTranslationKey = 'tenant.settings.responses.errors.';
  readonly successTranslationKey = 'tenant.settings.responses.success.';

  // ── Forms ──────────────────────────────────────────────────────────────────
  tenantForm!: FormGroup;

  // ── State ──────────────────────────────────────────────────────────────────
  selectedTenant: TenantSettingsDto | null = null;
  tenantId: string | null = null;
  isValidating = false;
  loading = false;
  isEdit: boolean = false;

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
    public tenantService: TenantService,
    private fb: FormBuilder,
    private currencyConfig: CurrencyConfigService,
  ) {}

  ngOnInit(): void {
    this.isEdit = true;
    this.tenantId = this.authService.TenantId;

    if (!this.tenantId) {
      this.cancel();
      return;
    }

    this.buildForms();
    this.tenantForm.disable();

    this.reload();

    ['primaryColor', 'secondaryColor'].forEach(name => {
      this.tenantForm.get(name)?.valueChanges
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(() => this.syncColorPickers());
    });
  }

  private buildForms(): void {
    this.tenantForm = this.fb.group({
      name:           ['', [Validators.required, Validators.maxLength(200), Validators.pattern(RegexPatterns.safeText)]],
      email:          ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
      phone:          ['', [Validators.required, Validators.pattern(RegexPatterns.phone)]],
      subdomainSlug:  ['', [Validators.required, Validators.maxLength(100), Validators.pattern(RegexPatterns.subdomainSlug)]],
      address:        ['', [Validators.required, Validators.maxLength(200), Validators.pattern(RegexPatterns.safeText)]],
      logoUrl:        ['', [Validators.maxLength(500)]],
      primaryColor:   ['', [Validators.pattern(RegexPatterns.hexColor)]],
      secondaryColor: ['', [Validators.pattern(RegexPatterns.hexColor)]],
      currency: [CurrencyEnum.TND, Validators.required],
      locale: [LocaleEnum.FR, Validators.required],
      timezone: [TimeZoneEnum.AFRICA_TUNIS, Validators.required],
    });
  }

  reload(): void {
    if (!this.tenantId) return;

    this.loading = true;

    this.tenantService.getTenantSettings(this.tenantId).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (tenant) => {
          this.selectedTenant = tenant;
          this.populateFormFromTenant(tenant);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (err: HttpErrorResponse) => {
          this.loading = false;
          const errorMsg = err.error?.message || this.translate.instant(`${this.errorsTranslationKey}update_failed`);
          this.flash('error', errorMsg);
          this.cancel();
        }
      });
  }

  private populateFormFromTenant(tenant: TenantSettingsDto): void {
    this.tenantForm.patchValue({
      name: tenant.name,
      subdomainSlug: tenant.slug,
      email: tenant.email,
      phone: tenant.phone,
      address: tenant.address,
      currency: tenant.currency || CurrencyEnum.TND,
      locale: this.localeToEnum(tenant.locale),
      timezone: tenant.timezone || TimeZoneEnum.AFRICA_TUNIS,
      logoUrl: tenant.logoUrl,
      primaryColor: tenant.primaryColor,
      secondaryColor: tenant.secondaryColor
    });
    this.tenantForm.markAsPristine();
    this.syncColorPickers();
  }

  private syncColorPickers(): void {
    (['primaryColor', 'secondaryColor'] as const).forEach(controlName => {
      const value = this.tenantForm.get(controlName)?.value;
      if (!value) return;
      const pickers = document.querySelectorAll<HTMLInputElement>(
        `input[type="color"][formControlName="${controlName}"]`
      );
      pickers.forEach(el => (el.value = value));
    });
  }

  get canSubmit(): boolean {
    if (this.tenantForm.invalid) return false;
    if (this.isValidating) return false;
    return true;
  }

  getSubmitButtonTooltip(): string {
    if (this.isValidating) return this.translate.instant('common.processing');
    if (this.tenantForm.invalid) return this.translate.instant(`${this.templateTranslationKey}validation.required`);
    return '';
  }

  updateTenant(): void {
    if (this.tenantForm.invalid) {
      this.flash('error', this.translate.instant(`${this.templateTranslationKey}validation.required`));
      return;
    }

    if (!this.selectedTenant) {
      this.flash('error', this.translate.instant(`${this.errorsTranslationKey}update_failed`));
      return;
    }

    this.isValidating = true;
    this.loading = true;

    const formValue = this.tenantForm.value;
    const updateDto: UpdateTenantRequestDto = {
      name: formValue.name,
      email: formValue.email,
      phone: formValue.phone,
      address: formValue.address,
      currency: formValue.currency,
      locale: formValue.locale,
      timezone: formValue.timezone,
      primaryColor: formValue.primaryColor,
      secondaryColor: formValue.secondaryColor,
      logoUrl: formValue.logoUrl
    };

    this.tenantService.updateTenant(this.tenantId!, updateDto)
      .pipe(tap(() => this.tenantService.patchSettings(updateDto)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.selectedTenant = {
            name: updated.name,
            address: updated.address,
            currency: updated.currency,
            email: updated.email,
            locale: updated.locale,
            phone: updated.phone,
            primaryColor: updated.primaryColor,
            secondaryColor: updated.secondaryColor,
            slug: updated.subdomainSlug,
            timezone: updated.timezone,
            logoUrl: updated.logoUrl
          };
          this.flash('success', this.translate.instant(`${this.templateTranslationKey}update_success`));
          this.currencyConfig.save(updated.currency, updated.locale);
          this.isEdit = false;
          setTimeout(() => {
            this.isValidating = false;
            this.loading = false;
            this.cdr.markForCheck();
          }, 2000);
        },
        error: (err: HttpErrorResponse) => {
          const errorMsg = err.error?.message || this.translate.instant(`${this.templateTranslationKey}update_failed`);
          this.flash('error', errorMsg);
          this.isValidating = false;
          this.loading = false;
          this.cdr.markForCheck();
        }
      });
  }

  edit(): void {
    this.isEdit = !this.isEdit;
    if (this.isEdit) {
      this.tenantForm.disable();
      this.populateFormFromTenant(this.selectedTenant!);
    } else {
      this.tenantForm.enable();
    }
  }

  fillColorTextInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const controlName = input.getAttribute('formControlName') as string;
    this.tenantForm.get(controlName)?.setValue(input.value, { emitEvent: false });
  }

  fillColorInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const controlName = input.getAttribute('formControlName') as string;
    const value = input.value.trim();
    if (RegexPatterns.hexColor.test(value)) {
      this.tenantForm.get(controlName)?.setValue(value, { emitEvent: false });
    }
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

  ngOnDestroy(): void {}

  private localeToEnum(locale: string | undefined): LocaleEnum {
    const map: Record<string, LocaleEnum> = {
      'en': LocaleEnum.EN,
      'en-US': LocaleEnum.EN,
      'fr': LocaleEnum.FR,
      'fr-FR': LocaleEnum.FR
    };
    return map[locale ?? ''] ?? LocaleEnum.FR;
  }
}