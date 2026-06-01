import { AuthService } from '../../services/auth/auth.service';
import { Component, OnInit, OnDestroy, ChangeDetectorRef, ViewEncapsulation, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { AuthUserGetResponseDto } from '../../interfaces/AuthDto';
import { ModalComponent } from '../modal/modal';
import { environment } from '../../environment';
import { UserSettingsService } from '../../services/user-settings.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { map, Subscription, switchMap, take, tap } from 'rxjs';
import { RegexPatterns } from '../../interfaces/RegexPatterns';
import { TenantService } from '../../services/tenant/tenant.service';

@Component({
  selector: 'app-login',
  imports: [
    CommonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    TranslatePipe,
    ReactiveFormsModule,
    RouterLink
],
  templateUrl: './login.html',
  styleUrl: './login.scss',
  encapsulation: ViewEncapsulation.None
})
export class LoginComponent implements OnInit, OnDestroy {
  private langSub?: Subscription;

  readonly year: number = new Date().getFullYear();
  userProfile: AuthUserGetResponseDto | null = null;

  showPassword = false;
  isLoading = false;
  errorMessage: string | null = null;

  loginForm!: FormGroup;

  constructor(
    private router: Router,
    private authService: AuthService,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    public userSettings: UserSettingsService,
    public translate: TranslateService,
    private fb: FormBuilder,
    private tenantService: TenantService
  ) {
    this.loginForm = this.fb.group({
      login:       ['', [Validators.required, Validators.pattern(RegexPatterns.login)]],
      password:    ['', [Validators.required, Validators.minLength(8)]],
    });
  }

  ngOnInit(): void {
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/home']);
    }

    this.langSub = this.translate.onLangChange.subscribe(() => {
      this.cdr.detectChanges();
    });

    this.cdr.detectChanges();
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  dismissError(): void {
    this.errorMessage = null;
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading = true;
    this.errorMessage = null;

    const {login, password}= this.loginForm.value;

    this.authService.login({ login, password }).pipe(
      switchMap(response =>
        this.authService.getMe().pipe(
          tap(user => {
            this.authService.setUserProfile(user);
            this.userProfile = user;
          }),
          map(() => response)
        )
      ),
      switchMap(response =>
        this.tenantService.loadTenantSettings(this.authService.TenantId!).pipe(
          map(() => response)   // keep response flowing for next()
        )
      ),
      take(1)
    ).subscribe({
      next: (response) => {
        this.isLoading = false;

        if (response.mustChangePassword && environment.production) {
          this.router.navigate(['/must-change-password']);
          return;
        }

        this.router.navigate(['/home']);
      },
      error: (error) => {
        this.stopLoading();

        // Handle specific status codes
        if (error.status === 0) {
          this.showErrorDialog('SERVER_UNREACHABLE', error);
          return;
        }
        if (error.status === 403) {
          this.showErrorDialog('ACCESS_DENIED', error);
          return;
        }
        if (error.status === 429) {
          this.showErrorDialog('RATE_LIMIT', error);
          return;
        }

        const code = error.error?.code ?? 'UNKNOWN';
        this.showErrorDialog(code, error);
      }
    });
  }

  private showErrorDialog(code: string, error: any): void {
    const key = `ERRORS.${code}`;
    const translatedMsg = this.translate.instant(key);

    // If translation key doesn't exist, fall back to error message from server
    const displayMessage = translatedMsg === key
      ? (error.error?.message ?? translatedMsg)
      : translatedMsg;

    // Determine dialog title based on error type
    let titleKey = 'DIALOG.ACCESS_DENIED';
    if (code === 'SERVER_UNREACHABLE') titleKey = 'DIALOG.SERVER_UNREACHABLE';
    if (code === 'RATE_LIMIT') titleKey = 'DIALOG.RATE_LIMIT';
    if (code === 'AUTH_003') titleKey = 'DIALOG.ACCOUNT_DEACTIVATED';
    if (code === 'AUTH_019') titleKey = 'DIALOG.SESSION_EXPIRED';

    this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant(titleKey),
        message:     displayMessage,
        confirmText: this.translate.instant('DIALOG.OK'),
        showCancel:  false,
        icon:        'dangerous',
        iconColor:   'danger'
      }
    });
  }

  stopLoading(): void {
    this.isLoading = false;
    this.cdr.markForCheck();
  }

  ngOnDestroy(): void {
    this.langSub?.unsubscribe();
  }
}
