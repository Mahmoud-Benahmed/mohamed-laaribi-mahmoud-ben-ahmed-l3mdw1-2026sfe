import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { TenantService } from '../../../services/tenant.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { UserSettingsService } from '../../../services/user-settings.service';

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, TranslateModule],
  templateUrl: './onboarding.html',
  styleUrl: './onboarding.scss'
})
export class OnboardingComponent implements OnInit {
  form!: FormGroup;
  planId   = '';
  planName = '';
  loading  = false;
  error    = '';
  success  = false;
  timezones = [
    'Africa/Tunis', 'Africa/Cairo', 'Europe/Paris',
    'Europe/London', 'America/New_York', 'Asia/Dubai'
  ];
  currencies = ['TND', 'EUR', 'USD', 'GBP', 'MAD', 'DZD'];
  locales    = ['fr-TN', 'en-US', 'fr-FR', 'ar-TN'];
  constructor(
    private fb: FormBuilder,
    private tenantService: TenantService,
    private router: Router,
    private route: ActivatedRoute,
    private translate: TranslateService,
    public  userSettings: UserSettingsService
  ) {}
  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      this.planId   = params['planId']   ?? '';
      this.planName = params['planName'] ?? '';
    });
    this.form = this.fb.group({
      //Company
      name:           ['', [Validators.required, Validators.maxLength(200)]],
      email:          ['', [Validators.required, Validators.email, Validators.maxLength(200)]],
      phone:          ['', [Validators.required, Validators.maxLength(25), Validators.pattern(/^\+?[\d\s\-().]{6,25}$/)]],
      subdomainSlug:  ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/^[a-zA-Z0-9-]+$/)]],
      logoUrl:        ['', [Validators.maxLength(500), Validators.pattern(/^https?:\/\/.+\..+/)]],
      //Branding
      primaryColor:   ['', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
      secondaryColor: ['', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
      //Regional
      currency:       ['TND',           Validators.required],
      locale:         ['fr-TN',         Validators.required],
      timezone:       ['Africa/Tunis',  Validators.required],
    });
  }
  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.error   = '';
    const payload = { ...this.form.value, planId: this.planId };
    this.tenantService.createTenant(payload).subscribe({
      next: () => {
        this.loading = false;
        this.success = true;
        setTimeout(() => this.router.navigate(['/login']), 2500);
      },
      error: (err) => {
        this.loading = false;
        this.error = err?.error?.message ?? this.translate.instant('ERRORS.UNKNOWN');
      }
    });
  }
}