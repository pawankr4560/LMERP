import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  if (!request.url.startsWith('/api/') || request.url.startsWith('/api/auth/')) return next(request);
  const token = auth.accessToken();
  if (!token) return next(request);

  const authorized = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  return next(authorized).pipe(catchError(error => {
    if (error.status !== 401) return throwError(() => error);
    return auth.refreshAccessToken().pipe(
      switchMap(session => next(request.clone({ setHeaders: { Authorization: `Bearer ${session.accessToken}` } })).pipe(
        catchError(retryError => {
          if (retryError.status === 401) auth.expireSession();
          return throwError(() => retryError);
        })
      )),
      catchError(refreshError => {
        auth.expireSession();
        return throwError(() => refreshError);
      })
    );
  }));
};
