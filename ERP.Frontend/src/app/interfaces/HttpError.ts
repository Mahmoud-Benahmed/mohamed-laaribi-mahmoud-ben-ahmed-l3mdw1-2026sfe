export interface HttpError {
  code: string;
  message: string;
  statusCode: number;
  errors?: Record<string, string[]>; // ← add this
}
