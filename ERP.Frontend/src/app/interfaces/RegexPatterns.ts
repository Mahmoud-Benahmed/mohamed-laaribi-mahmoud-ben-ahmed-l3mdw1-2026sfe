export const RegexPatterns = {
safeText    : /^[\p{L}0-9\s,.'\\-]+$/u,
phone       : /^\+?\d{8,15}$/,
alphaNumeric: /^[\p{L}0-9\s]+$/u,
categoryCode : /^[\p{L}0-9_\-]+$/u,
hexColor     : /^#[0-9a-fA-F]{6}$/,
subdomainSlug: /^[a-zA-Z0-9]{3,}$/,
alpha: /^[\p{L}\s]+$/u,
login: /^[a-z0-9_]+$/,
barCode: /^\d{8,13}$/,
integer: /^-?\d+$/,
decimal: /^-?\d+(\.\d+)?$/
}