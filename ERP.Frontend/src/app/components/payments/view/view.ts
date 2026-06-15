import { CommonModule, Location } from '@angular/common';
import { ChangeDetectorRef, Component, computed, DestroyRef, inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ClientResponseDto, ClientsService } from '../../../services/clients/clients.service';
import { StockItem, StockService } from '../../../services/stock.service';
import { AuthService, PRIVILEGES } from '../../../services/auth/auth.service';
import { InvoiceDto, InvoiceService } from '../../../services/invoice.service';
import { ArticleService, UnitEnum } from '../../../services/articles/articles.service';
import { catchError, firstValueFrom, forkJoin, map, Observable, of, take, tap } from 'rxjs';
import { HttpError } from '../../../interfaces/HttpError';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ModalComponent } from '../../modal/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LoadingOverlayComponent } from "../../loading-overlay/loading-overlay";
import { InvoiceCacheDto, PaymentDto, PaymentService } from '../../../services/payment.service';
import { CreatePaymentModal } from '../../payments/create-modal/create-modal';

type InvoiceWithPayment = InvoiceDto & {
  paidAmount: number;
  remainingAmount: number;
};

@Component({
  selector: 'app-payments-view',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe,
    RouterLink
],
  templateUrl: './view.html',
  styleUrl: './view.scss',
})
export class ViewPaymentComponent implements OnInit, OnDestroy {
  private themeObserver: MutationObserver | null = null;
  private readonly destroyRef = inject(DestroyRef);
  private translate = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);
  private location= inject(Location);

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null

  private invoicesCache = new Map<string, InvoiceDto>();
  private invoiceFetchMap = new Map<string, Observable<InvoiceDto | null>>();

  invoicesCached: InvoiceWithPayment[] = [];

  selectedPayment: PaymentDto | null= null;
  paymentIdFromRoute: string|null=null;
  client$!: Observable<ClientResponseDto | null>;

  readonly PRIVILEGES = PRIVILEGES;

  constructor(
      public authService: AuthService,
      private invoiceService: InvoiceService,
      private paymentService: PaymentService,
      private clientService: ClientsService,
      private route: ActivatedRoute,
      private router: Router,
      private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    if(!this.authService.hasPrivilege(PRIVILEGES.PAYMENTS.VIEW_PAYMENTS)){
      this.cancel();
      return;
    }

    this.paymentIdFromRoute = this.route.snapshot.paramMap.get('id');

    if (!this.paymentIdFromRoute) {
      this.cancel();
      return;
    }

    this.reload();
  }

  reload(): void {
    if (!this.paymentIdFromRoute) return;

    forkJoin({
      invoices: this.invoiceService.getAll(1, 100),
      invoiceCache: this.paymentService.getInvoicesCacheByStatus('UNPAID',1, 100),
      cache:   this.paymentService.getPaymentById(this.paymentIdFromRoute)
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ invoiceCache, invoices, cache}) => {
          this.selectedPayment= cache;

          this.client$ = this.clientService.getById(cache.clientId).pipe(
            catchError(() => of(null))
          );

          const cacheMap = new Map(
            invoiceCache.items.map(c => [c.id, c])
          );

          this.invoicesCached = invoices.items.map(inv => {
            const cache = cacheMap.get(inv.id);

            return {
              ...inv,
              paidAmount: cache?.paidAmount ?? 0,
              remainingAmount: cache?.remainingAmount ?? inv.totalTTC
            };
          });
          this.cdr.markForCheck();
        },
        error: (err) => {
          const errorMsg = (err.error as HttpError)?.message
            ?? this.translate.instant('invoices.responses.errors.load_failed');
          this.flash('error', errorMsg);
          this.cancel();
        }
      });
  }

    flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; }, 4000);
    }
  }



  getByInvoiceId(id: string): InvoiceWithPayment | null {
    const cached = this.invoicesCache.get(id);
    if (cached) {
      // Find matching cache from paymentService or compute zero values
      const cacheInfo = this.invoicesCached.find(i => i.id === id);
      return {
        ...cached,
        paidAmount: cacheInfo?.paidAmount ?? 0,
        remainingAmount: cacheInfo?.remainingAmount ?? cached.totalTTC
      };
    }

    if (this.invoiceFetchMap.has(id)) return null;

    const fetch$ = this.invoiceService.getById(id).pipe(
      take(1),
      map(invoice => {
        if (invoice) this.invoicesCache.set(id, invoice);
        this.invoiceFetchMap.delete(id);
        return invoice;
      }),
      catchError(() => {
        this.invoicesCache.set(id, null!);
        this.invoiceFetchMap.delete(id);
        return of(null);
      })
    );

    this.invoiceFetchMap.set(id, fetch$);
    fetch$.subscribe();
    return null;
  }

  openEdit(){
    this.cdr.markForCheck();


    const payment= this.selectedPayment!;
    const ref = this.dialog.open(CreatePaymentModal, {
      width: '640px',
      maxWidth: '72vw',
      disableClose: true,   // ← requires explicit cancel/submit
      data: { mode: 'edit', payment }
    });
    this.cdr.markForCheck();

    ref.afterClosed().subscribe((payment)=>{
      if(payment) this.reload();
    });
  }

  statusClass(status: string): Record<string, boolean> {
    return {
      'badge--red':   status === 'CANCELLED',
      'badge--green': status === 'DONE',
    };
  }


  trackById(_: number, item: { id: string }) { return item.id; }

  cancel(){
    this.location.back();
  }
  ngOnDestroy(): void {
    this.themeObserver?.disconnect();
  }
}