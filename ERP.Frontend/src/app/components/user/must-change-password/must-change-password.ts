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
import { PasswordService } from '../../../services/password.service';         // ← added
import { NotSameAsDirective } from '../../../util/NotSameAsDirective';
import { ChangeProfilePasswordRequestDto } from '../../../interfaces/AuthDto';
import { MatDialog } from '@angular/material/dialog';
import { ModalComponent } from '../../modal/modal';
import { HttpError } from '../../../interfaces/HttpError';
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
  styleUrl: './must-change-password.scss',
})
export class MustChangePasswordComponent implements OnInit {
  @ViewChild('passwordFormRef') passwordFormRef!: NgForm;

  private translate      = inject(TranslateService);
  private passwordService = inject(PasswordService);              // ← added

  mustChangePassword = false;
  errors: string[]   = [];
  successMessage: string | null = null;

  isLoading           = false;
  showCurrentPassword = false;
  showNewPassword     = false;

  passwordErrors:   string[] = [];
  passwordScore:    number   = 0;
  passwordStrength: string   = '';

  passwordForm: ChangeProfilePasswordRequestDto = {
    currentPassword: '',
    newPassword: '',
  };

  constructor(
    private authService: AuthService,
    private router: Router,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    private location: Location
  ) {}

  ngOnInit(): void {
    this.mustChangePassword = this.authService.getMustChangePassword();
  }

  // ── Password change handlers ──────────────────────────────────────────────

  onPasswordChange(): void {
    const result = this.passwordService.validatePassword(    // ← was checkPassword()
      this.passwordForm.newPassword,
      this.passwordForm.currentPassword,
    );
    this.passwordErrors   = result.errors;
    this.passwordScore    = result.score;
    this.passwordStrength = result.strength;

    // Keep notSameAs cross-validation in sync
    this.passwordFormRef?.controls?.['currentPassword']?.updateValueAndValidity();
  }

  onCurrentPasswordChange(): void {
    this.onPasswordChange();
    this.passwordFormRef?.controls?.['newPassword']?.updateValueAndValidity();
  }

  generatePassword(): void {
    this.passwordForm.newPassword = this.passwordService.generatePassword();  // ← was generatePassword()
    this.showNewPassword = true;
    this.onPasswordChange();
  }

  // ── Strength helpers — all delegated to PasswordService ──────────────────

  getScore(): number {
    return this.passwordService.getStrengthScore(this.passwordStrength);      // ← was inline map
  }

  getStrengthClass(): string {
    return this.passwordService.getStrengthClass(this.passwordStrength);      // ← was inline map
  }

  getStrengthLabel(): string {
    return this.passwordService.getStrengthLabel(this.passwordStrength);      // ← was inline translate.instant
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  onSubmit(): void {
    this.isLoading = true;
    this.authService.changeProfilePassword(this.passwordForm).subscribe({
      next: () => {
        this.isLoading = false;
        if (this.mustChangePassword) this.authService.clearMustChangePassword();

        this.dialog.open(ModalComponent, {
          width: '400px',
          data: {
            title:       this.translate.instant('USERS.PROFILE.PASSWORD_CHANGE_SUCCESS_TITLE'),
            message:     this.translate.instant('USERS.PROFILE.PASSWORD_CHANGE_SUCCESS_MESSAGE'),
            confirmText: this.translate.instant('USERS.PROFILE.UNDERSTOOD'),
            showCancel:  false,
            icon:        'info',
            iconColor:   'success',
          },
        });

        this.router.navigate(['/home']);
      },
      error: (error) => {
        this.isLoading = false;
        const err = error.error as HttpError;

        if (err.code === 'VALIDATION_ERROR' && err.errors) {
          this.flashErrors(Object.values(err.errors).flat());
        } else {
          this.flash('error', err.message
            ?? this.translate.instant('USERS.PROFILE.PASSWORD_CHANGE_FAILED'));
        }
        this.cdr.markForCheck();
      },
    });
  }

  goBack(){
    this.location.back();
  }
  // ── Auth ──────────────────────────────────────────────────────────────────

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
