import { ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthUserGetResponseDto, PagedResultDto, UserStatsDto } from '../../../../interfaces/AuthDto';
import { MatDialog } from '@angular/material/dialog';
import { ModalComponent } from '../../../modal/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService, PRIVILEGES } from '../../../../services/auth/auth.service';
import { PaginationComponent } from '../../../pagination/pagination';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatInputModule,
    MatFormFieldModule,
    MatMenuModule,
    MatTooltipModule,
    MatBadgeModule,
    MatDividerModule,
    MatSnackBarModule,
    RouterLinkActive,
    RouterLink,
    PaginationComponent,
    TranslatePipe
  ],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class UsersHomeComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);

  @ViewChild(MatSort) sort!: MatSort;

  displayedColumns: string[] = [
    'fullName', 'email', 'role', 'createdAt', 'lastLoginAt', 'actions',
  ];

  dataSource = new MatTableDataSource<AuthUserGetResponseDto>([]);

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  sortColumn: string = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  isLoading = false;
  searchTerm = '';
  errors: string[] = [];
  successMessage: string | null = null;

  stats: UserStatsDto | null = null;

  readonly PRIVILEGES = PRIVILEGES;

  readonly templateTranslationKey="auth.list.";
  readonly responseSuccessTranslationKey="auth.responses.success.";
  readonly confirmationsTranslationKey="auth.confirmations.";

  constructor(
    private router: Router,
    public authService: AuthService,
    private cdr: ChangeDetectorRef,
    private dialog: MatDialog,
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  // ── Page title ────────────────────────────────────────────────────────────

  get pageTitle(): string {
    return this.translate.instant('USERS.TITLE_ACTIVE');
  }

  // ── Stats ─────────────────────────────────────────────────────────────────

  get activeUsers():      number { return this.stats?.activeUsers      ?? 0; }
  get deactivatedUsers(): number { return this.stats?.deactivatedUsers ?? 0; }
  get totalUsers():       number { return this.stats?.totalUsers       ?? 0; }

  // ── Load (pure fetchers) ──────────────────────────────────────────────────

  loadUsers(): void {
    this.isLoading = true;
    this.authService.getActivatedUsers(this.pageNumber(), this.pageSize()).subscribe({
      next: (result: PagedResultDto<AuthUserGetResponseDto>) => {
        this.dataSource.data = result.items.filter(u => u.id !== this.currentUserId);
        this.totalCount = result.totalCount;
        this.dataSource.sort = this.sort;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
        this.flash('error', errorMessage);
        this.isLoading = false;
      },
    });
  }

  loadStats(): void {
    this.authService.getStats().subscribe({
      next: (result) => { this.stats = result; this.cdr.markForCheck(); },
      error: (err: HttpErrorResponse) => {
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
        this.flash('error', errorMessage);
        this.isLoading = false;
      },    });
  }

  reload(): void {
    this.loadUsers();
    this.loadStats();
    this.cdr.markForCheck();
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }

  onPageSizeChange(): void {
    this.pageNumber.set(1);
    this.reload();
  }

  // ── Sort ──────────────────────────────────────────────────────────────────

  sortBy(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
  }

  get sortedData(): AuthUserGetResponseDto[] {
    const data = [...this.dataSource.filteredData];
    if (!this.sortColumn) return data;

    return data.sort((a, b) => {
      let valA = (a as any)[this.sortColumn];
      let valB = (b as any)[this.sortColumn];

      if (valA == null) return 1;
      if (valB == null) return -1;
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();

      return (valA < valB ? -1 : valA > valB ? 1 : 0) *
        (this.sortDirection === 'asc' ? 1 : -1);
    });
  }

  applyFilter(): void {
    this.dataSource.filter = this.searchTerm.trim().toLowerCase();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  deactivateUser(user: AuthUserGetResponseDto): void {
    const prefix=`${this.confirmationsTranslationKey}deactivate_user`;
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title: this.translate.instant(`${prefix}.title`),
        message: this.translate.instant(`${prefix}.message`, { fullname: user.fullName ?? user.login }),
        confirmText: this.translate.instant(`${prefix}.confirm_text`),
        showCancel: true,
        icon: 'block',
        iconColor: 'warning',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        if (!result) return;
        this.authService.deactivate(user.id).subscribe({
          next: () => {
            this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}user_deactivated`, { fullname: user.fullName ?? user.login }));
            this.reload();
          },
          error: (err: HttpErrorResponse) => {
            const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
            this.flash('error', errorMessage);
            this.isLoading = false;
          }
        });
      });
  }

  softDeleteUser(user: AuthUserGetResponseDto): void {
    const prefix=`${this.confirmationsTranslationKey}delete_user`;
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title: this.translate.instant(`${prefix}.title`),
        message: this.translate.instant(`${prefix}.message`, { fullname: user.fullName ?? user.login }),
        confirmText: this.translate.instant(`${prefix}.confirm_text`),
        showCancel: true,
        icon: 'auto_delete',
        iconColor: 'danger',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        if (!result) return;
        this.authService.softDelete(user.id).subscribe({
          next: () => {
            this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}user_deleted`, { fullname: user.fullName ?? user.login }));
            this.reload();
          },
          error: (err: HttpErrorResponse) => {
            const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
            this.flash('error', errorMessage);
            this.isLoading = false;
          }
        });
      });
  }

  restoreUser(user: AuthUserGetResponseDto): void {
    const prefix=`${this.confirmationsTranslationKey}restore_user`;
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title: this.translate.instant(`${prefix}.title`),
        message: this.translate.instant(`${prefix}.message`, { fullname: user.fullName ?? user.login }),
        confirmText: this.translate.instant(`${prefix}.confirm_text`),
        showCancel: true,
        icon: 'settings_backup_restore',
        iconColor: 'success',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        if (!result) return;
        this.authService.restore(user.id).subscribe({
          next: () => {
            this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}user_restored`, { fullname: user.fullName ?? user.login }));
            this.reload();
          },
          error: (err: HttpErrorResponse) => {
            const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
            this.flash('error', errorMessage);
            this.isLoading = false;
          }
        });
      });
  }

  // ── Navigation ────────────────────────────────────────────────────────────

  goToProfile(authUserId: string): void {
    if (!authUserId) return;
    if (authUserId === this.authService.UserId) {
      this.router.navigate(['/profile']);
      return;
    }
    this.router.navigate(['/users', authUserId]);
  }

  goToRegister(): void {
    this.router.navigate(['/users/register']);
  }

  // ── Feedback ──────────────────────────────────────────────────────────────

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; this.cdr.markForCheck(); }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; this.cdr.markForCheck(); }, 4000);
    }
    this.cdr.markForCheck();
  }

  dismissError(): void { this.errors = []; }

  // ── Helpers ───────────────────────────────────────────────────────────────

  get currentUserId(): string | null { return this.authService.UserId; }

  getInitials(user: AuthUserGetResponseDto): string {
    if (user.fullName) {
      return user.fullName
        .split(' ')
        .map(n => n[0])
        .slice(0, 2)
        .join('')
        .toUpperCase();
    }
    return user.email[0].toUpperCase();
  }
}
