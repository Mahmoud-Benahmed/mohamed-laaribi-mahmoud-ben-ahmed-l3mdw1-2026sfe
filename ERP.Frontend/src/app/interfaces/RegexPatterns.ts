export const RegexPatterns = {
safeText    : /^[\p{L}0-9\s,.'\\-]+$/u,
phone       : /^\+?\d{8,15}$/,
alphaNumeric: /^[A-Za-z0-9]+$/,
categoryCode : /^[A-Za-z0-9_\-]+$/,
hexColor     : /^#[0-9a-fA-F]{6}$/,
subdomainSlug: /^[a-zA-Z0-9-]+$/,
alpha        : /^[A-Za-z\s]+$/,
login: /^[a-z0-9_]+$/,
barCode: /^\d{8,13}$/
}