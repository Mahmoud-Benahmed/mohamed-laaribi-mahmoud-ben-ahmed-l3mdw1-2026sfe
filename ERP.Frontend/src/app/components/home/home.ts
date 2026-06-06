import { PRIVILEGES } from './../../services/auth/auth.service';
import { ChangeDetectorRef, Component, OnInit, ViewEncapsulation } from '@angular/core';
import { AuthService } from '../../services/auth/auth.service';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthUserGetResponseDto } from '../../interfaces/AuthDto';
import { HttpError } from '../../interfaces/HttpError';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatTooltipModule, RouterModule, TranslatePipe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
  encapsulation: ViewEncapsulation.None,
})
export class HomeComponent implements OnInit {
  isLoading = false;
  userProfile: AuthUserGetResponseDto | null = null;
  lastLogin: string = '';
  error: string | null = null;
  successMessage: string | null = null;
  readonly PRIVILEGES = PRIVILEGES;

  constructor(
    public authService: AuthService,
    private cdr: ChangeDetectorRef,
    private translate: TranslateService
  ) {}

  ngOnInit(): void {
    if (this.authService.UserProfile) {
      this.userProfile = this.authService.UserProfile;
    } else {
      this.authService.getMe().subscribe({
        next: (authUser) => {
          this.userProfile = authUser;
          this.authService.setUserProfile(this.userProfile);
        },
        error: (error) => {
          const err = error.error as HttpError;
          this.flash('error', err.message || this.translate.instant('ERRORS.INTERNAL_ERROR'));
        }
      });
    }
  }

  get userName(): string {
    return this.userProfile?.fullName || this.userProfile?.login || '-';
  }

  get welcomeMessage(): string {
    return this.translate.instant('home.title', { username: this.userName ?? '' });
  }

  get roleName():string{
    return this.authService.Role || '-';
  }

  dismissError(): void { this.error = null; }

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      this.cdr.markForCheck();
      setTimeout(() => (this.successMessage = null), 3000);
    } else {
      this.error = msg;
      this.cdr.markForCheck();
      setTimeout(() => (this.error = null), 3000);
    }
  }

  logout(): void {
    this.authService.logout();
  }
}
