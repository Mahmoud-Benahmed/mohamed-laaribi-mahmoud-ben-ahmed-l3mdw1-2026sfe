// password.service.ts
import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import {
  generatePassword,
  checkPassword,
  DEFAULT_ENGLISH_MESSAGES,
  DEFAULT_FRENCH_MESSAGES,
  PasswordValidationResult
} from '../util/PasswordUtil';

@Injectable({ providedIn: 'root' })
export class PasswordService {
  private translate = inject(TranslateService);

  /**
   * Validates a password with translated error messages
   */
  validatePassword(
    password: string,
    currentPassword: string | null = null
  ): PasswordValidationResult {
    const messages = this.translate.currentLang === 'fr'
      ? DEFAULT_FRENCH_MESSAGES
      : DEFAULT_ENGLISH_MESSAGES;

    return checkPassword(password, currentPassword, undefined, messages);
  }

  /**
   * Gets the translated strength label for a password strength value
   */
  getStrengthLabel(strength: string): string {
    const strengthMap: Record<string, string> = {
      'weak':       'weak',
      'fair':       'fair',
      'strong':     'good',
      'very strong':'strong',
    };
    return strengthMap[strength] || 'weak'; // return the key, don't translate here
  }

  /**
   * Generates a secure random password
   */
  generatePassword(): string {
    return generatePassword();
  }

  /**
   * Gets the CSS class for password strength meter
   */
  getStrengthClass(strength: string): string {
    const classMap: Record<string, string> = {
      'weak': 'strength--weak',
      'fair': 'strength--fair',
      'strong': 'strength--strong',
      'very strong': 'strength--very-strong',
    };
    return classMap[strength] || '';
  }

  /**
   * Gets the score (1-4) for password strength bars
   */
  getStrengthScore(strength: string): number {
    const scoreMap: Record<string, number> = {
      'weak': 1,
      'fair': 2,
      'strong': 3,
      'very strong': 4,
    };
    return scoreMap[strength] || 0;
  }
}
