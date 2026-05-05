import { ModalComponent } from './../../../modal/modal';
import { HttpError } from '../../../../interfaces/HttpError';
import { AdminChangeProfileRequest } from './../../../../interfaces/AuthDto';
import { AuthService } from '../../../../services/auth/auth.service';
import { PasswordService } from '../../../../services/password.service';        // ← added
import { ChangeDetectorRef, Component, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
  styleUrl: './change-password.scss',
})
export class ChangePasswordComponent implements OnInit {
  @ViewChild('passwordFormRef') passwordFormRef!: NgForm;

  private location= inject(Location);

  // ── Route context ─────────────────────────────────────────────────────────
  targetUserId: string | null = null;

  // ── UI state ──────────────────────────────────────────────────────────────
  isLoading       = false;
  showNewPassword = false;
  errors: string[] = [];

  // ── Password validation ───────────────────────────────────────────────────
  passwordErrors:   string[] = [];
  passwordScore     = 0;
  passwordStrength  = '';

  // ── Form ──────────────────────────────────────────────────────────────────
  adminForm: AdminChangeProfileRequest = { newPassword: '' };

  constructor(
    private authService: AuthService,
    public  router:      Router,
    private route:       ActivatedRoute,
    private dialog:      MatDialog,
    private cdr:         ChangeDetectorRef,
    private passwordService:PasswordService,
    private translate: TranslateService
  ) {}

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
        this.cdr.markForCheck();
        this.dialog
          .open(ModalComponent, {
            width: '400px',
            data: {
              title:       this.translate.instant('PASSWORD.RESET_SUCCESS_TITLE'),
              message:     this.translate.instant('PASSWORD.RESET_SUCCESS_MESSAGE'),
              confirmText: this.translate.instant('PASSWORD.GOT_IT'),
              showCancel:  false,
              icon:        'check_circle',
              iconColor:   'success',
            },
          })
          .afterClosed()
          .subscribe(() => this.router.navigate(['/users', this.targetUserId]));
      },
      error: (error) => {
        this.isLoading = false;
        const err = error.error as HttpError;
        if (err.code === 'VALIDATION_ERROR' && err.errors) {
          this.flashErrors(Object.values(err.errors).flat());
        } else {
          this.flash(err.message ?? this.translate.instant('PASSWORD.ERRORS.RESET_FAILED'));
        }
      },
    });
  }

  // ── Password validation ───────────────────────────────────────────────────

  // Admin flow: no current password context — pass null
  onPasswordChange(): void {
    const result = this.passwordService.validatePassword(   // ← was checkPassword()
      this.adminForm.newPassword,
      null,
    );
    this.passwordErrors   = result.errors;
    this.passwordScore    = result.score;
    this.passwordStrength = result.strength;
  }

  // ── Generate ──────────────────────────────────────────────────────────────

  generatePassword(): void {
    this.adminForm.newPassword = this.passwordService.generatePassword();   // ← was generatePassword()
    this.showNewPassword = true;
    this.onPasswordChange();
  }

  // ── Strength meter — all delegated to PasswordService ────────────────────

  getScore(): number {
    return this.passwordService.getStrengthScore(this.passwordStrength);    // ← was inline map
  }

  getStrengthClass(): string {
    return this.passwordService.getStrengthClass(this.passwordStrength);    // ← was inline map
  }

  getStrengthLabel(): string {
    return this.passwordService.getStrengthLabel(this.passwordStrength);    // ← was inline translate.instant
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  dismissError(): void { this.errors = []; }

  flash(msg: string): void {
    this.errors = [msg];
    this.cdr.markForCheck();
    setTimeout(() => { this.errors = []; this.cdr.markForCheck(); }, 4000);
  }

  flashErrors(messages: string[]): void {
    this.errors = messages;
    this.cdr.markForCheck();
    setTimeout(() => { this.errors = []; this.cdr.markForCheck(); }, 4000);
  }

  goBack(){
    this.location.back();
  }
}
