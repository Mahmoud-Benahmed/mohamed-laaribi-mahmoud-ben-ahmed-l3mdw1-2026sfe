// ─────────────────────────────────────────────────────────────────────────────
// NIST SP 800-63B + OWASP compliant password utilities
//
// Policy decisions:
//  - Min length: 8 (NIST floor); 15+ scores as "strong"
//  - Max length: 128 (NIST: "SHALL NOT impose other composition rules")
//  - NO mandatory uppercase/number/symbol requirements (NIST §5.1.1.2)
//  - Spaces/unicode permitted — supports passphrases (NIST §5.1.1.2)
//  - Common/breached password screening (NIST §5.1.1.2 — mandatory)
//  - Same-as-current check: NIST §5.1.1.2 recommends rejecting reuse
//  - "invalid" removed from strength — validity and strength are orthogonal
//  - generatePassword uses crypto.getRandomValues — never Math.random()
//  - Strength score weights length over character variety
// ─────────────────────────────────────────────────────────────────────────────

// Extend with a real breached-password dataset in production.
// NIST §5.1.1.2: "commonly-used, expected, or compromised" passwords MUST be rejected.
// Production: replace Set lookup with a k-anonymity HaveIBeenPwned API call
// or an offline bloom filter over the full 500M+ breached password corpus.


export interface PasswordValidationResult {
  isValid: boolean;
  strength: "weak" | "fair" | "strong" | "very strong";
  errors: string[];
  score: number;
}

export interface PasswordMessages {
  minLength: (min: number) => string;
  maxLength: (max: number) => string;
  mustDiffer: string;
  breached: string;
  repeatedChar: string;
}

// Default English messages
export const DEFAULT_ENGLISH_MESSAGES: PasswordMessages = {
  minLength: (min) => `Password must be at least ${min} characters.`,
  maxLength: (max) => `Password must be no more than ${max} characters.`,
  mustDiffer: "New password must differ from the current one.",
  breached: "This password appears in known data breaches and cannot be used. Choose a unique passphrase instead.",
  repeatedChar: "Password cannot consist of a single repeated character.",
};

// French messages
export const DEFAULT_FRENCH_MESSAGES: PasswordMessages = {
  minLength: (min) => `Le mot de passe doit contenir au moins ${min} caractères.`,
  maxLength: (max) => `Le mot de passe ne doit pas dépasser ${max} caractères.`,
  mustDiffer: "Le nouveau mot de passe doit être différent de l'actuel.",
  breached: "Ce mot de passe apparaît dans des fuites de données connues et ne peut pas être utilisé. Choisissez plutôt une phrase de passe unique.",
  repeatedChar: "Le mot de passe ne peut pas consister en un seul caractère répété.",
};

const COMMON_PASSWORDS = new Set([
  "password", "password1", "password123", "12345678", "123456789",
  "qwerty123", "iloveyou", "admin123", "letmein", "welcome1",
  "monkey123", "dragon123", "master123", "sunshine", "princess",
  "passw0rd", "p@ssword", "p@ssw0rd", "abc12345", "111111111",
]);

export interface PasswordRules {
  minLength?: number;              // NIST floor: 8; production recommendation: 12–15
  maxLength?: number;              // NIST: don't restrict below 64; 128 is standard
  checkCommonPasswords?: boolean;  // NIST §5.1.1.2: MUST screen breached passwords
}

export interface PasswordValidationResult {
  isValid: boolean;
  // "invalid" intentionally removed — validity lives in isValid, not strength.
  // Mixing them forces every consumer to check both fields defensively.
  // Gate meter rendering on password.length > 0 in the template instead.
  strength: "weak" | "fair" | "strong" | "very strong";
  errors: string[];
  score: number; // 0–10 informational only — not enforced as a rule
}

// NIST-aligned defaults — no mandatory composition rules
export const DEFAULT_RULES: Required<PasswordRules> = {
  minLength: 8,
  maxLength: 128,
  checkCommonPasswords: true,
};

/**
 * Validates a password against NIST SP 800-63B and OWASP guidelines.
 *
 * @param password        The candidate password to validate.
 * @param currentPassword Pass the user's current password to enforce the
 *                        "must differ from current" rule (NIST §5.1.1.2).
 *                        Pass `null` when no current password exists
 *                        (e.g. admin-set passwords, first-time setup).
 * @param rules           Optional overrides for min/max length and breach check.
 */
export function checkPassword(
  password: string,
  currentPassword: string | null = null,
  rules: PasswordRules = DEFAULT_RULES,
  messages: PasswordMessages = DEFAULT_ENGLISH_MESSAGES, // ← add messages parameter
): PasswordValidationResult {
  const { minLength, maxLength, checkCommonPasswords } = {
    ...DEFAULT_RULES,
    ...rules,
  };

  const errors: string[] = [];
  let score = 0;

  // ── 1. Same-as-current check (NIST §5.1.1.2) ─────────────────────────────
  // Only enforced when a current password is provided (i.e. self-service flow).
  // Admins resetting another user's password pass null — no reuse check needed.
  if (currentPassword !== null && password === currentPassword) {
    errors.push(messages.mustDiffer);
  }

  // ── 2. Length (NIST §5.1.1.1: minimum 8; no arbitrary short maximum) ──────
  // Length is the strongest single predictor of entropy — weight it heavily.
  if (password.length < minLength) {
    errors.push(messages.minLength(minLength));
  } else {
    score += 1;                             // ≥ minLength
    if (password.length >= 12) score += 2;  // OWASP "recommended" minimum
    if (password.length >= 15) score += 1;  // NIST memorized-secret sweet spot
    if (password.length >= 20) score += 1;  // Passphrase territory
  }

  if (password.length > maxLength) {
    errors.push(messages.maxLength(maxLength));
  }

  // ── 3. Breached / common password check (NIST §5.1.1.2 — MANDATORY) ───────
  if (checkCommonPasswords && COMMON_PASSWORDS.has(password.toLowerCase())) {
    errors.push(messages.breached);
  }

  // ── 4. Repetition / trivial pattern penalties ─────────────────────────────
  // NIST §5.1.1.2 explicitly permits rejecting "repetitive or sequential
  // characters" even while banning arbitrary composition rules.
  if (/^(.)\1+$/.test(password)) {
    errors.push(messages.repeatedChar);
    score = Math.max(0, score - 3);
  }

  // Penalise (but don't hard-reject) keyboard walks and numeric sequences
  const SEQUENTIAL_PATTERNS =
    /(?:012|123|234|345|456|567|678|789|890|abc|bcd|cde|def|efg|fgh|ghi|hij|ijk|jkl|klm|lmn|mno|nop|opq|pqr|qrs|rst|stu|tuv|uvw|vwx|wxy|xyz|qwerty|asdf|zxcv)/i;
  if (SEQUENTIAL_PATTERNS.test(password)) {
    score = Math.max(0, score - 1);
  }

  // ── 5. Character-variety bonus (informational only — NEVER enforced) ───────
  // NIST §5.1.1.2: "SHALL NOT impose other composition rules."
  // Variety is rewarded in the score so passphrases aren't unfairly penalised,
  // but nothing is rejected solely for lacking uppercase, numbers, or symbols.
  if (/[A-Z]/.test(password)) score += 0.5;
  if (/[a-z]/.test(password)) score += 0.5;
  if (/[0-9]/.test(password)) score += 0.5;
  if (/[^A-Za-z0-9]/.test(password)) score += 0.5; // any non-alphanumeric
  score = Math.round(score);

  // ── 6. Strength label ─────────────────────────────────────────────────────
  const strength: PasswordValidationResult["strength"] =
    score <= 2 ? "weak" :
    score <= 4 ? "fair" :
    score <= 6 ? "strong" :
    "very strong";

  return {
    isValid: errors.length === 0,
    strength,
    errors,
    score,
  };
}

// ── generatePassword ──────────────────────────────────────────────────────────
// Generates a cryptographically secure random password.
//
// OWASP: password generators MUST use a CSPRNG — Math.random() is NOT
// cryptographically secure and MUST NOT be used for security-sensitive values.
// Web/Node: crypto.getRandomValues (SubtleCrypto) is the standard CSPRNG.
//
// Default output: 16 characters (above NIST/OWASP recommended minimum of 12),
// guaranteeing coverage across all four character classes so the result scores
// "strong" or better out of the box.
export function generatePassword(minLength = 16, maxLength = 28): string {
  const length = cryptoRandomInt(minLength, maxLength);
  const chars = {
    upper:   "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
    lower:   "abcdefghijklmnopqrstuvwxyz",
    numbers: "0123456789",
    symbols: "!@#$%^&*()_+-=[]{}|;:,.<>?",
  };

  const allChars = chars.upper + chars.lower + chars.numbers + chars.symbols;

  // Guarantee at least one character from each class so the password
  // always carries full variety bonus in checkPassword scoring.
  const guaranteed = [
    cryptoRandomChoice(chars.upper),
    cryptoRandomChoice(chars.lower),
    cryptoRandomChoice(chars.numbers),
    cryptoRandomChoice(chars.symbols),
  ];

  const remaining = Array.from(
    { length: Math.max(0, length - guaranteed.length) },
    () => cryptoRandomChoice(allChars),
  );

  // Fisher-Yates shuffle with crypto.getRandomValues so guaranteed
  // characters don't cluster at a predictable position.
  return cryptoShuffle([...guaranteed, ...remaining]).join("");
}

// ── Crypto helpers ────────────────────────────────────────────────────────────

/** Returns a cryptographically random element from a string. */
function cryptoRandomChoice(chars: string): string {
  // Rejection sampling: discard values in the bias zone above the largest
  // multiple of chars.length that fits in a Uint32, ensuring uniform distribution.
  const max = Math.floor(0x1_0000_0000 / chars.length) * chars.length;
  const buf = new Uint32Array(1);
  let value: number;
  do {
    crypto.getRandomValues(buf);
    value = buf[0];
  } while (value >= max);
  return chars[value % chars.length];
}

/** Fisher-Yates shuffle using crypto.getRandomValues. */
function cryptoShuffle<T>(arr: T[]): T[] {
  const buf = new Uint32Array(arr.length);
  crypto.getRandomValues(buf);
  for (let i = arr.length - 1; i > 0; i--) {
    const j = buf[i] % (i + 1);
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

/** Returns a cryptographically random integer in [min, max] inclusive. */
function cryptoRandomInt(min: number, max: number): number {
  const range = max - min + 1;
  const max32 = Math.floor(0x1_0000_0000 / range) * range;
  const buf = new Uint32Array(1);
  let value: number;
  do {
    crypto.getRandomValues(buf);
    value = buf[0];
  } while (value >= max32);
  return min + (value % range);
}

export function generateUuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  // Fallback for non-secure contexts
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}