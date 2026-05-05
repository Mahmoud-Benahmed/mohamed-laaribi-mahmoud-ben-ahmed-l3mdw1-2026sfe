import { LoginRequestDto } from './../../../../interfaces/AuthDto';
import { ChangeDetectorRef, Component, HostBinding, OnDestroy, ViewChild, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../../services/auth/auth.service';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ModalComponent } from '../../../modal/modal';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressSpinner } from "@angular/material/progress-spinner";
import { forkJoin } from 'rxjs';
import { RegisterRequestDto, RoleResponseDto } from '../../../../interfaces/AuthDto';
import { HttpError } from '../../../../interfaces/HttpError';
import { RoleService } from '../../../../services/auth/roles.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PasswordService } from '../../../../services/password.service';

@HostBinding('class')
@Component({
  selector: 'app-register',
  imports: [
    CommonModule,
    FormsModule,
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
  styleUrl: './register.scss'
})
export class RegisterComponent implements OnDestroy {
  @ViewChild('registerForm') registerForm!: NgForm;

  private location= inject(Location);

  private translate = inject(TranslateService);
  private passwordService = inject(PasswordService);

  readonly emailPattern = /^(([^<>()[\]\\.,;:\s@"]+(\.[^<>()[\]\\.,;:\s@"]+)*)|.(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/.source;
  readonly fullnamePattern = /^\p{L}+(\s\p{L}+)*$/u;
  readonly loginPattern = /^[a-zA-Z0-9_\s]+$/;

  credentials: RegisterRequestDto = { login: '', email: '', fullName: '', password: '', roleId: null };
  showPassword = false;
  private errorTimeout: any = null;
  isLoading: boolean = false;
  errors: string[] = [];
  successMessage: string | null = null;

  roles: RoleResponseDto[] = [];

  passwordErrors: string[] = [];
  passwordScore: number = 0;
  passwordStrength: string = '';

  constructor(
    private router: Router,
    private authService: AuthService,
    private roleService: RoleService,
    private cdr: ChangeDetectorRef,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.roleService.getAll().subscribe({
      next: (roles) => {
        this.roles = roles;
      },
      error: (err) => {
        const error = err.error as HttpError;
        this.flash('error', error.message ?? this.translate.instant('USERS.ERRORS.LOAD_ROLES_FAILED'));
      }
    });
  }

  get hostClass(): string {
    return this.passwordStrength
      ? this.passwordService.getStrengthClass(this.passwordStrength)
      : '';
  }

  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  onSubmit(): void {
    this.isLoading = true;
    this.sanitizeInputs();

    const sanitizedLogin = this.sanitizeLogin(this.credentials.login);
    const loginChanged = sanitizedLogin !== this.credentials.login;

    if (loginChanged) {
      const dialogRef = this.dialog.open(ModalComponent, {
        width: '400px',
        data: {
          title: this.translate.instant('CONFIRMATION.LOGIN_CHANGE_WARNING_TITLE'),
          message: this.translate.instant('CONFIRMATION.LOGIN_CHANGE_WARNING', { login: sanitizedLogin }),
          confirmText: this.translate.instant('COMMON.CONFIRM'),
          showCancel: true,
          icon: 'check_circle',
          iconColor: 'warn'
        }
      });

      dialogRef.afterClosed().subscribe(result => {
        if (result) {
          this.credentials.login = sanitizedLogin;
          this.cdr.markForCheck();
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
      loginExists: this.authService.existsByLogin(this.credentials.login),
      emailExists: this.authService.existsByEmail(this.credentials.email)
    }).subscribe({
      next: ({ loginExists, emailExists }) => {
        if (loginExists) {
          this.stopLoading();
          this.dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: this.translate.instant('USERS.ERRORS.LOGIN_EXISTS_TITLE'),
              message: this.translate.instant('USERS.ERRORS.LOGIN_EXISTS', { login: this.credentials.login }),
              confirmText: this.translate.instant('COMMON.OK'),
              showCancel: false,
              icon: 'check_circle',
              iconColor: 'warn'
            }
          });
          return;
        }

        if (emailExists) {
          this.stopLoading();
          this.dialog.open(ModalComponent, {
            width: '400px',
            data: {
              title: this.translate.instant('USERS.ERRORS.EMAIL_EXISTS_TITLE'),
              message: this.translate.instant('USERS.ERRORS.EMAIL_EXISTS', { email: this.credentials.email }),
              confirmText: this.translate.instant('COMMON.OK'),
              showCancel: false,
              icon: 'check_circle',
              iconColor: 'warn'
            }
          });
          return;
        }

        this.register();
      },
      error: (error) => {
        this.stopLoading();
        const err = error.error as HttpError;
        if (err.code === 'VALIDATION_ERROR' && err.errors) {
          const messages = Object.values(err.errors).flat();
          this.flashErrors(messages);
        } else {
          this.flash('error', err.message ?? this.translate.instant('USERS.ERRORS.CHECK_FAILED'));
        }
      }
    });
  }

  private register(): void {
    this.isLoading = true;
    this.sanitizeInputs();
    this.authService.register(this.credentials).subscribe({
      next: (registeredUser) => {
        this.stopLoading();
        this.resetForm();
        this.flash('success', this.translate.instant('SUCCESS.USER_REGISTERED', { name: registeredUser.fullName }));
        setTimeout(() => {
          document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 0);
        setTimeout(() => {
          this.cancel();
          this.isLoading = false;
        }, 2000);
      },
      error: (error) => {
        this.stopLoading();
        const err = error.error as HttpError;
        if (err.code === 'VALIDATION_ERROR' && err.errors) {
          const messages = Object.values(err.errors).flat();
          this.flashErrors(messages);
          setTimeout(() => {
            document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }, 0);
          setTimeout(() => {
            this.cancel();
            this.isLoading = false;
          }, 2000);
        } else {
          this.flash('error', err.message ?? this.translate.instant('USERS.ERRORS.REGISTER_FAILED'));
        }
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  cancel(){
    this.location.back();
  }

  generatePassword(): void {
    this.credentials.password = this.passwordService.generatePassword();
    if (!this.showPassword) this.showPassword = true;
    this.onPasswordChange();
  }

  onPasswordChange(): void {
    if (!this.credentials.password) {
      this.passwordErrors = [];
      this.passwordScore = 0;
      this.passwordStrength = '';
      return;
    }

    // Use PasswordService for validation
    const result = this.passwordService.validatePassword(this.credentials.password, null);

    this.passwordErrors = result.errors;
    this.passwordScore = this.passwordService.getStrengthScore(result.strength);
    this.passwordStrength = result.strength;
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

  private sanitizeLogin(login: string): string {
    return login.toLowerCase().replace(/ /g, "_");
  }

  private sanitizeInputs(): void {
    this.credentials.login = this.sanitizeText(this.credentials.login);
    this.credentials.email = this.sanitizeText(this.credentials.email).toLowerCase();
    this.credentials.fullName = this.credentials.fullName.trim();
    this.credentials.password = this.credentials.password.trim();
  }

  private sanitizeText(value: string): string {
    return value
      .trim()
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/&/g, '&amp;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#x27;')
      .replace(/\//g, '&#x2F;');
  }

  resetForm(): void {
    this.credentials = { login: '', email: '', fullName: '', password: '', roleId: null };
    this.passwordErrors = [];
    this.passwordScore = 0;
    this.passwordStrength = '';
    this.showPassword = false;
    this.registerForm?.resetForm();
  }

  dismissError(): void { this.errors = []; }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; this.cdr.markForCheck(); }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.dismissError(); this.cdr.markForCheck(); }, 4000);
    }
    this.cdr.markForCheck();
  }

  flashErrors(messages: string[]): void {
    this.errors = messages;
    setTimeout(() => { this.errors = []; this.cdr.markForCheck(); }, 4000);
    this.cdr.markForCheck();
  }

  get isPasswordInvalid(): boolean {
    return this.passwordErrors.length > 0;
  }

  stopLoading(): void {
    this.isLoading = false;
    this.cdr.markForCheck();
  }

  ngOnDestroy(): void {
    if (this.errorTimeout) clearTimeout(this.errorTimeout);
  }
}
