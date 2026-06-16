import { ChangeDetectorRef, Component, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../../services/auth/auth.service';
import { PasswordService } from '../../../services/password.service';
import { NotSameAsDirective } from '../../../util/NotSameAsDirective';
import { ChangeProfilePasswordRequestDto } from '../../../interfaces/AuthDto';
import { MatDialog } from '@angular/material/dialog';
import { ModalComponent } from '../../modal/modal';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-must-change-password',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    NotSameAsDirective,
    TranslatePipe
  ],
  templateUrl: './must-change-password.html',
  styleUrls: ['./must-change-password.scss'],
})
export class MustChangePasswordComponent implements OnInit {
  @ViewChild('passwordFormRef') passwordFormRef!: NgForm;

  public authService = inject(AuthService);
  private translate = inject(TranslateService);
  private passwordService = inject(PasswordService);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private cdr = inject(ChangeDetectorRef);
  private location = inject(Location);

  // Translation prefix
  readonly templateTranslationKey = 'auth.profile.changePassword.';

  mustChangePassword = false;
  errors: string[] = [];
  successMessage: string | null = null;

  isLoading = false;
  showCurrentPassword = false;
  showNewPassword = false;

  passwordErrors: string[] = [];
  passwordStrength = '';

  passwordForm: ChangeProfilePasswordRequestDto = {
    currentPassword: '',
    newPassword: '',
  };

  ngOnInit(): void {
    this.mustChangePassword = this.authService.getMustChangePassword();
  }

  // ── Password change handlers ──────────────────────────────────────────────

  onPasswordChange(): void {
    const result = this.passwordService.validatePassword(
      this.passwordForm.newPassword,
      this.passwordForm.currentPassword,
    );
    // Translate each validation error (if it's a key, translate it)
    this.passwordErrors = result.errors.map(err => this.translateErrorMessage(err));
    this.passwordStrength = result.strength;

    // Keep notSameAs cross-validation in sync
    this.passwordFormRef?.controls?.['currentPassword']?.updateValueAndValidity();
  }

  onCurrentPasswordChange(): void {
    this.onPasswordChange();
    this.passwordFormRef?.controls?.['newPassword']?.updateValueAndValidity();
  }

  generatePassword(): void {
    this.passwordForm.newPassword = this.passwordService.generatePassword();
    this.showNewPassword = true;
    this.onPasswordChange();
  }

  // ── Strength helpers ──────────────────────────────────────────────────────

  getScore(): number {
    return this.passwordService.getStrengthScore(this.passwordStrength);
  }

  getStrengthClass(): string {
    return this.passwordService.getStrengthClass(this.passwordStrength);
  }

  getStrengthLabel(): string {
    return this.passwordService.getStrengthLabel(this.passwordStrength);
  }

  // ── Translate a single error message (fallback for validation strings) ────

  private translateErrorMessage(message: string): string {
    if (message && message.match(/^[A-Z0-9_]+$/)) {
      const translated = this.translate.instant(message);
      if (translated !== message) return translated;
      const withPrefix = this.translate.instant(`VALIDATION.${message}`);
      if (withPrefix !== `VALIDATION.${message}`) return withPrefix;
    }
    return message;
  }

  // ── Submit ─────────────────────────────────────────────────────────────────

  onSubmit(): void {
    if (this.passwordFormRef.invalid || this.passwordErrors.length > 0) return;
    this.isLoading = true;

    this.authService.changeProfilePassword(this.passwordForm).subscribe({
      next: () => {
        this.isLoading = false;
        if (this.mustChangePassword) this.authService.clearMustChangePassword();

        this.dialog.open(ModalComponent, {
          width: '400px',
          data: {
            title: this.translate.instant(`${this.templateTranslationKey}success_dialog.title`),
            message: this.translate.instant(`${this.templateTranslationKey}success_dialog.message`),
            confirmText: this.translate.instant(`${this.templateTranslationKey}success_dialog.button`),
            showCancel: false,
            icon: 'check_circle',
            iconColor: 'success',
          },
        });

        this.router.navigate(['/home']);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading = false;
        // The interceptor already provides a translated message in err.error?.message
        const errorMessage = err.error?.message || this.translate.instant(`${this.templateTranslationKey}errors.change_failed`);
        this.flash('error', errorMessage);
        this.cdr.markForCheck();
      },
    });
  }

  goBack(): void {
    this.location.back();
  }

  logout(): void {
    this.authService.logout();
  }

  // ── Feedback ──────────────────────────────────────────────────────────────

  dismissError(): void { this.errors = []; }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
    } else {
      this.errors = [msg];
    }
    this.cdr.markForCheck();
    setTimeout(() => {
      this.successMessage = null;
      this.errors = [];
      this.cdr.markForCheck();
    }, 3000);
  }

  flashErrors(messages: string[]): void {
    this.errors = messages;
    this.cdr.markForCheck();
    setTimeout(() => { this.errors = []; this.cdr.markForCheck(); }, 4000);
  }
}