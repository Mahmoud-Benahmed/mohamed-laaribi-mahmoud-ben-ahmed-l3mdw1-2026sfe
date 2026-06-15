import { ModalComponent } from './../../../modal/modal';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminChangeProfileRequest } from './../../../../interfaces/AuthDto';
import { AuthService } from '../../../../services/auth/auth.service';
import { PasswordService } from '../../../../services/password.service';
import { ChangeDetectorRef, Component, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './change-password.html',
  styleUrls: ['./change-password.scss'],
})
export class ChangePasswordComponent implements OnInit {
  @ViewChild('passwordFormRef') passwordFormRef!: NgForm;

  private location = inject(Location);
  private translate = inject(TranslateService);
  private passwordService = inject(PasswordService);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  public router = inject(Router);
  private dialog = inject(MatDialog);
  private cdr = inject(ChangeDetectorRef);

  // Translation prefixes
  readonly templateTranslationKey = 'auth.changePassword.';
  readonly responseSuccessTranslationKey = 'auth.responses.success.';
  readonly responseErrorsTranslationKey = 'auth.responses.errors.';

  // ── Route context ─────────────────────────────────────────────────────────
  targetUserId: string | null = null;

  // ── UI state ──────────────────────────────────────────────────────────────
  isLoading = false;
  showNewPassword = false;
  errors: string[] = [];

  // ── Password validation ───────────────────────────────────────────────────
  passwordErrors: string[] = [];
  passwordStrength = '';

  // ── Form ──────────────────────────────────────────────────────────────────
  adminForm: AdminChangeProfileRequest = { newPassword: '' };

  ngOnInit(): void {
    this.targetUserId = this.route.snapshot.paramMap.get('authUserId');

    if (!this.targetUserId || this.targetUserId === this.authService.UserId) {
      this.router.navigate(['/change-password']);
    }
  }

  // ── Submission ────────────────────────────────────────────────────────────

  onSubmit(): void {
    if (this.passwordFormRef.invalid || this.passwordErrors.length > 0) return;
    this.isLoading = true;

    this.authService.adminChangePassword(this.targetUserId!, this.adminForm).subscribe({
      next: () => {
        this.isLoading = false;
        this.dialog
          .open(ModalComponent, {
            width: '400px',
            data: {
              title: this.translate.instant(`${this.responseSuccessTranslationKey}reset_success_title`),
              message: this.translate.instant(`${this.responseSuccessTranslationKey}reset_success_message`),
              confirmText: this.translate.instant(`${this.responseSuccessTranslationKey}got_it`),
              showCancel: false,
              icon: 'check_circle',
              iconColor: 'success',
            },
          })
          .afterClosed()
          .subscribe(() => this.router.navigate(['/users', this.targetUserId]));
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading = false;
        const errorMessage = err.error?.message || this.translate.instant(`${this.responseErrorsTranslationKey}reset_failed`);
        this.flash(errorMessage);
      },
    });
  }

  // ── Password validation ───────────────────────────────────────────────────

  onPasswordChange(): void {
    const result = this.passwordService.validatePassword(this.adminForm.newPassword, null);
    this.passwordErrors = result.errors;
    this.passwordStrength = result.strength;
  }

  generatePassword(): void {
    this.adminForm.newPassword = this.passwordService.generatePassword();
    this.showNewPassword = true;
    this.onPasswordChange();
  }

  getScore(): number {
    return this.passwordService.getStrengthScore(this.passwordStrength);
  }

  getStrengthClass(): string {
    return this.passwordService.getStrengthClass(this.passwordStrength);
  }

  getStrengthLabel(): string {
    return this.passwordService.getStrengthLabel(this.passwordStrength);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  flash(msg: string): void {
    this.errors = [msg];
    this.cdr.markForCheck();
    setTimeout(() => {
      this.errors = [];
      this.cdr.markForCheck();
    }, 4000);
  }

  goBack(): void {
    this.location.back();
  }
}