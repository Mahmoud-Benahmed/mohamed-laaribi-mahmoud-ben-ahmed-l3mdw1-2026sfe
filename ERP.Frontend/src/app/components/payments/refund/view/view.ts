import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { forkJoin, catchError, of, Observable } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  PaymentService,
  RefundRequestDto,
  RefundLineDto,
  PaymentDto,
} from './../../../../services/payment.service';
import { ClientResponseDto, ClientsService } from './../../../../services/clients/clients.service';
import { AuthService, PRIVILEGES } from './../../../../services/auth/auth.service';
import { InvoiceDto, InvoiceService } from './../../../../services/invoice.service';

@Component({
  selector: 'app-refund-view',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatIconModule,
    MatTooltipModule,
    MatButtonModule,
    TranslatePipe,
  ],
  templateUrl: './view.html',
  styleUrl: './view.scss',
})
export class RefundViewComponent implements OnInit {
  private cdr       = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private location  = inject(Location);

  readonly PRIVILEGES = PRIVILEGES;

  // ── Alerts ────────────────────────────────────────────────────────────────
  errors: string[] = [];
  successMessage: string | null = null;

  // ── State ─────────────────────────────────────────────────────────────────
  refund:   RefundRequestDto | null = null;
  client:   ClientResponseDto | null = null;
  invoice:  InvoiceDto | null = null;
  payments: Map<string, PaymentDto> = new Map(); // paymentId → PaymentDto

  refundIdFromRoute: string | null = null;

  // ── Complete inline modal ─────────────────────────────────────────────────
  showCompleteModal = false;
  externalReference = '';
  isSubmitting      = false;

  constructor(
    public  authService:    AuthService,
    private paymentService: PaymentService,
    private clientService:  ClientsService,
    private invoiceService: InvoiceService,
    private route:          ActivatedRoute,
    private translate:      TranslateService,
  ) {}

  ngOnInit(): void {
    this.refundIdFromRoute = this.route.snapshot.paramMap.get('id');
    if (!this.refundIdFromRoute) { this.cancel(); return; }
    this.reload();
  }

  reload(): void {
    if (!this.refundIdFromRoute) return;

    this.paymentService.getRefundById(this.refundIdFromRoute)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (refund) => {
          this.refund = refund;
          this.loadRelated(refund);
        },
        error: () => {
          this.flash('error', this.translate.instant('payments.refunds.errors.load_failed'));
          this.cancel();
        },
      });
  }

  private loadRelated(refund: RefundRequestDto): void {
    const paymentIds = [...new Set(refund.lines.map(l => l.paymentId))];

    forkJoin({
      client:  this.clientService.getById(refund.clientId).pipe(catchError(() => of(null))),
      invoice: this.invoiceService.getById(refund.invoiceId).pipe(catchError(() => of(null))),
      payments: paymentIds.length
        ? forkJoin(paymentIds.map(id => this.paymentService.getPaymentById(id).pipe(catchError(() => of(null)))))
        : of([]),
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ client, invoice, payments }) => {
          this.client  = client;
          this.invoice = invoice;

          this.payments = new Map();
          (payments as (PaymentDto | null)[]).forEach(p => {
            if (p) this.payments.set(p.id, p);
          });

          this.cdr.markForCheck();
        },
        error: () => {
          this.flash('error', this.translate.instant('payments.refunds.errors.load_failed'));
          this.cdr.markForCheck();
        },
      });
  }
  
  // ── Helpers ───────────────────────────────────────────────────────────────

  getPaymentForLine(line: RefundLineDto): PaymentDto | null {
    return this.payments.get(line.paymentId) ?? null;
  }

  getTotalRefundAmount(): number {
    return Math.round(
      (this.refund?.lines ?? []).reduce((sum, l) => sum + l.amount, 0) * 100
    ) / 100;
  }

  statusClass(status: string): Record<string, boolean> {
    return {
      'badge--amber': status === 'PENDING',
      'badge--green': status === 'COMPLETED',
    };
  }

  trackById(_: number, item: { paymentId: string }) { return item.paymentId; }

  cancel(): void { this.location.back(); }

  // ── Alerts ────────────────────────────────────────────────────────────────

  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errors = [msg];
      setTimeout(() => { this.errors = []; }, 4000);
    }
  }

  dismissError(): void { this.errors = []; }
}