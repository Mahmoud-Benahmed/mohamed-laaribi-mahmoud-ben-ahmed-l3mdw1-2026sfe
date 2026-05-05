import { PRIVILEGES } from './../../../services/auth/auth.service';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { AuthService } from '../../../services/auth/auth.service';
import { checkPassword, generatePassword } from '../../../util/PasswordUtil';
import { AuthUserGetResponseDto } from '../../../interfaces/AuthDto';
import { MatDialog } from '@angular/material/dialog';
import { ModalComponent } from '../../modal/modal';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HttpError } from '../../../interfaces/HttpError';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslatePipe
  ],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class ProfileComponent implements OnInit {
  private translate  = inject(TranslateService);
  private fb         = inject(FormBuilder);

  infoCollapsed    = false;
  accountCollapsed = false;

  userProfile: AuthUserGetResponseDto | null = null;
  isLoading  = true;
  isEditing  = false;
  isSaving   = false;
  authUserId: string | null = null;
  noDataChange = true;

  showCurrentPassword = false;
  showNewPassword     = false;

  passwordScore    = 0;
  passwordStrength = '';

  // ── Patterns (match backend) ──────────────────────────────────────────────
  // UpdateProfileDto: [EmailAddress][MaxLength(255)]
  // UpdateProfileDto: [MaxLength(100)][RegularExpression(RegexPatterns.FullName)]
  // ChangePasswordRequestDto: MinLength(8) MaxLength(128), must differ from current
  // AdminChangeProfileRequest: [Required][MinLength(8)][MaxLength(128)]

  readonly fullNamePattern = /^[\p{L}]+(\s[\p{L}]+)*$/u;
  readonly emailPattern    = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  // ── Forms ─────────────────────────────────────────────────────────────────
  editForm!:          FormGroup;
  passwordForm!:      FormGroup;
  adminPasswordForm!: FormGroup;

  constructor(
    private authService: AuthService,
    private dialog:      MatDialog,
    private route:       ActivatedRoute,
    private cdr:         ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.buildForms();
    this.loadProfile();
  }

  private buildForms(): void {
    this.editForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(this.fullNamePattern)]],
      email:    ['', [Validators.required, Validators.email, Validators.maxLength(255)]],
    });

    this.editForm.valueChanges.subscribe(() => this.checkChanges());

    // Own profile password form — cross-field: newPassword !== currentPassword
    this.passwordForm = this.fb.group({
      currentPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
      newPassword:     ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
    }, { validators: this.passwordsDifferValidator });

    // Admin password form
    this.adminPasswordForm = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
    });

    // Trigger strength check on change
    this.passwordForm.get('newPassword')?.valueChanges.subscribe(() => this.onPasswordChange());
    this.passwordForm.get('currentPassword')?.valueChanges.subscribe(() => this.onPasswordChange());
    this.adminPasswordForm.get('newPassword')?.valueChanges.subscribe(() => this.onPasswordChange());
  }

  private passwordsDifferValidator(group: AbstractControl): ValidationErrors | null {
    const current = group.get('currentPassword')?.value;
    const next    = group.get('newPassword')?.value;
    if (!current || !next) return null;
    return current === next ? { samePassword: true } : null;
  }

  // ── Profile load ──────────────────────────────────────────────────────────

  loadProfile(): void {
    const routeId  = this.route.snapshot.paramMap.get('authUserId');
    this.authUserId = routeId ?? this.authService.UserId;

    if (!this.authUserId) { this.isLoading = false; return; }

    if (this.authService.hasPrivilege(PRIVILEGES.USERS.VIEW_USERS) && this.authService.UserId !== this.authUserId) {
      this.authService.getById(this.authUserId).subscribe({
        next:  u  => { this.userProfile = u; this.stopLoading('isLoading'); },
        error: () => { this.stopLoading('isLoading'); this.showErrorDialog(this.translate.instant('USERS.PROFILE.LOAD_ERROR_TITLE'), this.translate.instant('USERS.PROFILE.LOAD_ERROR_MESSAGE')); }
      });
    } else {
      const cached = this.authService.UserProfile;
      if (cached) {
        this.userProfile = cached;
        this.isLoading   = false;
      } else {
        this.authService.getMe().subscribe({
          next:  u  => { this.userProfile = u; this.authService.setUserProfile(u); this.stopLoading('isLoading'); },
          error: () => { this.stopLoading('isLoading'); this.showErrorDialog(this.translate.instant('USERS.PROFILE.LOAD_ERROR_TITLE'), this.translate.instant('USERS.PROFILE.LOAD_ERROR_MESSAGE')); }
        });
      }
    }
  }

  // ── Edit profile ──────────────────────────────────────────────────────────

  toggleEditing(): void {
    if (this.isEditing) {
      this.cancelEditing();
    } else {
      if (!this.userProfile) return;
      this.editForm.patchValue({
        fullName: this.userProfile.fullName ?? '',
        email:    this.userProfile.email    ?? '',
      });
      this.noDataChange = true;
      this.isEditing    = true;
    }
  }

  cancelEditing():void{
      this.isEditing = false;
      this.editForm.reset();
  }

  checkChanges(): void {
    if (!this.userProfile) return;
    const { fullName, email } = this.editForm.value;
    this.noDataChange = fullName === this.userProfile.fullName && email === this.userProfile.email;
  }

  saveProfile(): void {
    if (this.editForm.invalid || !this.userProfile) return;
    this.isSaving = true;

    this.authService.update(this.userProfile.id, this.editForm.value).subscribe({
      next: updated => {
        this.userProfile = { ...updated, mustChangePassword: this.userProfile!.mustChangePassword, lastLoginAt: this.userProfile!.lastLoginAt };
        this.isEditing = false;
        this.stopLoading('isSaving');
        this.showSuccessDialog(this.translate.instant('SUCCESS.USER_UPDATED'));
        if (this.isOwnProfile) this.authService.setUserProfile(this.userProfile);
        else this.loadProfile();
      },
      error: err => {
        this.stopLoading('isSaving');
        this.showErrorDialog(this.translate.instant('USERS.PROFILE.UPDATE_ERROR_TITLE'), (err.error as HttpError).message);
      }
    });
  }

  // ── Password ──────────────────────────────────────────────────────────────

  changePassword(): void {
    const form = this.hasPrivilege ? this.adminPasswordForm : this.passwordForm;
    if (form.invalid) return;
    this.isSaving = true;

    const stop = () => { this.isSaving = false; this.cdr.markForCheck(); };
    const onSuccess = () => { stop(); this.showSuccessDialog(this.translate.instant('SUCCESS.PASSWORD_CHANGED')); form.reset(); };
    const onError   = (err: any) => { stop(); this.showErrorDialog(this.translate.instant('USERS.PROFILE.UPDATE_ERROR_TITLE'), (err.error as HttpError).message); };

    if (this.isOwnProfile) {
      this.authService.changeProfilePassword(this.passwordForm.value).subscribe({ next: onSuccess, error: onError });
    } else if (this.hasPrivilege) {
      this.authService.adminChangePassword(this.authUserId!, { newPassword: this.adminPasswordForm.value.newPassword }).subscribe({ next: onSuccess, error: onError });
    } else {
      stop();
    }
  }

  onPasswordChange(): void {
    const pwd     = this.hasPrivilege ? this.adminPasswordForm.get('newPassword')?.value : this.passwordForm.get('newPassword')?.value;
    const current = this.hasPrivilege ? null : this.passwordForm.get('currentPassword')?.value;
    const result  = checkPassword(pwd, current);
    this.passwordScore    = result.score;
    this.passwordStrength = result.strength;
  }

  generatePassword(): void {
    const pwd = generatePassword();
    if (this.hasPrivilege) this.adminPasswordForm.patchValue({ newPassword: pwd });
    else this.passwordForm.patchValue({ newPassword: pwd });
    this.showNewPassword = true;
    this.onPasswordChange();
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  get hasPrivilege(): boolean { return this.authService.hasPrivilege(PRIVILEGES.USERS.UPDATE_USER) && !this.isOwnProfile; }
  get isOwnProfile(): boolean { return this.selectedUserId === this.authService.UserId; }
  get canEditProfile(): boolean { return this.isOwnProfile || this.hasPrivilege; }
  get selectedUserId(): string | undefined { return this.userProfile?.id; }

  get initials(): string {
    const name  = this.userProfile?.fullName ?? this.userProfile?.email ?? '?';
    const words = name.split(' ').filter(w => w.length > 0);
    if (words.length === 0) return '?';
    if (words.length === 1) return words[0][0].toUpperCase();
    return (words[0][0] + words[words.length - 1][0]).toUpperCase();
  }

  get memberSince(): string {
    if (!this.userProfile?.createdAt) return '—';
    return new Date(this.userProfile.createdAt).toLocaleDateString(
      this.translate.currentLang === 'fr' ? 'fr-FR' : 'en-US',
      { year: 'numeric', month: 'long', day: 'numeric' }
    );
  }

  getStrengthClass(): string {
    return { weak: 'strength--weak', fair: 'strength--fair', strong: 'strength--strong', 'very strong': 'strength--very-strong' }[this.passwordStrength] ?? '';
  }

  getStrengthLabel(): string {
    return this.translate.instant(`VALIDATION.PASSWORD_${this.passwordStrength.toUpperCase().replace(/ /g, '_')}`);
  }

  stopLoading(type: 'isSaving' | 'isLoading'): void {
    if (type === 'isLoading') this.isLoading = false;
    if (type === 'isSaving')  this.isSaving  = false;
    this.cdr.markForCheck();
  }

  private showErrorDialog(title: string, message: string): void {
    this.dialog.open(ModalComponent, { width: '400px', data: { title, message, confirmText: this.translate.instant('COMMON.OK'), showCancel: false, icon: 'info', iconColor: 'warn' } });
  }

  private showSuccessDialog(message: string): void {
    this.dialog.open(ModalComponent, { width: '400px', data: { title: this.translate.instant('USERS.PROFILE.SUCCESS_TITLE'), message, confirmText: this.translate.instant('COMMON.OK'), showCancel: false, icon: 'check_circle', iconColor: 'success' } });
  }
}