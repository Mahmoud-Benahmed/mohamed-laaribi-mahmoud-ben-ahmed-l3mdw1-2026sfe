import { Routes } from '@angular/router';
import { authGuard } from './guard/auth.guard';
import { PRIVILEGES } from './services/auth/auth.service';

function pickPrivileges(category: keyof typeof PRIVILEGES, keys: string[]) {
  return keys.map(k => PRIVILEGES[category][k as keyof typeof PRIVILEGES[typeof category]]);
}

export const routes: Routes = [

  // ── Public landing ───────────────────────────────────────────────────────
  {
    path: '',
    loadComponent: () => import('./components/plans/plans').then(m => m.PlansComponent),
  },
  {
    path: 'plans',
    loadComponent: () => import('./components/plans/plans').then(m => m.PlansComponent),
  },
  {
    path: 'onboarding',
    loadComponent: () => import('./components/onboarding/onboarding').then(m => m.OnboardingComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./components/login/login').then(m => m.LoginComponent),
  },
  {
    path: 'must-change-password',
    loadComponent: () => import('./components/user/must-change-password/must-change-password').then(m => m.MustChangePasswordComponent),
    canActivate: [authGuard],
  },

  // ── Authenticated shell ──────────────────────────────────────────────────
  {
    path: '',
    loadComponent: () => import('./components/shell/shell').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [

      // ── Home / Profile ─────────────────────────────────────────────────
      {
        path: 'home',
        loadComponent: () => import('./components/home/home').then(m => m.HomeComponent),
      },
      {
        path: 'profile',
        loadComponent: () => import('./components/user/profile/profile').then(m => m.ProfileComponent),
      },
      {
        path: 'change-password',
        loadComponent: () => import('./components/user/must-change-password/must-change-password').then(m => m.MustChangePasswordComponent),
      },

      // ── System admin ───────────────────────────────────────────────────
      {
        path: 'audit-log',
        loadComponent: () => import('./components/system-admin/audit-log/audit-log').then(m => m.AuditLogComponent),
        data: { privileges: pickPrivileges('AUDIT', ['MANAGE_AUDITLOGS']) },
      },
      {
        path: 'permissions',
        loadComponent: () => import('./components/system-admin/permission-matrix/permission-matrix').then(m => m.PermissionMatrixComponent),
        data: { privileges: pickPrivileges('USERS', ['ASSIGN_ROLES']) },
      },
      {
        path: 'system-settings',
        loadComponent: () => import('./components/system-admin/system-settings/system-settings').then(m => m.SystemSettingsComponent),
        data: { privileges: pickPrivileges('USERS', ['EDIT_SYSTEM_SETTINGS']) },
      },

      // ── Users ──────────────────────────────────────────────────────────
      {
        path: 'users',
        loadComponent: () => import('./components/system-admin/users/home/home').then(m => m.UsersHomeComponent),
        data: { privileges: pickPrivileges('USERS', ['VIEW_USERS', 'CREATE_USER', 'UPDATE_USER', 'DELETE_USER', 'DEACTIVATE_USER']) },
      },
      {
        path: 'users/register',
        loadComponent: () => import('./components/system-admin/users/register/register').then(m => m.RegisterComponent),
        data: { privileges: pickPrivileges('USERS', ['CREATE_USER']) },
      },
      {
        path: 'users/deactivated',
        loadComponent: () => import('./components/system-admin/users/deactivated/deactivated').then(m => m.DeactivatedComponent),
        data: { privileges: pickPrivileges('USERS', ['ACTIVATE_USER', 'DEACTIVATE_USER']) },
      },
      {
        path: 'users/deleted',
        loadComponent: () => import('./components/system-admin/users/deleted/deleted').then(m => m.DeletedUsersComponent),
        data: { privileges: pickPrivileges('USERS', ['RESTORE_USER']) },
      },
      {
        path: 'users/controles',
        loadComponent: () => import('./components/system-admin/controles/controles').then(m => m.ControleComponent),
        data: { privileges: pickPrivileges('USERS', ['ASSIGN_ROLES']) },
      },
      {
        path: 'users/roles',
        loadComponent: () => import('./components/system-admin/roles/roles').then(m => m.RoleComponent),
        data: { privileges: pickPrivileges('USERS', ['ASSIGN_ROLES']) },
      },
      {
        path: 'users/change-password/:authUserId',
        loadComponent: () => import('./components/system-admin/users/change-password/change-password').then(m => m.ChangePasswordComponent),
        data: { privileges: pickPrivileges('USERS', ['VIEW_USERS', 'UPDATE_USER']) },
      },
      {
        path: 'users/:authUserId',
        loadComponent: () => import('./components/user/profile/profile').then(m => m.ProfileComponent),
        data: { privileges: pickPrivileges('USERS', ['VIEW_USERS', 'UPDATE_USER']) },
      },

      // ── Articles ───────────────────────────────────────────────────────
      {
        path: 'articles/categories',
        loadComponent: () => import('./components/articles/categories/categories').then(m => m.ArticleCategoriesComponent),
        data: { privileges: pickPrivileges('ARTICLES', ['VIEW_ARTICLES', 'CREATE_ARTICLE', 'UPDATE_ARTICLE']) },
      },
      {
        path: 'articles/:id',
        loadComponent: () => import('./components/articles/home/home').then(m => m.ArticleComponent),
        data: { privileges: pickPrivileges('ARTICLES', ['VIEW_ARTICLES']) },
      },
      {
        path: 'articles',
        loadComponent: () => import('./components/articles/home/home').then(m => m.ArticleComponent),
        data: { privileges: pickPrivileges('ARTICLES', ['VIEW_ARTICLES', 'CREATE_ARTICLE', 'UPDATE_ARTICLE', 'DELETE_ARTICLE']) },
      },

      // ── Clients ────────────────────────────────────────────────────────
      {
        path: 'clients/categories',
        loadComponent: () => import('./components/clients/categories/categories').then(m => m.ClientCategoriesComponent),
        data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS', 'CREATE_CLIENT', 'UPDATE_CLIENT']) },
      },
      {
        path: 'clients/categories/:id',
        loadComponent: () => import('./components/clients/categories/categories').then(m => m.ClientCategoriesComponent),
        data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS']) },
      },
      {
        path: 'clients/:id',
        loadComponent: () => import('./components/clients/home/home').then(m => m.ClientsComponent),
        data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS', 'UPDATE_CLIENT', 'DELETE_CLIENT']) },
      },
      {
        path: 'clients',
        loadComponent: () => import('./components/clients/home/home').then(m => m.ClientsComponent),
        data: { privileges: pickPrivileges('CLIENTS', ['VIEW_CLIENTS', 'CREATE_CLIENT', 'UPDATE_CLIENT', 'DELETE_CLIENT']) },
      },

      // ── Invoices ───────────────────────────────────────────────────────
      {
        path: 'invoices/create',
        loadComponent: () => import('./components/invoices/create/create').then(m => m.CreateInvoiceComponent),
        data: { privileges: pickPrivileges('INVOICES', ['CREATE_INVOICE']) },
      },
      {
        path: 'invoices/edit/:id',
        loadComponent: () => import('./components/invoices/edit/edit').then(m => m.EditInvoiceComponent),
        data: { privileges: pickPrivileges('INVOICES', ['UPDATE_DRAFT_INVOICE']) },
      },
      {
        path: 'invoices/:id',
        loadComponent: () => import('./components/invoices/view/view').then(m => m.ViewInvoiceComponent),
        data: { privileges: pickPrivileges('INVOICES', ['VIEW_INVOICES']) },
      },
      {
        path: 'invoices',
        loadComponent: () => import('./components/invoices/invoices').then(m => m.InvoicesComponent),
        data: { privileges: pickPrivileges('INVOICES', ['VIEW_INVOICES']) },
      },

      // ── Stock ──────────────────────────────────────────────────────────
      {
        path: 'stock/fournisseurs/:id',
        loadComponent: () => import('./components/stock/fournisseur/fournisseur').then(m => m.FournisseurComponent),
        data: { privileges: pickPrivileges('STOCK', ['VIEW_STOCK']) },
      },
      {
        path: 'stock/fournisseurs',
        loadComponent: () => import('./components/stock/fournisseur/fournisseur').then(m => m.FournisseurComponent),
        data: { privileges: pickPrivileges('STOCK', ['VIEW_STOCK', 'UPDATE_STOCK', 'ADD_ENTRY']) },
      },
      {
        path: 'stock/bons',
        loadComponent: () => import('./components/stock/bon/bon').then(m => m.BonsComponent),
        data: { privileges: pickPrivileges('STOCK', ['VIEW_STOCK', 'UPDATE_STOCK', 'ADD_ENTRY']) },
      },

      // ── Payments ───────────────────────────────────────────────────────
      {
        path: 'payments/refunds/:id',
        loadComponent: () => import('./components/payments/refund/view/view').then(m => m.RefundViewComponent),
        data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS', 'MANAGE_PAYMENTS']) },
      },
      {
        path: 'payments/refunds',
        loadComponent: () => import('./components/payments/refund/refund').then(m => m.RefundsComponent),
        data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS', 'MANAGE_PAYMENTS']) },
      },
      {
        path: 'payments/:id',
        loadComponent: () => import('./components/payments/view/view').then(m => m.ViewPaymentComponent),
        data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS']) },
      },
      {
        path: 'payments',
        loadComponent: () => import('./components/payments/payments').then(m => m.PaymentComponent),
        data: { privileges: pickPrivileges('PAYMENTS', ['VIEW_PAYMENTS', 'RECORD_PAYMENT', 'CANCEL_PAYMENT']) },
      },

      // ── Tenants ────────────────────────────────────────────────────────
      // NOTE: 'tenants/edit/:id' must come before 'tenants/:id' to avoid
      //       'edit' being matched as the :id param.
      {
        path: 'tenants/edit/:id',
        loadComponent: () => import('./components/tenants/edit/edit').then(m => m.EditTenantComponent),
        data: { privileges: pickPrivileges('TENANTS', ['UPDATE_TENANT', 'MANAGE_SUBSCRIPTIONS']) },
      },
      {
        path: 'tenants/:id',
        loadComponent: () => import('./components/tenants/view/view').then(m => m.ViewTenantComponent),
        data: { privileges: pickPrivileges('TENANTS', ['VIEW_TENANTS', 'MANAGE_SUBSCRIPTIONS', 'VIEW_BILLING']) },
      },
      {
        path: 'tenants',
        loadComponent: () => import('./components/tenants/home/home').then(m => m.TenantsComponent),
        data: {
          privileges: pickPrivileges('TENANTS', [
            'VIEW_TENANTS', 'CREATE_TENANT', 'UPDATE_TENANT',
            'DELETE_TENANT', 'RESTORE_TENANT', 'MANAGE_SUBSCRIPTIONS', 'VIEW_BILLING',
          ]),
        },
      },

      // ── Subscription expiry ────────────────────────────────────────────
      {
        path: 'subscription-expiry',
        loadComponent: () => import('./components/tenants/subscription-expiry/subscription-expiry').then(m => m.SubscriptionExpiryComponent),
      },

      // ── Default shell child ────────────────────────────────────────────
      { path: '', redirectTo: 'home', pathMatch: 'full' },
    ],
  },

  // ── Fallback ─────────────────────────────────────────────────────────────
  { path: '**', redirectTo: 'plans' },
];