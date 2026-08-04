import { HttpInterceptorFn } from '@angular/common/http';

/** Attaches JWT bearer token to outgoing API requests. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('smi_token');
  if (!token) return next(req);

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    })
  );
};
