import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environment';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../services/auth/auth.service';
import { RoleService } from '../../../services/auth/roles.service';
import { ControleService } from '../../../services/auth/controle.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

interface RoleDto {
  id: string;
  libelle: string;
}

interface ControleDto {
  id: string;
  category: string;
  libelle: string;
  description: string;
}

interface PrivilegeDto {
  id: string;
  roleId: string;
  controleId: string;
  controleLibelle: string;
  controleCategory: string;
  isGranted: boolean;
}

interface MatrixCell {
  roleId: string;
  controleId: string;
  isGranted: boolean;
  loading: boolean;
}

@Component({
  selector: 'app-permission-matrix',
  standalone: true,
  imports: [
    CommonModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatIconModule,
    MatButtonModule,
    TranslatePipe
  ],
  templateUrl: './permission-matrix.html',
  styleUrl: './permission-matrix.scss',
})
export class PermissionMatrixComponent implements OnInit {
  private translate = inject(TranslateService);

  roles: RoleDto[] = [];
  controles: ControleDto[] = [];
  categories: string[] = [];
  matrix: Map<string, MatrixCell> = new Map();
  isLoading = true;
  error: string | null = null;
  successMessage: string | null = null;

  collapsedCategories = new Set<string>();
  readonly cellWidth = 160;
  private baseUrl = `${environment.apiUrl}`;

  constructor(
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    public authService: AuthService,
    private roleService: RoleService,
    private controleService: ControleService
  ) {}

  ngOnInit(): void {
    this.loadMatrix();
  }

  loadMatrix(): void {
    this.isLoading = true;
    forkJoin({
      roles: this.roleService.getAll(),
      controles: this.controleService.getAll()
    }).subscribe({
      next: ({ roles, controles }) => {
        this.roles = roles;
        this.controles = controles;

        this.categories = [...new Set(this.controles.map((c) => c.category))];
        this.collapsedCategories = new Set(this.categories);
        this.loadPrivileges();
      },
      error: () => {
        this.isLoading = false;
        this.flash('error', this.translate.instant('auth.permissions.errors.load_matrix_failed'));
      },
    });
  }

  loadPrivileges(): void {
    const requests = this.roles.map((role) =>
      this.http.get<PrivilegeDto[]>(`${this.baseUrl}/auth/privileges/${role.id}`)
    );

    forkJoin(requests).subscribe({
      next: (results) => {
        // First, create a Set of existing privileges for quick lookup
        const existingPrivileges = new Set<string>();

        results.forEach((privileges) => {
          privileges.forEach((p) => {
            const key = this.cellKey(p.roleId, p.controleId);
            existingPrivileges.add(key);
            this.matrix.set(key, {
              roleId: p.roleId,
              controleId: p.controleId,
              isGranted: p.isGranted,
              loading: false,
            });
          });
        });

        // Then, create empty cells for all missing combinations
        this.roles.forEach(role => {
          this.controles.forEach(controle => {
            const key = this.cellKey(role.id, controle.id);
            if (!existingPrivileges.has(key)) {
              // Create a default "denied" cell for missing privileges
              this.matrix.set(key, {
                roleId: role.id,
                controleId: controle.id,
                isGranted: false,
                loading: false,
              });
            }
          });
        });

        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.flash('error', this.translate.instant('auth.permissions.errors.load_privileges_failed'));
      },
    });
  }

  get cellWidthPx(): string { return `${this.cellWidth}px`; }

  getCell(roleId: string, controleId: string): MatrixCell | undefined {
    return this.matrix.get(this.cellKey(roleId, controleId));
  }

  togglePrivilege(roleId: string, controleId: string): void {
    const cell = this.getCell(roleId, controleId);
    if (!cell || cell.loading) return;

    const wasGranted = cell.isGranted;
    cell.loading = true;

    const action = wasGranted ? 'deny' : 'allow';
    const url = `${this.baseUrl}/auth/privileges/${roleId}/${controleId}/${action}`;

    this.http.patch(url, {}).subscribe({
      next: () => {
        this.flash('success', this.translate.instant('auth.responses.success.privilege_updated'));
        cell.isGranted = !wasGranted;
        cell.loading = false;
      },
      error: () => {
        cell.loading = false;
        this.flash('error', this.translate.instant('auth.permissions.errors.operation_failed'));
      },
    });
  }

  toggleCategory(category: string): void {
    const next = new Set(this.collapsedCategories);
    if (next.has(category)) {
      next.delete(category);
    } else {
      next.add(category);
    }
    this.collapsedCategories = next;
    this.cdr.markForCheck();
  }

  isCategoryCollapsed(category: string): boolean {
    return this.collapsedCategories.has(category);
  }

  getControlesByCategory(category: string): ControleDto[] {
    return this.controles.filter((c) => c.category === category);
  }

  formatRole(libelle: string): string {
    return libelle.replace(/([A-Z])/g, ' $1').trim();
  }

  private cellKey(roleId: string, controleId: string): string {
    return `${roleId}::${controleId}`;
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