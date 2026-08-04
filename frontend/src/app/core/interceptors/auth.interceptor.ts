import { HttpInterceptorFn } from '@angular/common/http';

/** Attaches the JWT from localStorage to every API request. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('smh_token');
  if (!token) return next(req);

  return next(req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  }));
};
