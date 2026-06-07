import { Component, OnDestroy, inject, ViewChild, computed, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../../services/auth/auth.service';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { RegisterRequestDto, RoleResponseDto } from '../../../../interfaces/AuthDto';
import { HttpErrorResponse } from '@angular/common/http';
import { RoleService } from '../../../../services/auth/roles.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PasswordService } from '../../../../services/password.service';
import { RegexPatterns } from '../../../../interfaces/RegexPatterns';
import { ModalComponent } from '../../../modal/modal';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatProgressSpinner,
    TranslatePipe
  ],
  templateUrl: './register.html',
  styleUrls: ['./register.scss']
})
export class RegisterComponent implements OnDestroy {
  private location = inject(Location);
  private translate = inject(TranslateService);
  private passwordService = inject(PasswordService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private authService = inject(AuthService);
  private roleService = inject(RoleService);
  private dialog = inject(MatDialog);

  // Translation prefixes
  readonly templateTranslationKey = 'auth.register.';
  readonly responseSuccessTranslationKey = 'auth.responses.success.';
  readonly confirmationsTranslationKey = 'auth.confirmations.';

  form!: FormGroup;
  formValue = signal<any>(null);
  login = computed(() => this.formValue()?.login ?? '');
  fullName = computed(() => this.formValue()?.fullName ?? '');
  email = computed(() => this.formValue()?.email ?? '');
  roleId = computed(() => this.formValue()?.roleId ?? null);
  password = computed(() => this.formValue()?.password ?? '');

  showPassword = false;
  isLoading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  roles: RoleResponseDto[] = [];
  passwordErrors: string[] = [];
  passwordStrength: string = '';

  constructor() {
    this.initForm();
    this.form.valueChanges.subscribe(val => {
      this.formValue.set(val);
    });
  }

  ngOnInit(): void {
    this.roleService.getAll().subscribe({
      next: (roles) => this.roles = roles,
      error: (err: HttpErrorResponse) => {
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.INTERNAL_ERROR');
        this.flash('error', errorMessage);
      }
    });
  }

  private initForm(): void {
    this.form = this.fb.group({
      fullName: ['', [Validators.required,Validators.minLength(5), Validators.pattern(RegexPatterns.alpha)]],
      login: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(50), Validators.pattern(RegexPatterns.login)]],
      email: ['', [Validators.required, Validators.email, Validators.pattern(RegexPatterns.email)]],
      roleId: [null, Validators.required],
      password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]]
    });
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.sanitizeForm();

    const originalLogin = this.form.get('login')?.value;
    this.sanitizeLogin();
    const loginChanged = this.form.get('login')?.value !== originalLogin;

    if (loginChanged) {
      const dialogRef = this.dialog.open(ModalComponent, {
        width: '400px',
        data: {
          title: this.translate.instant(`${this.confirmationsTranslationKey}login_change_warning.title`),
          message: this.translate.instant(`${this.confirmationsTranslationKey}login_change_warning.message`, { login: this.login() }),
          confirmText: this.translate.instant(`${this.confirmationsTranslationKey}login_change_warning.confirm_text`),
          showCancel: true,
          icon: 'check_circle',
          iconColor: 'warn'
        }
      });
      dialogRef.afterClosed().subscribe(result => {
        if (result) {
          this.sanitizeLogin();
          this.checkAndRegister();
        } else {
          this.stopLoading();
        }
      });
    } else {
      this.checkAndRegister();
    }
  }

  private checkAndRegister(): void {
    forkJoin({
      loginExists: this.authService.existsByLogin(this.login()),
      emailExists: this.authService.existsByEmail(this.email())
    }).subscribe({
      next: ({ loginExists, emailExists }) => {
        if (loginExists) {
          this.stopLoading();
          this.dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: this.translate.instant('common.error'),
              message: this.translate.instant('auth.responses.errors.login_exists', { login: this.login() }),
              confirmText: this.translate.instant('common.ok'),
              showCancel: false,
              icon: 'error',
              iconColor: 'danger'
            }
          });
          return;
        }
        if (emailExists) {
          this.stopLoading();
          this.dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: this.translate.instant('common.error'),
              message: this.translate.instant('auth.responses.errors.email_exists', { email: this.email() }),
              confirmText: this.translate.instant('common.ok'),
              showCancel: false,
              icon: 'error',
              iconColor: 'danger'
            }
          });
          return;
        }
        this.register();
      },
      error: (err: HttpErrorResponse) => {
        this.stopLoading();
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.INTERNAL_ERROR');
        this.flash('error', errorMessage);
      }
    });
  }

  private register(): void {
    this.isLoading = true;
    this.sanitizeForm();
    const dto: RegisterRequestDto = {
      login: this.login(),
      email: this.email(),
      fullName: this.fullName(),
      password: this.password(),
      roleId: this.roleId()
    };

    this.authService.register(dto).subscribe({
      next: (registeredUser) => {
        this.stopLoading();
        this.resetForm();
        this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}user_registered`, { fullname: registeredUser.fullName }));
        setTimeout(() => {
          document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0);
        setTimeout(() => this.cancel(), 2000);
      },
      error: (err: HttpErrorResponse) => {
        this.stopLoading();
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.INTERNAL_ERROR');
        this.flash('error', errorMessage);
      }
    });
  }

  generatePassword(): void {
    const newPassword = this.passwordService.generatePassword();
    this.form.get('password')?.setValue(newPassword);
    if (!this.showPassword) this.showPassword = true;
    this.onPasswordChange();
  }

  onPasswordChange(): void {
    const pwd = this.form.get('password')?.value || '';
    if (!pwd) {
      this.passwordErrors = [];
      this.passwordStrength = '';
      return;
    }
    const result = this.passwordService.validatePassword(pwd, null);
    this.passwordErrors = result.errors;
    this.passwordStrength = result.strength;
  }

  getScore(): number {
    return this.passwordService.getStrengthScore(this.passwordStrength);
  }

  getStrengthClass(): string {
    return this.passwordService.getStrengthClass(this.passwordStrength);
  }

  getStrengthLabel(): string {
    const label = this.passwordService.getStrengthLabel(this.passwordStrength);
    return label;
  }

  private sanitizeForm(): void {
    this.sanitizeLogin();
    this.sanitizeEmail();
    this.sanitizeFullName();
    this.sanitizePassword();
  }

  private sanitizeLogin(): void {
    const raw = this.form.get('login')?.value || '';
    const sanitized = raw.trim().toLowerCase().replace(/[^a-z0-9_]/g, '');
    this.form.get('login')?.setValue(sanitized, { emitEvent: false });
  }

  private sanitizeEmail(): void {
    const raw = this.form.get('email')?.value || '';
    const sanitized = raw.trim().toLowerCase();
    this.form.get('email')?.setValue(sanitized, { emitEvent: false });
  }

  private sanitizeFullName(): void {
    const raw = this.form.get('fullName')?.value || '';
    const sanitized = raw.trim();
    this.form.get('fullName')?.setValue(sanitized, { emitEvent: false });
  }

  private sanitizePassword(): void {
    const raw = this.form.get('password')?.value || '';
    const sanitized = raw.trim();
    this.form.get('password')?.setValue(sanitized, { emitEvent: false });
  }

  resetForm(): void {
    this.form.reset({
      fullName: '',
      login: '',
      email: '',
      roleId: null,
      password: ''
    });
    this.passwordErrors = [];
    this.showPassword = false;
    this.errors = [];
    this.successMessage = '';
    this.form.markAsPristine();
    this.form.markAsUntouched();
  }

  dismissError(): void {
    this.errors = [];
  }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => this.successMessage = null, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => this.errors = [], 4000);
    }
  }

  flashErrors(messages: string[]): void {
    this.errors = messages;
    setTimeout(() => this.errors = [], 4000);
  }

  get isPasswordInvalid(): boolean {
    return this.passwordErrors.length > 0;
  }

  stopLoading(): void {
    this.isLoading = false;
  }

  cancel(): void {
    this.location.back();
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  ngOnDestroy(): void {}
}