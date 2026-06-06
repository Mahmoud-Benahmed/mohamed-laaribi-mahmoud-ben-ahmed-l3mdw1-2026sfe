import { RoleCreateDto, RoleService, RoleUpdateDto } from '../../../services/auth/roles.service';
import { ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { ModalComponent } from '../../modal/modal';
import { MatDialog } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PaginationComponent } from '../../pagination/pagination';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { RoleResponseDto } from '../../../interfaces/AuthDto';
import { MatTableDataSource } from '@angular/material/table';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

type ViewMode = 'list' | 'create' | 'edit' | 'view';

@Component({
  selector: 'app-role',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIcon,
    PaginationComponent,
    TranslatePipe
  ],
  templateUrl: './roles.html',
  styleUrls: ['./roles.scss'],
})
export class RoleComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);

  // Translation prefixes
  readonly templateTranslationKey = 'auth.roles.';
  readonly responseSuccessTranslationKey = 'auth.responses.success.';
  readonly confirmationsTranslationKey = 'auth.confirmations.';

  dataSource = new MatTableDataSource<RoleResponseDto>([]);

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  viewMode: ViewMode = 'list';
  selectedRole: RoleResponseDto | null = null;
  loading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  searchQuery = '';

  roleForm: FormGroup;

  readonly PRIVILEGES = PRIVILEGES;

  constructor(
    public authService: AuthService,
    private roleService: RoleService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef
  ) {
    this.roleForm = this.fb.group({
      libelle: ['', [Validators.required, Validators.minLength(2)]],
    });
  }

  ngOnInit(): void {
    this.reload();
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize());
  }

  onPageSizeChange(): void {
    this.pageNumber.set(1);
    this.reload();
  }

  // ── Search ────────────────────────────────────────────────────────────────

  applyFilter(): void {
    this.dataSource.filter = this.searchQuery.trim().toLowerCase();
    this.pageNumber.set(1);
  }

  // ── Sorting ───────────────────────────────────────────────────────────────

  sortBy(column: string): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = column;
      this.sortDirection = 'asc';
    }
  }

  get sortedData(): RoleResponseDto[] {
    const filtered = [...this.dataSource.filteredData];

    if (this.sortColumn) {
      filtered.sort((a, b) => {
        let valA = (a as any)[this.sortColumn];
        let valB = (b as any)[this.sortColumn];

        if (valA == null) return 1;
        if (valB == null) return -1;

        if (typeof valA === 'string') valA = valA.toLowerCase();
        if (typeof valB === 'string') valB = valB.toLowerCase();

        return (valA < valB ? -1 : valA > valB ? 1 : 0) * (this.sortDirection === 'asc' ? 1 : -1);
      });
    }

    const start = (this.pageNumber() - 1) * this.pageSize();
    return filtered.slice(start, start + this.pageSize());
  }

  // ── Load ──────────────────────────────────────────────────────────────────

  load(): void {
    this.loading = true;
    this.errors = [];
    this.roleService.getAll().subscribe({
      next: (res: RoleResponseDto[]) => {
        this.dataSource.data = res;
        this.totalCount = this.dataSource.data.length;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.load_failed');
        this.flash('error', errorMessage);
        this.loading = false;
      },
    });
  }

  reload(): void {
    this.load();
    this.cdr.markForCheck();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  openCreate(): void {
    this.viewMode = 'create';
    this.selectedRole = null;
    this.roleForm.reset({ libelle: '' });
  }

  openEdit(role: RoleResponseDto): void {
    this.viewMode = 'edit';
    this.selectedRole = role;
    this.roleForm.patchValue({ libelle: role.libelle });
    this.cdr.markForCheck();
  }

  openView(role: RoleResponseDto): void {
    this.viewMode = 'view';
    this.selectedRole = role;
    this.cdr.markForCheck();
  }

  cancel(): void {
    this.viewMode = 'list';
    this.selectedRole = null;
    this.roleForm.reset();
  }

  submit(): void {
    if (this.roleForm.invalid) return;
    const val = this.roleForm.value;

    if (this.viewMode === 'create') {
      const dto: RoleCreateDto = { libelle: val.libelle };
      this.roleService.create(dto).subscribe({
        next: (role) => {
          this.reload();
          this.cancel();
          this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}role_created`, { name: role.libelle }));
        },
        error: (err: HttpErrorResponse) => {
          const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.create_failed');
          this.flash('error', errorMessage);
        },
      });
    } else if (this.viewMode === 'edit' && this.selectedRole) {
      const dto: RoleUpdateDto = { libelle: val.libelle };
      this.roleService.update(this.selectedRole.id, dto).subscribe({
        next: (role) => {
          this.cancel();
          this.reload();
          this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}role_updated`, { name: role.libelle }));
        },
        error: (err: HttpErrorResponse) => {
          const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.update_failed');
          this.flash('error', errorMessage);
        },
      });
    }
  }

  delete(role: RoleResponseDto): void {
    const prefix = `${this.confirmationsTranslationKey}delete_role`;
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title: this.translate.instant(`${prefix}.title`),
        message: this.translate.instant(`${prefix}.message`, { name: role.libelle }),
        confirmText: this.translate.instant(`${prefix}.confirm_text`),
        showCancel: true,
        icon: 'auto_delete',
        iconColor: 'danger',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        this.roleService.delete(role.id).subscribe({
          next: () => {
            if (this.viewMode === 'view') this.cancel();
            this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}role_deleted`, { name: role.libelle }));
            this.reload();
          },
          error: (err: HttpErrorResponse) => {
            const errorMessage = err.error?.message || this.translate.instant('auth.responses.errors.delete_failed', { name: role.libelle });
            this.flash('error', errorMessage);
          },
        });
      });
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

  dismissError(): void {
    this.errors = [];
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  trackById(_: number, r: RoleResponseDto): string {
    return r.id;
  }
}