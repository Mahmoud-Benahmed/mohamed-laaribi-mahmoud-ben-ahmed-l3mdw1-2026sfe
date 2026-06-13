import { ChangeDetectorRef, Component, DestroyRef, Inject, inject, OnInit } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MatIcon, MatIconModule } from "@angular/material/icon";
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatButtonModule } from '@angular/material/button';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule, Location } from '@angular/common';
import { CreatePaymentDto, InvoiceCacheDto, PaymentDto, PaymentService, RefundRequestDto } from '../../../../services/payment.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-create-payment-modal',
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatDialogModule,
    TranslatePipe
  ],
  templateUrl: './complete-modal.html',
  styleUrl: './complete-modal.scss',
})
export class CompleteRefundModal implements OnInit{
  private dialogRef = inject(MatDialogRef<CompleteRefundModal>);
  private readonly destroyRef = inject(DestroyRef);
  private cdr = inject(ChangeDetectorRef);
  private translate = inject(TranslateService);
  private router = inject(Location);
  private paymentService= inject(PaymentService);
  data = inject<{ refund: RefundRequestDto }>(MAT_DIALOG_DATA);

  completingRefund: RefundRequestDto | null = null;

  form!: FormGroup;

  successMessage: string | null = null;
  errorMessage:   string | null = null;

  isSubmitting = false;

  constructor(private fb: FormBuilder){}


  ngOnInit(): void {
    this.completingRefund = this.data.refund;
    this.initForm();
  }


  initForm(): void {
    this.form = this.fb.group({
      refundReason: [null, Validators.required],
    });
  }


  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (!this.completingRefund) {
      this.flash('error', this.translate.instant('payments.refunds.errors.complete_failed'));
      return;
    }

    this.isSubmitting = true;

    this.paymentService.completeRefund(this.completingRefund.id, {
      externalReference: this.form.get('refundReason')?.value?.trim(),
    }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.flash('success', this.translate.instant('payments.refunds.success.completed'));
        setTimeout(() => this.dialogRef.close(true), 1500);
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err?.error?.message ?? this.translate.instant('payments.refunds.errors.complete_failed');
        this.flash('error', msg);
      },
    });
  }


  flash(type: 'success' | 'error', msg: string): void {
    if (type === 'success') {
      this.successMessage = msg;
      setTimeout(() => {
        document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 0);
      setTimeout(() => { this.successMessage = null; }, 3000);
    } else {
      this.errorMessage = msg;
      setTimeout(() => {
        document.getElementById('top')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 0);
      setTimeout(() => { this.errorMessage = null; }, 4000);
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}