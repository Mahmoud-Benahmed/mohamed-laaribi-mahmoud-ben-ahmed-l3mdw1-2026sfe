import { ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal, ViewChild } from '@angular/core';
import { CommonModule, registerLocaleData } from '@angular/common';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService, PRIVILEGES } from '../../../../services/auth/auth.service';
import { AuthUserGetResponseDto, PagedResultDto, UserStatsDto } from '../../../../interfaces/AuthDto';
import { PaginationComponent } from "../../../pagination/pagination";
import { ModalComponent } from '../../../modal/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatDialog } from '@angular/material/dialog';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-deactivated',
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
    MatTooltipModule,
    MatDividerModule,
    MatSnackBarModule,
    RouterLinkActive,
    RouterLink,
    PaginationComponent,
    TranslatePipe
  ],
  templateUrl: './deactivated.html',
  styleUrl: './deactivated.scss',
})
export class DeactivatedComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);

  readonly templateTranslationKey="auth.deactivated.";
  readonly responseSuccessTranslationKey="auth.responses.success.";
  readonly confirmationsTranslationKey="auth.confirmations.";

  @ViewChild(MatSort) sort!: MatSort;

  stats: UserStatsDto | null = null;

  displayedColumns: string[] = [
    'fullName',
    'email',
    'role',
    'createdAt',
    'lastLoginAt',
    'actions',
  ];

  dataSource = new MatTableDataSource<AuthUserGetResponseDto>([]);

  totalCount = 0;
  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];

  sortColumn: string = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  isLoading = false;
  searchTerm = '';
  error: string | null = null;
  successMessage: string | null = null;

  readonly PRIVILEGES = PRIVILEGES;

  constructor(
    public authService: AuthService,
    private cdr: ChangeDetectorRef,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  // ── Page title ────────────────────────────────────────────────────────────

  get pageTitle(): string {
    return this.translate.instant('USERS.TITLE_DEACTIVATED');
  }

  // ── Stats ─────────────────────────────────────────────────────────────────

  get activeUsers(): number { return this.stats?.activeUsers ?? 0; }
  get deactivatedUsers(): number { return this.stats?.deactivatedUsers ?? 0; }
  get deletedUsers(): number { return this.stats?.deletedUsers ?? 0; }

  loadUsers(): void {
    this.isLoading = true;
    this.authService.getDeactivatedUsers(this.pageNumber(), this.pageSize()).subscribe({
      next: (result: PagedResultDto<AuthUserGetResponseDto>) => {
        this.dataSource.data = result.items;
        this.totalCount = result.totalCount;
        this.dataSource.sort = this.sort;
        this.isLoading = false;
        this.loadStats();
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading = false;
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
        this.flash('error', errorMessage);
      },
    });
  }

  loadStats(): void {
    this.authService.getStats().subscribe({
      next: (result) => this.stats = result,
      error: (err: HttpErrorResponse) => {
        this.isLoading = false;
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
        this.flash('error', errorMessage);
      }
    });
  }

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }

  prevPage(): void {
    if (this.pageNumber() > 1) {
      this.pageNumber.set(this.pageNumber() - 1);
      this.loadUsers();
    }
  }

  nextPage(): void {
    if (this.pageNumber() < this.totalPages) {
      this.pageNumber.set(this.pageNumber() + 1);
      this.loadUsers();
    }
  }

  sortBy(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
  }

  get sortedData() {
    const data = [...this.dataSource.filteredData];
    if (!this.sortColumn) return data;

    return data.sort((a, b) => {
      let valA = (a as any)[this.sortColumn];
      let valB = (b as any)[this.sortColumn];

      if (valA == null) return 1;
      if (valB == null) return -1;

      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();

      return (valA < valB ? -1 : valA > valB ? 1 : 0) * (this.sortDirection === 'asc' ? 1 : -1);
    });
  }

  applyFilter(): void {
    this.dataSource.filter = this.searchTerm.trim().toLowerCase();
  }

  activateUser(user: AuthUserGetResponseDto): void {
    this.authService.activate(user.id).subscribe({
      next: () => {
        this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}user_activated`, { fullname: user.fullName ?? user.login }));
        this.reload();
      },
      error: () =>
        this.flash('error', this.translate.instant('USERS.ERRORS.ACTIVATE_FAILED', { name: user.fullName ?? user.login })),
    });
  }

  delete(user: AuthUserGetResponseDto): void {
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

  getInitials(user: AuthUserGetResponseDto): string {
    if (user.fullName) {
      return user.fullName
        .split(' ')
        .map((n) => n[0])
        .slice(0, 2)
        .join('')
        .toUpperCase();
    }
    return user.email[0].toUpperCase();
  }

  onPageSizeChange(): void {
    this.pageNumber.set(1);
    this.reload();
  }

  reload(): void {
    this.loadStats();
    this.loadUsers();
    this.cdr.markForCheck();
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
}
