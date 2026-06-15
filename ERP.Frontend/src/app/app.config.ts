import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { HTTP_INTERCEPTORS, HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { AuthInterceptor } from './interceptor/auth-interceptor';
import { LoadingInterceptor } from './interceptor/loading-interceptor';
import { registerLocaleData } from '@angular/common';
import localeFrMA from '@angular/common/locales/fr-MA';
import localeFrTN from '@angular/common/locales/fr-TN';
import localeEnUS from '@angular/common/locales/en';
import localeFrFR from '@angular/common/locales/fr';
import localeEnGB from '@angular/common/locales/en-GB';
import { provideTranslateService, TranslateLoader } from '@ngx-translate/core';
import { Observable } from 'rxjs';
import { TenantInactiveInterceptor } from './interceptor/tenant-inactive-interceptor';
import { errorTranslateInterceptor } from './interceptor/error-Translate.interceptor';
import { apiInterceptor } from './interceptor/api-interceptor.interceptor';

registerLocaleData(localeFrMA);
registerLocaleData(localeFrTN);
registerLocaleData(localeEnUS);
registerLocaleData(localeFrFR);
registerLocaleData(localeEnGB);

// ✅ inline loader — no external package needed
class HttpTranslateLoader implements TranslateLoader {
  constructor(private http: HttpClient) {}
  getTranslation(lang: string): Observable<any> {
    return this.http.get(`assets/i18n/${lang}.json`);
  }
}

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
