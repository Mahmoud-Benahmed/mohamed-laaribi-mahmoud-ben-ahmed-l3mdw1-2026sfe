export const RegexPatterns = {
safeText    : /^[\p{L}0-9\s,.'\\-]+$/u,
phone       : /^\+?[\d][\d\s]{6,18}[\d]$/,
email: /^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$/,
alphaNumeric: /^[\p{L}0-9\s]+$/u,
categoryCode : /^[\p{L}0-9_\-]+$/u,
hexColor     : /^#[0-9a-fA-F]{6}$/,
subdomainSlug: /^[a-z0-9]([a-z0-9-]*[a-z0-9])?$/,
alpha: /^[\p{L}\s]+$/u,
login: /^[a-z0-9_-]+$/,
barCode: /^\d{8,13}$/,
integer: /^-?\d+$/,
decimal: /^-?\d+(\.\d+)?$/
}