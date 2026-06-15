export interface HttpError {
  code: string;
  message: string;
  statusCode: number;
  errors?: Record<string, string[]>; // validation errors
}

export function isHttpError(obj: unknown): obj is HttpError {
  return (
    obj !== null &&
    typeof obj === 'object' &&
    'code' in obj &&
    typeof (obj as any).code === 'string'
  );
}