import { AuthService } from './../../../services/auth/auth.service';
import { ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal, TemplateRef, ViewChild } from '@angular/core';
import { CommonModule, formatNumber, KeyValuePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { AuditAction, AuditLogResponseDto, AuditLogService } from '../../../services/audit-log.service';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { ModalComponent } from '../../modal/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PaginationComponent } from "../../pagination/pagination";

interface ActionMeta {
  icon: string;
  category: 'auth' | 'user' | 'admin' | 'danger' | 'password'|'default';
}

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatTableModule, MatPaginatorModule, MatCardModule,
    MatButtonModule, MatIconModule, MatFormFieldModule,
    MatSelectModule, MatButtonToggleModule, MatProgressSpinnerModule,
    MatTooltipModule, MatDividerModule, MatSnackBarModule,
    MatDialogModule, MatChipsModule, MatInputModule, MatSelectModule, MatFormFieldModule,
    TranslatePipe,
    PaginationComponent
],
  templateUrl: './audit-log.html',
  styleUrl: './audit-log.scss',
})
export class AuditLogComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);

  @ViewChild('detailDialog') detailDialog!: TemplateRef<any>;

  displayedColumns = ['status', 'action', 'performedBy', 'targetUserId', 'ipAddress', 'timestamp', 'details'];
  dataSource = new MatTableDataSource<AuditLogResponseDto>([]);

  isLoading = false;

  selectedAction: AuditAction | null = null;
  selectedStatus: boolean | null = null;
  userIdFilter = '';
  error: string | null = null;
  successMessage: string | null = null;

  readonly ACTION_MAP: Record<AuditAction, ActionMeta> = {
      // Auth
      Login:                    { icon: 'login',              category: 'auth' },
      Logout:                   { icon: 'logout',             category: 'auth' },
      TokenRefreshed:           { icon: 'refresh',            category: 'auth' },
      TokenRevoked:             { icon: 'block',              category: 'danger' },
      TokenValidated:           { icon: 'verified',           category: 'auth' },
      TokenValidationFailed:    { icon: 'error',              category: 'danger' },

      // Registration
      UserRegistered:           { icon: 'person_add',         category: 'user' },

      // Password
      PasswordChanged:          { icon: 'lock_reset',         category: 'password' },
      PasswordChangedByAdmin:   { icon: 'admin_panel_settings', category: 'admin' },

      // Profile
      ProfileUpdated:           { icon: 'edit',               category: 'user' },

      // Account status
      UserActivated:            { icon: 'check_circle',       category: 'user' },
      UserDeactivated:          { icon: 'block',              category: 'danger' },
      UserDeleted:              { icon: 'auto_delete',        category: 'danger' },
      UserRestored:             { icon: 'restore_from_trash', category: 'user' },

      // Role
      RoleCreated:              { icon: 'person_add',         category: 'admin' },
      RoleUpdated:              { icon: 'person_edit',        category: 'admin' },
      RoleDeleted:              { icon: 'person_cancel',      category: 'danger' },

      // Controle
      ControleCreated:          { icon: 'add_moderator',      category: 'admin' },
      ControleUpdated:          { icon: 'edit_location_alt',  category: 'admin' },
      ControleDeleted:          { icon: 'remove_moderator',   category: 'danger' },

      // Error actions
      Unauthorized:             { icon: 'warning',            category: 'danger' },
      UserNotFound:             { icon: 'search_off',         category: 'danger' },
      UnhandledError:           { icon: 'bug_report',         category: 'danger' }
  };

    // ── Pagination ────────────────────────────────────────────────────────────
  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  constructor(
    private authService: AuthService,
    private auditLogService: AuditLogService,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
    this.cdr.markForCheck();
  }

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }
  onPageSizeChange(): void {
    this.pageNumber.set(1);
    this.load();
  }


  load(): void {
    this.isLoading = true;

    const source$ = this.userIdFilter.trim()
      ? this.auditLogService.getByUser(this.userIdFilter.trim(), this.pageNumber(), this.pageSize())
      : this.auditLogService.getAll(this.pageNumber(), this.pageSize());

    source$.subscribe({
      next: (result) => {
        this.dataSource.data = this.selectedStatus !== null
          ? result.items.filter(l => l.success === this.selectedStatus)
          : result.items;
        this.totalCount = result.totalCount;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.flash('error', this.translate.instant('auth.audit_log.responses.errors.internal_error'));
      }
    });
  }

  onFilterChange(): void {
    this.pageNumber.set(1);
    this.load();
  }

  openDetail(log: AuditLogResponseDto): void {
    this.dialog.open(this.detailDialog, { width: '500px', data: log });
  }

  confirmClear(): void {
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title: this.translate.instant('auth.audit_log.clear'),
        message: this.translate.instant('auth.audit_log.confirmations.clear_logs'),
        confirmText: this.translate.instant('common.ok'),
        showCancel: true,
        icon: 'playlist_remove',
        iconColor: 'warn',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        if (result) {
          this.auditLogService.clear().subscribe({
            next: () => {
              this.flash('success', this.translate.instant('auth.audit_log.responses.success.logs_cleared'));
              this.load();
              this.cdr.markForCheck();
            },
            error: () => this.flash('error', this.translate.instant('auth.audit_log.responses.errors.internal_error'))
          });
        }
      });
  }

  formatAction(action: AuditAction): string {
    // Return the translation key path, the template will use the translate pipe
    return `auth.audit_log.actions.${action}`;
  }

  getActionIcon(action: AuditAction): string {
    return this.ACTION_MAP[action]?.icon ?? 'circle';
  }

  getActionCategory(action: AuditAction): string {
    return this.ACTION_MAP[action]?.category ?? 'default';
  }

  hasDetails(log: AuditLogResponseDto): boolean {
    return !!(log.metadata || log.failureReason || log.userAgent);
  }

  metadataEntries(log: AuditLogResponseDto): { key: string; value: string }[] {
    if (!log.metadata) return [];
    return Object.entries(log.metadata).map(([key, value]) => ({ key, value }));
  }

  goToProfile(authUserId: string): void {
    if (!authUserId) return;

    if (authUserId === this.authService.UserId) {
      this.router.navigate(['/profile']);
      return;
    }

    this.router.navigate(['/users', authUserId]);
  }

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

  dismissError(): void {
    this.error = null;
  }
}