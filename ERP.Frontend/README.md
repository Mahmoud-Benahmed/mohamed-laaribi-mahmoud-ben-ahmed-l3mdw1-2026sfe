# 💻 Enterprise ERP Frontend (Angular SPA)

![Angular](https://img.shields.io/badge/Angular-21.1-DD0031?style=flat-square&logo=angular&logoColor=white)
![Angular CDK](https://img.shields.io/badge/Angular_CDK-21.2-DD0031?style=flat-square&logo=angular&logoColor=white)
![Angular Material](https://img.shields.io/badge/Angular_Material-21.2-757de8?style=flat-square&logo=angular&logoColor=white)
![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?style=flat-square&logo=typescript&logoColor=white)
![RxJS](https://img.shields.io/badge/RxJS-7.8-B7178C?style=flat-square&logo=reactivex&logoColor=white)
![ngx-translate](https://img.shields.io/badge/ngx--translate-17.0-4CAF50?style=flat-square)
![chart.js](https://img.shields.io/badge/Chart.js-4.5-FF6384?style=flat-square&logo=chartdotjs&logoColor=white)
![jwt-decode](https://img.shields.io/badge/jwt--decode-4.0-orange?style=flat-square)
![Node](https://img.shields.io/badge/npm-11.8-CB3837?style=flat-square&logo=npm&logoColor=white)
![Test Runner](https://img.shields.io/badge/Tests-Vitest_4.0-6E9F18?style=flat-square&logo=vitest&logoColor=white)
![Architecture](https://img.shields.io/badge/Architecture-Standalone_Components-orange?style=flat-square)
![Multi-tenant](https://img.shields.io/badge/Multi--tenant-JWT%20%2B%20Subdomain-blueviolet?style=flat-square)

The ERP Frontend is a production-grade Angular Single-Page Application built entirely on **standalone components** and the Angular Signals API, serving as the client-facing interface for a multi-service, multi-tenant ERP backend. It implements a strict privilege-based authorization model where every route is guarded by fine-grained `PRIVILEGES` constants decoded directly from JWT claims at runtime, and every cross-cutting concern — token refresh, tenant suspension detection, global error translation, and loading state — is handled through a composable pipeline of functional `HttpInterceptorFn` interceptors. The application maintains a fully reactive tenant context (branding colors, locale, currency, timezone) sourced from per-tenant settings loaded after authentication, applied to the DOM via CSS custom properties and `ngx-translate` locale switching, ensuring each tenant's users receive a correctly branded and localized experience without a full page reload.

---

## 📦 Tech Stack & Dependencies

### Runtime Dependencies

| Package | Version | Role |
|---|---|---|
| `@angular/core` | `^21.1.0` | Framework core — standalone components, Signals, DI |
| `@angular/common` | `^21.1.0` | `CommonModule`, `CurrencyPipe`, locale registration |
| `@angular/forms` | `^21.1.0` | `ReactiveFormsModule`, `FormBuilder`, `NG_VALIDATORS` |
| `@angular/router` | `^21.1.0` | `RouterOutlet`, `CanActivateFn`, `ActivatedRoute` |
| `@angular/platform-browser` | `^21.1.0` | DOM rendering, `bootstrapApplication` |
| `@angular/compiler` | `^21.1.0` | Template compilation |
| `@angular/cdk` | `^21.2.4` | `MatTableDataSource`, overlay, scrolling primitives |
| `@ngx-translate/core` | `^17.0.0` | i18n — `TranslateService`, `TranslatePipe`, `TranslateLoader` |
| `chart.js` | `^4.5.1` | Analytics charts in tenant/stats dashboards |
| `jwt-decode` | `^4.0.0` | Client-side JWT payload decoding (no verification) |
| `rxjs` | `~7.8.0` | `BehaviorSubject`, `shareReplay`, `forkJoin`, `switchMap`, `finalize` |
| `tslib` | `^2.3.0` | TypeScript runtime helpers |

### Dev Dependencies

| Package | Version | Role |
|---|---|---|
| `@angular/cli` | `^21.1.4` | `ng serve`, `ng build`, `ng generate` |
| `@angular/build` | `^21.1.4` | Application builder (Vite-based) |
| `@angular/compiler-cli` | `^21.1.0` | Ahead-of-time compilation |
| `@angular/material` | `^21.2.4` | `MatDialog`, `MatIcon`, `MatTooltip`, `MatTableDataSource` |
| `typescript` | `~5.9.2` | Language toolchain |
| `vitest` | `^4.0.8` | Unit test runner (replaces Karma/Jasmine) |
| `jsdom` | `^27.1.0` | DOM environment for Vitest |

> **Note:** `@angular/material` is a **dev dependency** in this project — it provides UI component primitives used at build time but the final bundle is what ships. `npm@11.8.0` is pinned as the package manager via `packageManager` field.

### Available Scripts

```bash
npm start          # ng serve — dev server on default port
npm run build      # ng build — production bundle
npm run watch      # ng build --watch --configuration development
npm test           # ng test — Vitest unit test run
```

---

## 🧱 Application Architecture & Features

The application is organized around a **`ShellComponent`** (`app-shell`) that acts as the authenticated layout host — all protected routes are mounted as children of this shell, which provides the navigation sidebar and top-bar. Public entry points (`/plans`, `/onboarding`, `/login`) exist outside the shell and are accessible without a token. Every feature area is implemented as a self-contained standalone component tree, importing only the Angular modules and Material components it actually uses.

### Primary Operational Areas

**System Administration** is the most privileged area of the application, covering user lifecycle management (registration, activation/deactivation, soft-delete and restore), role and permission management through `ControleComponent` and `RoleComponent`, a full `PermissionMatrixComponent` for visual role-to-privilege assignment, an `AuditLogComponent` that streams paginated security events, and a `SystemSettingsComponent` gated behind `EDIT_SYSTEM_SETTINGS`. Password management is split between a self-service profile flow (requiring the current password) and an admin-forced reset flow, both backed by the same NIST-aligned `PasswordUtil`.

**Invoice Management** presents the most complex form in the application: a multi-step `CreateInvoiceComponent` that prefetches the full client and article caches via `forkJoin`, validates client credit limits and block status client-side before submission, applies per-category bulk discount rates dynamically, and supports both HT (tax-exclusive) and TTC (tax-inclusive) input modes via a segmented toggle. The `EditInvoiceComponent` handles DRAFT-state modifications, `ViewInvoiceComponent` renders a read-only detail view with inline PDF download, and `InvoicesComponent` manages the paginated list with server-side status filtering.

**Payment & Refund Management** implements a modal-driven payment creation flow (`CreatePaymentModal`) where a client is selected first, their unpaid invoices are loaded reactively, and amounts are allocated across multiple invoices in a single payment record. `PaymentComponent` and `ViewPaymentComponent` display the payment ledger and individual allocation details. The `RefundsComponent` and `RefundViewComponent` handle the refund request lifecycle, and `CompleteModalComponent` is used to finalize a refund with an external reference.

**Client & Article Management** each follow the same proven single-page view-mode pattern: a `signal<ViewMode>` drives conditional rendering between `list`, `list-deleted`, `create`, `edit`, and `view` states within a single component, eliminating full navigation round-trips. Both domains expose stats dashboards (active/deleted counts), sortable and client-side filterable data tables backed by `MatTableDataSource`, and inline `PaginationComponent` controls. The client domain extends this with credit limit management, return-window (`delaiRetour`) configuration, per-client category assignment, and block/unblock toggling.

**Stock Management** covers three distinct bon (goods document) types — Bon d'Entrée (supplier goods receipt), Bon de Sortie (outgoing delivery), and Bon de Retour (return) — each with date-range and entity-filtered query support. The `StockService` exposes a read-through local cache layer for articles (`/cache/articles`), clients (`/cache/clients`), and fournisseurs (`/cache/fournisseurs`) to avoid redundant cross-service HTTP calls during bon creation.

**Tenant Management** (platform admin only) provides full CRUD over tenant organizations via `TenantsComponent`, `ViewTenantComponent`, and `EditTenantComponent`, subscription plan assignment through `SubscriptionPlanService`, and a `SubscriptionExpiryComponent` that renders when the `TenantInactiveInterceptor` detects a `TENANT_INACTIVE` 403 response.

---

### Core Angular Views — Component Reference Table

| Module / Feature Area | Key Component (Selector) | Primary Functionality | Forms / Validation Used |
|---|---|---|---|
| **Shell / Layout** | `ShellComponent` (`app-shell`) | Authenticated layout host; navigation sidebar and top-bar | None |
| **Authentication** | `LoginComponent` (`app-login`) | Credential submission, token storage, redirect on `mustChangePassword` | Reactive form — `login` (alphanumeric pattern), `password` (required) |
| **Auth — Password** | `MustChangePasswordComponent` | Forced password-change flow post-login; blocks shell access until complete | Reactive form — NIST `PasswordUtil` strength validator, `notSameAs` directive |
| **Auth — Password** | `ChangePasswordComponent` | Admin-forced reset or self-service profile password change | Reactive form — current password (self), `newPassword`, `SameAsDirective` confirm field |
| **User Profile** | `ProfileComponent` | View / edit own profile (fullName, email); theme & language toggles | Reactive form — `email` pattern, `fullName` min/max length |
| **System Admin — Users** | `UsersHomeComponent` | Paginated user list; activate, deactivate, soft-delete actions | Search filter (plain input) |
| **System Admin — Users** | `RegisterComponent` | Create new user with role assignment | Reactive form — `login` (lowercase alphanumeric + underscore), `email`, `fullName`, `password` (NIST), `roleId` (required) |
| **System Admin — Users** | `DeactivatedComponent` | List and reactivate deactivated user accounts | Search filter |
| **System Admin — Users** | `DeletedUsersComponent` | List and restore soft-deleted user accounts | Search filter |
| **System Admin — Roles** | `RoleComponent` | CRUD for role definitions | Reactive form — `libelle` (required) |
| **System Admin — Controles** | `ControleComponent` | CRUD for privilege control definitions by category | Reactive form — `category`, `libelle`, `description` |
| **System Admin — Permissions** | `PermissionMatrixComponent` | Visual role-to-privilege grant matrix | Toggle slider per privilege row |
| **System Admin — Audit** | `AuditLogComponent` | Paginated audit log viewer; filter by user | Pagination controls |
| **System Admin — Settings** | `SystemSettingsComponent` | Tenant-level system settings editor | Reactive form — various typed fields |
| **Articles** | `ArticleComponent` (`app-article`) | Full article lifecycle: list, create, edit, view, delete, restore, filter by category | Reactive form — `libelle` (alphanumeric pattern), `prix` (min 0.01), `unit` (enum select), `barCode` (8–13 digit regex), `tva` (integer 0–100), `categoryId` |
| **Articles — Categories** | `ArticleCategoriesComponent` | Article category CRUD with TVA rate management | Reactive form — `name` (alpha pattern, 2–100 chars), `tva` (integer 0–100) |
| **Clients** | `ClientsComponent` | Client lifecycle: list, CRUD, block/unblock, credit limit, return-window, category assignment | Reactive form — `name`, `phone` (E.164 regex), `email`, `address`, `creditLimit`, `delaiRetour` |
| **Clients — Categories** | `ClientCategoriesComponent` | Client category CRUD with bulk-pricing discount rate | Reactive form — `name` (alpha), `discountRate` (0–100), `useBulkPricing` (boolean) |
| **Invoices — List** | `InvoicesComponent` | Paginated invoice list; status and date filtering | Search / filter controls |
| **Invoices — Create** | `CreateInvoiceComponent` | Multi-line invoice builder with client prefetch, article search, HT/TTC mode toggle, discount application, credit-limit check | Reactive form — client select, item lines (articleId, quantity, uniPriceHT, tva), discount % |
| **Invoices — Edit** | `EditInvoiceComponent` | DRAFT-state invoice modification | Same as create; pre-populated |
| **Invoices — View** | `ViewInvoiceComponent` | Read-only invoice detail with PDF download | None |
| **Payments** | `PaymentComponent` | Paginated payment ledger by status (DONE / CANCELLED) | Search filter |
| **Payments — Create** | `CreatePaymentModal` | Modal: select client → load unpaid invoices → allocate amounts → submit | Reactive form — `method` (select), `paymentDate`, `totalAmount`, per-invoice `amountAllocated` |
| **Payments — View** | `ViewPaymentComponent` | Payment detail with allocation breakdown | None |
| **Refunds** | `RefundsComponent` | Paginated refund request list | Pagination controls |
| **Refunds — View** | `RefundViewComponent` | Refund detail with line-by-line breakdown | None |
| **Refunds — Complete** | `CompleteModalComponent` | Modal to complete a pending refund with external reference | Reactive form — `externalReference` (required) |
| **Stock — Bons** | `BonsComponent` | Bon Entrée / Sortie / Retour list and creation forms | Reactive form — fournisseur/client select, article lines with quantities |
| **Stock — Fournisseurs** | `FournisseurComponent` | Fournisseur CRUD: list, create, edit, block/unblock, delete, restore | Reactive form — `name`, `address`, `phone`, `taxNumber`, `rib`, `email` (optional) |
| **Tenants** | `TenantsComponent` | Platform-admin tenant list with lifecycle actions | Pagination, search filter |
| **Tenants — View** | `ViewTenantComponent` | Tenant detail with subscription status and branding preview | None |
| **Tenants — Edit** | `EditTenantComponent` | Edit tenant metadata, branding colors, locale, currency, timezone; assign subscription | Reactive form — `name`, `email`, `phone`, `address`, `slug` (subdomain regex), `primaryColor` / `secondaryColor` (hex regex), `currency` (enum), `locale` (enum), `timezone` (enum) |
| **Tenants — Expiry** | `SubscriptionExpiryComponent` | Full-page intercept rendered when subscription is inactive | None |
| **Plans** | `PlansComponent` | Public-facing subscription plan listing (unauthenticated entry point) | None |
| **Onboarding** | `OnboardingComponent` | New-tenant onboarding wizard | Multi-step form |
| **Shared — Pagination** | `PaginationComponent` | Reusable page-number and page-size controls with `EventEmitter` outputs | `pageSize` select |
| **Shared — Modal** | `ModalComponent` | Generic confirmation/alert dialog (Angular Material `MatDialog`) with optional inline inputs | Optional `ModalInput[]` typed fields |
| **Shared — Loading** | `LoadingOverlayComponent` | Full-viewport loading spinner driven by `LoadingService` observable | None |
| **Shared — Toggle** | `ToggleSliderComponent` | Styled boolean toggle used in permission matrix and settings | None |

---

## 🔄 Cross-Cutting Concerns & Core Mechanics

The application routes all HTTP traffic through a four-interceptor pipeline registered in strict order inside `app.config.ts`:

```
LoadingInterceptor → errorTranslateInterceptor → TenantInactiveInterceptor → AuthInterceptor
```

Each interceptor is a standalone `HttpInterceptorFn` (Angular's functional interceptor API), making them tree-shakeable, independently testable, and free of constructor injection coupling.

---

### Multi-Tenant Routing & Subdomain Interception

Tenant identity is carried entirely inside the **JWT payload** — the `tenantId`, `slug`, and tenant-scoped `privilege` claims are decoded client-side via `jwtDecode<JwtPayload>` inside `AuthService`. No separate tenant-resolution HTTP call is made at navigation time.

Upon a successful login, `UserSettingsService` — which orchestrates startup sequencing via Angular `effect()` — calls `TenantService.loadTenantSettings(tenantId)` to fetch the full `TenantSettingsDto` (name, email, phone, address, slug, logoUrl, primaryColor, secondaryColor, currency, locale, timezone). The settings are stored in an Angular `signal<TenantSettingsDto | null>` and exposed as a set of fine-grained `computed()` signals (`name`, `currency`, `locale`, `primaryColor`, etc.) so that any component can inject `TenantService` and read individual settings reactively without triggering a full-object comparison.

Branding is applied to the DOM by `TenantThemeService`, which sets `--primary-color` and `--secondary-color` CSS custom properties directly on `document.documentElement`, and persists the currency and locale to `localStorage` via `CurrencyConfigService` so that Angular's `CurrencyPipe` picks up the correct formatting on the next render cycle.

The **`TenantInactiveInterceptor`** sits in third position in the pipeline and watches every `HttpErrorResponse`. If it receives a `403` with `error.code === 'TENANT_INACTIVE'`, it immediately calls `router.navigate(['/subscription-expiry'])` and returns `EMPTY` — swallowing the error so no downstream component error handler fires. This guarantees that any expired-subscription state, regardless of which API call triggered it, funnels the user to the `SubscriptionExpiryComponent` without any additional component-level logic.

---

### Security & Authorization Framework

**`authGuard`** (`CanActivateFn`) is the single route guard applied to every protected path. Its logic runs in three stages:

1. **Token validity check:** If `AuthService.isLoggedIn()` returns `true` (access token present and `exp * 1000 > Date.now()`), the guard proceeds immediately without a network call. If the access token has expired but a refresh token exists in `localStorage`, the guard initiates a silent refresh via `AuthService.refresh()` — the observable returned by the guard suspends navigation until the refresh resolves.

2. **Mandatory password enforcement:** If `mustChangePassword` is `true` in `localStorage` and the target route is not `must-change-password`, the guard issues `router.createUrlTree(['/must-change-password'])`. Conversely, once the flag is cleared, attempting to navigate back to `/must-change-password` redirects to `/home`.

3. **Privilege-based access:** Routes declare their required privileges in `route.data['privileges']` using the `pickPrivileges()` helper, which selects typed keys from the centrally defined `PRIVILEGES` constant object. The guard calls `AuthService.hasPrivilege(...privileges)` which checks the decoded `privilege` claim array from the JWT. Denial does not result in a 403 page — instead, the guard performs a cascading redirect to the first module the user actually has access to (`/users` → `/articles` → `/clients` → `/stock` → `/home`).

**`AuthInterceptor`** (fourth in the pipeline) attaches `Authorization: Bearer <token>` to every outbound request that is not an `i18n` asset load. When it receives a `401`, it checks the error code: `AUTH_009` (account deleted) and `AUTH_008` (invalid token) trigger immediate logout; any other `401` initiates a shared token-refresh via `shareReplay(1)` — a single `refreshInProgress$` observable is shared across all concurrent failing requests so that only one refresh call is made, and all queued requests are retried with the new token on success. Rate-limit (`429`), forbidden (`403`), not-found (`404`), and gateway-error (`502/503/504`) responses each display a targeted `MatDialog` modal with a localized error message before routing the user appropriately.

**`AuthService`** decodes and caches the JWT payload on first access, re-decoding only when the stored token string changes. Claim accessors (`TenantId`, `UserId`, `Slug`, `Role`, `Privileges`, `Language`, `Theme`) are all computed from this cached payload, making them synchronous and O(1). On logout, `revoke()` is called against the backend to invalidate the refresh token server-side before `clearSession()` wipes `localStorage`.

---

### Global Error Handling & Localization

The **`errorTranslateInterceptor`** runs second in the pipeline, after the loading interceptor, so it processes all API failures before any component error handler sees them. It identifies structured backend errors using the `isHttpError()` type guard (checking for the `code` string property on the response body). For each structured error, it attempts to find a translated message by searching two i18n namespaces in order — `auth.responses.errors.<code>` then `tenant.responses.errors.<code>` — and replaces the raw backend message with the translated string before re-throwing. Unstructured errors (network failures, `status === 0`, 5xx without a body) are normalized into the same `HttpError` shape with appropriate fallback messages (`SERVER_UNREACHABLE`, `RATE_LIMIT`, `INTERNAL_ERROR`).

**Internationalization** is powered by `@ngx-translate/core` with a custom inline `HttpTranslateLoader` (no external loader package dependency) that fetches translation JSON files from `/assets/i18n/${lang}.json`. The application pre-registers five Angular locale datasets at startup:

| Registered Locale | Constant | Use Case |
|---|---|---|
| `fr-MA` | `localeFrMA` | Moroccan French — default currency/date formatting |
| `fr-TN` | `localeFrTN` | Tunisian French |
| `en-US` | `localeEnUS` | Standard English |
| `fr-FR` | `localeFrFR` | Metropolitan French |
| `en-GB` | `localeEnGB` | British English |

The active language is driven by `UserSettingsService`, which persists the user's preference both to `localStorage` (for flicker-free reload) and to the backend via `AuthService.updateSettings()`. Language toggling calls `TranslateService.use(language)` and sets the `lang` attribute on `<html>`. Theme switching sets `data-theme` on `document.documentElement`, which CSS custom property declarations pick up via `[data-theme="dark"]` attribute selectors.

Tenant-specific currency and locale are saved to `localStorage` by `CurrencyConfigService.saveFromBranding()` whenever branding loads, making the values available to Angular's `CurrencyPipe` without re-injection.

---

### Custom Utility & Directives

**`SameAsDirective`** (`[sameAs]`, standalone) is a synchronous `NG_VALIDATORS` custom validator that validates whether the host control's value matches a sibling control identified by name via `control.parent?.get(this.sameAs)`. It is used on password-confirmation fields — if both controls are non-empty and equal, it emits `{ sameAs: true }`. This is distinct from the confirm-password use case: `sameAs` is used where two fields must match (e.g., a "repeat email" field).

**`NotSameAsDirective`** (`[notSameAs]`, standalone) is the logical inverse — it emits `{ notSameAs: true }` when the control's value equals the sibling control's value. This enforces the "new password must differ from current password" rule at the template level, complementing the same check performed inside `PasswordUtil.checkPassword()`.

**`PasswordUtil`** is a zero-dependency pure TypeScript module implementing the NIST SP 800-63B and OWASP password guidelines:

- Minimum length 8 characters (NIST floor); 12+ scores as a bonus; 15+ scores as "strong"; 20+ enters passphrase territory.
- Maximum length 128 characters — no arbitrary short cap (NIST §5.1.1.2).
- No mandatory composition rules (uppercase, digit, symbol are NOT required — NIST §5.1.1.2 explicitly prohibits such rules).
- Common/breached password screening against a built-in `Set` of 20 known-breached passwords, with an architectural comment pointing to a k-anonymity HaveIBeenPwned API integration path for production.
- Sequential character and keyboard-walk penalties applied to the score without hard rejection.
- `generatePassword()` uses **`crypto.getRandomValues`** (Web Crypto CSPRNG) exclusively — `Math.random()` is never used for security-sensitive values. The function guarantees at least one character from each of four classes via a guaranteed seed array, then Fisher-Yates shuffles the full array with a `Uint32Array` drawn from `crypto.getRandomValues` to ensure uniform distribution and no predictable positioning.
- The `PasswordMessages` interface allows the `PasswordService` wrapper to inject locale-specific error strings (`DEFAULT_ENGLISH_MESSAGES` / `DEFAULT_FRENCH_MESSAGES`) based on `TranslateService.currentLang` at call time.

**`RegexPatterns`** is a shared constants object (used by both frontend validation and backend-mirrored DTOs) defining: `safeText` (Unicode letter/number/space/punctuation), `phone` (E.164 format), `email`, `alphaNumeric` (Unicode), `categoryCode`, `hexColor` (#RRGGBB), `subdomainSlug` (alphanumeric ≥ 3 chars), `alpha` (Unicode letters only), `login` (lowercase alphanumeric + underscore), `barCode` (8–13 digits), `integer`, and `decimal`.

**`PaginationComponent`** is a fully reusable standalone component with `@Input()` bindings for `pageNumber`, `totalPages`, `pageSize`, and `pageSizeOptions`, and `@Output()` `EventEmitter`s for `pageChange` and `pageSizeChange`. Every paginated view in the application composes this component identically, keeping pagination rendering and page-size selection out of feature components entirely.

**`ModalComponent`** wraps Angular Material's `MatDialog` to provide a unified confirmation/alert modal. It accepts a `ModalData` input with optional `ModalInput[]` typed fields (text, password, email, number) rendered inline in the dialog body, enabling input-collecting confirmations (e.g., "enter amount to refund") without separate form components.

---

## ⚙️ Configuration & Bootstrap Flow

The application bootstraps through `bootstrapApplication(App, appConfig)` with all framework-level providers declared in `app.config.ts` as a single `ApplicationConfig` object — no `NgModule` is involved anywhere in the application. The build toolchain is Angular CLI `21.1.4` backed by the Vite-based `@angular/build` builder, and the test runner is **Vitest `4.0`** with `jsdom` as the DOM environment (replacing the legacy Karma + Jasmine stack).

```typescript
// app.config.ts — abridged

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),provideHttpClient(withInterceptors([
      apiInterceptor,
      LoadingInterceptor,
      errorTranslateInterceptor,
      TenantInactiveInterceptor,
      AuthInterceptor
    ])),
    provideTranslateService({
      fallbackLang: 'en',
      loader: {
        provide: TranslateLoader,
        useFactory: (http: HttpClient) => new HttpTranslateLoader(http),
        deps: [HttpClient]
      }
    })
  ]
};
```

**`provideHttpClient(withInterceptors([...]))`** uses Angular's functional interceptor API, which means the interceptors are provided as plain functions rather than class-based `HTTP_INTERCEPTORS` multi-providers. The order of the array is the order of execution on the outbound request path (and reverse on the response path), making the pipeline's sequencing explicit and auditable.

**`HttpTranslateLoader`** is an inline class implementing `TranslateLoader` from `@ngx-translate/core`. Rather than pulling in a third-party loader package (e.g., `@ngx-translate/http-loader`), it directly injects `HttpClient` and performs `this.http.get(`assets/i18n/${lang}.json`)`. The factory function in the `useFactory` provider receives `HttpClient` via the `deps: [HttpClient]` array, keeping it compatible with Angular's tree-shakeable DI system without requiring a separate `InjectionToken`.

**`provideTranslateService`** is configured with `fallbackLang: 'en'`, meaning any missing translation key in the active language falls back to the English bundle rather than displaying the raw key string to the user.

The root `App` component (`app-root`) performs two initialization tasks in its constructor: it calls `TranslateService.reloadLang('en')` with `pipe(take(1))` to ensure the English base bundle is loaded before any component renders, and it injects `UserSettingsService` whose constructor-time `effect()` calls then sequence the theme application, language application, tenant branding load, and tenant settings hydration — all driven reactively from the `userProfile$` BehaviorSubject emitting the first profile value from `localStorage` on startup.

The **route table** in `app.routes.ts` is structured in three tiers:
1. **Public routes** (`/plans`, `/onboarding`, `/login`, `/must-change-password`) — no shell, no guard (or minimal `authGuard` check only).
2. **Shell-wrapped authenticated routes** — a single parent `{ path: '', component: ShellComponent, canActivate: [authGuard] }` wraps all feature routes as children, so the `authGuard` is evaluated once at the shell boundary and all child routes inherit it.
3. **Wildcard fallback** — `{ path: '**', redirectTo: 'plans' }` routes unknown paths back to the public landing page.

Route-level privilege declarations use a typed `pickPrivileges(category, keys)` helper that selects values from the `PRIVILEGES` constant at module load time, providing type safety and preventing string typos in route data — the guard receives an already-resolved `string[]` of privilege identifiers rather than raw literals.

---

## 🐳 Local Development & Docker Deployment Notes

### Local Development (without Docker)

For local development using `ng serve` (or `npm start`), you must set the `production` flag in `src/app/environment.ts` to `false`:

```ts
export const environment = {
  production: false,   // ← must be false
  apiUrl: '/api',
  // ... rest of config
};
```

This disables production‑only behaviours such as forced password change redirections and enables development tools.

### Docker Compose (Full Stack)

When running the full stack with Docker Compose (`docker-compose.yaml`), the application is built inside a container and served by Nginx. The Nginx configuration expects the frontend to be reachable via the domain `erp.local` and its subdomains (e.g., `*.erp.local`). To make this work on your local machine, you **must** add the following entry to your operating system’s `hosts` file:

```
127.0.0.1   erp.local
127.0.0.1   *.erp.local
```

> **Windows** (`C:\Windows\System32\drivers\etc\hosts`)
> **Linux / macOS** (`/etc/hosts`)

This ensures that any request to `erp.local` or `tenant.erp.local` resolves to your local Docker host, and Nginx can correctly proxy the request to the Gateway service.

### Local Infrastructure Only (docker-compose.local.yaml)

If you only want to run the supporting infrastructure (SQL Server, MongoDB, Kafka, Redis) and run the .NET microservices directly from Visual Studio or Rider, use the `docker-compose.local.yaml` file:

```bash
docker-compose -f docker-compose.local.yaml up -d
```

In this mode the frontend is **not** built inside Docker; you run `ng serve` locally. You must still set `production: false` in `environment.ts` and point your local Angular dev server to the correct API endpoint (usually `http://localhost:8080` via the Gateway, but the exact URL depends on your local setup).
