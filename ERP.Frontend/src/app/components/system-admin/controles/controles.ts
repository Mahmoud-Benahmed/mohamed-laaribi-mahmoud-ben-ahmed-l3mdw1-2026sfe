import { AuthService, PRIVILEGES } from './../../../services/auth/auth.service';
import { ControleRequestDto } from './../../../services/auth/controle.service';
import { ControleService } from '../../../services/auth/controle.service';
import { ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { ModalComponent } from '../../modal/modal';
import { MatDialog } from '@angular/material/dialog';
import { HttpError } from '../../../interfaces/HttpError';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PaginationComponent } from '../../pagination/pagination';
import { ControleResponseDto } from '../../../interfaces/AuthDto';
import { MatTableDataSource } from '@angular/material/table';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HttpErrorResponse } from '@angular/common/http';

type ViewMode = 'list' | 'create' | 'edit' | 'view';

@Component({
  selector: 'app-controle',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatIcon, PaginationComponent, TranslatePipe],
  templateUrl: './controles.html',
  styleUrls: ['./controles.scss'],
})
export class ControleComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  // ── Data ──────────────────────────────────────────────────────────────────

  dataSource = new MatTableDataSource<ControleResponseDto>([]);

  // ── Pagination ────────────────────────────────────────────────────────────

  pageNumber = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [5, 10, 25, 50];
  totalCount = 0;

  // ── Sorting ───────────────────────────────────────────────────────────────

  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';

  // ── State ─────────────────────────────────────────────────────────────────

  viewMode: ViewMode = 'list';
  selectedControle: ControleResponseDto | null = null;
  loading = false;
  errors: string[] = [];
  successMessage: string | null = null;
  searchQuery = '';

  controleForm: FormGroup;

  readonly templateTranslationKey= `auth.controles.`;
  readonly responseSuccessTranslationKey="auth.responses.success.";
  readonly confirmationsTranslationKey="auth.confirmations.";


  readonly PRIVILEGES = PRIVILEGES;

  constructor(
    public authService: AuthService,
    private controleService: ControleService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef,
    public translate: TranslateService
  ) {
    this.controleForm = this.fb.group({
      category:    ['', [Validators.required, Validators.minLength(2)]],
      libelle:     ['', [Validators.required, Validators.minLength(2)]],
      description: ['', [Validators.required, Validators.minLength(5)]],
    });
  }

  ngOnInit(): void {
    this.reload();
  }

  // ── Pagination ────────────────────────────────────────────────────────────

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize()); }

  onPageSizeChange(): void { this.pageNumber.set(1); this.reload(); }

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

  get sortedData(): ControleResponseDto[] {
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

    // client-side pagination slice
    const start = (this.pageNumber() - 1) * this.pageSize();
    return filtered.slice(start, start + this.pageSize());
  }

  // ── Load ──────────────────────────────────────────────────────────────────

  load(): void {
    this.loading = true;
    this.errors = [];
    this.controleService.getAll().subscribe({
      next: (res: ControleResponseDto[]) => {
        this.dataSource.data = res;
        this.totalCount = res.length;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
        this.flash('error', errorMessage);
      }
    });
  }

  reload(): void {
    this.load();
    this.cdr.markForCheck();
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  openCreate(): void {
    this.viewMode = 'create';
    this.selectedControle = null;
    this.controleForm.reset({ category: '', libelle: '', description: '' });
  }

  openEdit(controle: ControleResponseDto): void {
    this.viewMode = 'edit';
    this.selectedControle = controle;
    this.controleForm.patchValue({
      category:    controle.category,
      libelle:     controle.libelle,
      description: controle.description,
    });
    this.cdr.markForCheck();
  }

  openView(controle: ControleResponseDto): void {
    this.viewMode = 'view';
    this.selectedControle = controle;
    this.cdr.markForCheck();
  }

  cancel(): void {
    this.viewMode = 'list';
    this.selectedControle = null;
    this.controleForm.reset();
  }

  submit(): void {
    if (this.controleForm.invalid) return;
    const val = this.controleForm.value as ControleRequestDto;

    if (this.viewMode === 'create') {
      this.controleService.create(val).subscribe({
        next: () => {
          this.reload();
          this.cancel();
          this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}controle_created`, { name: val.libelle }));
        },
        error: (err: HttpErrorResponse) => {
          const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
          this.flash('error', errorMessage);
        }
      });
    } else if (this.viewMode === 'edit' && this.selectedControle) {
      this.controleService.update(this.selectedControle.id, val).subscribe({
        next: () => {
          this.cancel();
          this.reload();
          this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}controle_updated`, { name: val.libelle }));
        },
        error: (err: HttpErrorResponse) => {
          const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
          this.flash('error', errorMessage);
        }
      });
    }
  }

  delete(controle: ControleResponseDto): void {
    const prefix=`auth.confirmations.delete_controle`;
    const dialogRef = this.dialog.open(ModalComponent, {
      width: '400px',
      data: {
        title:       this.translate.instant(`${prefix}.title`),
        message:     this.translate.instant(`${prefix}.message`, { name: controle.libelle }),
        confirmText: this.translate.instant(`${prefix}.confirm_text`),
        showCancel:  true,
        icon:        'auto_delete',
        iconColor:   'danger',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) return;
        this.controleService.delete(controle.id).subscribe({
          next: () => {
            if (this.viewMode === 'view') this.cancel();
            this.flash('success', this.translate.instant(`${this.responseSuccessTranslationKey}controle_deleted`, { name: controle.libelle }));
            this.reload();
          },
          error: (err: HttpErrorResponse) => {
            const errorMessage = err.error?.message || this.translate.instant('auth.responses.INTERNAL_ERROR');
            this.flash('error', errorMessage);
          }        });
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

  dismissError(): void { this.errors = []; }

  // ── Helpers ───────────────────────────────────────────────────────────────

  trackById(_: number, c: ControleResponseDto): string { return c.id; }
}
