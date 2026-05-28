import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login';
import { HomeComponent } from './components/home/home';
import { RegisterComponent } from './components/system-admin/users/register/register';
import { authGuard } from './guard/auth.guard';
import { UsersHomeComponent } from './components/system-admin/users/home/home';
import { ShellComponent } from './components/shell/shell';
import { ProfileComponent } from './components/user/profile/profile';
import { DeactivatedComponent } from './components/system-admin/users/deactivated/deactivated';
import { MustChangePasswordComponent } from './components/user/must-change-password/must-change-password';
import { PermissionMatrixComponent } from './components/system-admin/permission-matrix/permission-matrix';
import { ArticleComponent } from './components/articles/home/home';
import { AuditLogComponent } from './components/system-admin/audit-log/audit-log';
import { DeletedUsersComponent } from './components/system-admin/users/deleted/deleted';
import { ControleComponent } from './components/system-admin/controles/controles';
import { RoleComponent } from './components/system-admin/roles/roles';
import { ArticleCategoriesComponent } from './components/articles/categories/categories';
import { PRIVILEGES } from './services/auth/auth.service';
import { ClientsComponent } from './components/clients/home/home';
import { ClientCategoriesComponent } from './components/clients/categories/categories';
import { FournisseurComponent } from './components/stock/fournisseur/fournisseur';
import { BonsComponent } from './components/stock/bon/bon';
import { ChangePasswordComponent } from './components/system-admin/users/change-password/change-password';
import { LoadingOverlayComponent } from './components/loading-overlay/loading-overlay';
import { InvoicesComponent } from './components/invoices/invoices';
import { EditInvoiceComponent } from './components/invoices/edit/edit';
import { CreateInvoiceComponent } from './components/invoices/create/create';
import { ViewInvoiceComponent } from './components/invoices/view/view';
import { CreatePaymentModal } from './components/payments/create-modal/create-modal';
import { PaymentComponent } from './components/payments/payments';
import { ViewPaymentComponent } from './components/payments/view/view';
import { RefundsComponent } from './components/payments/refund/refund';
import { RefundViewComponent } from './components/payments/refund/view/view';
import { PlansComponent } from './components/tenants/plans/plans';
import { OnboardingComponent } from './components/tenants/onboarding/onboarding';

function pickPrivileges(category: keyof typeof PRIVILEGES, keys: string[]) {
  return keys.map(k => PRIVILEGES[category][k as keyof typeof PRIVILEGES[typeof category]]);
}

export const routes: Routes = [

  // ── Public routes (no guard) ─────────────────────────────────────────────
  { path: 'plans',       component: PlansComponent },
  { path: 'onboarding',  component: OnboardingComponent },
  { path: 'login',       component: LoginComponent },
  { path: 'must-change-password', component: MustChangePasswordComponent, canActivate: [authGuard] },

  // ── Authenticated shell ──────────────────────────────────────────────────
  {
    path: '', component: ShellComponent, canActivate: [authGuard],
    children: [
      { path: 'home', component: HomeComponent },
      { path: 'profile', component: ProfileComponent },
      { path: 'change-password', component: MustChangePasswordComponent },
      { path: 'change-password/:authUserId', component: ChangePasswordComponent, data: { privileges: pickPrivileges('USERS', ['VIEW_USERS', 'UPDATE_USER']) } },
      { path: 'audit-log', component: AuditLogComponent, data: { privileges: pickPrivileges('AUDIT', ['MANAGE_AUDITLOGS']) } },
      { path: 'permissions', component: PermissionMatrixComponent, data: { privileges: pickPrivileges('USERS', ['ASSIGN_ROLES']) } },

      { path: 'users', component: UsersHomeComponent, data: { privileges: pickPrivileges('USERS', ['VIEW_USERS','CREATE_USER','UPDATE_USER','DELETE_USER','DEACTIVATE_USER']) } },
      { path: 'users/register', component: RegisterComponent, data: { privileges: pickPrivileges('USERS', ['CREATE_USER']) } },
      { path: 'users/deactivated', component: DeactivatedComponent, data: { privileges: pickPrivileges('USERS', ['ACTIVATE_USER','DEACTIVATE_USER']) } },
      { path: 'users/deleted', component: DeletedUsersComponent, data: { privileges: pickPrivileges('USERS', ['RESTORE_USER']) } },
      { path: 'users/controles', component: ControleComponent, data: { privileges: pickPrivileges('USERS', ['ASSIGN_ROLES']) } },
      { path: 'users/roles', component: RoleComponent, data: { privileges: pickPrivileges('USERS', ['ASSIGN_ROLES']) } },
      { path: 'users/:authUserId', component: ProfileComponent, data: { privileges: pickPrivileges('USERS', ['VIEW_USERS','UPDATE_USER']) } },

      { path: 'articles/categories', component: ArticleCategoriesComponent, data: { privileges: pickPrivileges('ARTICLES', ['VIEW_ARTICLES','CREATE_ARTICLE','UPDATE_ARTICLE']) } },
      { path: 'articles/:id', component: ArticleComponent, data: { privileges: pickPrivileges('ARTICLES', ['VIEW_ARTICLES']) } },
      { path: 'articles', component: ArticleComponent, data: { privileges: pickPrivileges('ARTICLES', ['VIEW_ARTICLES','CREATE_ARTICLE','UPDATE_ARTICLE','DELETE_ARTICLE']) } },

      { path: 'clients/categories', component: ClientCategoriesComponent, data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS','CREATE_CLIENT','UPDATE_CLIENT']) } },
      { path: 'clients/categories/:id', component: ClientCategoriesComponent, data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS']) } },
      { path: 'clients/:id', component: ClientsComponent, data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS','UPDATE_CLIENT','DELETE_CLIENT']) } },
      { path: 'clients', component: ClientsComponent, data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS','CREATE_CLIENT','UPDATE_CLIENT','DELETE_CLIENT']) } },

      { path: 'invoices/edit/:id', component: EditInvoiceComponent, data: { privileges: pickPrivileges('INVOICES', ['UPDATE_DRAFT_INVOICE']) } },
      { path: 'invoices/create', component: CreateInvoiceComponent, data: { privileges: pickPrivileges('INVOICES', ['CREATE_INVOICE']) } },
      { path: 'invoices/:id', component: ViewInvoiceComponent, data: { privileges: pickPrivileges('INVOICES', ['VIEW_INVOICES'])} },
      { path: 'invoices', component: InvoicesComponent, data: { privileges: pickPrivileges('INVOICES', ['VIEW_INVOICES']) } },

      { path: 'stock/fournisseurs/:id', component: FournisseurComponent, data: { privileges: pickPrivileges('STOCK', ['VIEW_STOCK']) } },
      { path: 'stock/fournisseurs', component: FournisseurComponent, data: { privileges: pickPrivileges('STOCK', ['VIEW_STOCK','UPDATE_STOCK','ADD_ENTRY']) } },
      { path: 'stock/bons', component: BonsComponent, data: { privileges: pickPrivileges('STOCK', ['VIEW_STOCK', 'UPDATE_STOCK', 'ADD_ENTRY']) } },

      { path: 'payments/refunds/:id', component: RefundViewComponent, data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS', "'MANAGE_PAYMENTS'"]) } },
      { path: 'payments/refunds', component: RefundsComponent, data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS', "'MANAGE_PAYMENTS'"]) } },
      { path: 'payments/:id', component: ViewPaymentComponent, data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS']) } },
      { path: 'payments', component: PaymentComponent, data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS', 'RECORD_PAYMENT', 'CANCEL_PAYMENT']) } },

      // Default shell child → home
      { path: '', redirectTo: 'home', pathMatch: 'full' },
    ]
  },

  // ── Root redirect & fallback ─────────────────────────────────────────────
  { path: '**', redirectTo: 'plans' },
];