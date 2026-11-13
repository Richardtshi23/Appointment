import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Branch, ServiceItem, CreateAppointmentDto, AppointmentResultDto } from '../models/appointment.model';

@Injectable({
  providedIn: 'root'
})
export class AppointmentService {
  // Relative URL: forwarded to the backend by src/proxy.conf.js during `ng serve`
  private apiUrl = '/api';

  constructor(private http: HttpClient) {}

  getBranches(): Observable<Branch[]> {
    return this.http.get<Branch[]>(`${this.apiUrl}/branches`);
  }

  getServicesForBranch(branchId: string): Observable<ServiceItem[]> {
    return this.http.get<ServiceItem[]>(`${this.apiUrl}/branches/${branchId}/services`);
  }

  getAvailableSlots(branchId: string, serviceId: string, date: string): Observable<string[]> {
    const params = new HttpParams()
      .set('branchId', branchId)
      .set('serviceId', serviceId)
      .set('date', date);

    return this.http.get<string[]>(`${this.apiUrl}/appointments/available-slots`, { params });
  }

  bookAppointment(dto: CreateAppointmentDto): Observable<AppointmentResultDto> {
    return this.http.post<AppointmentResultDto>(`${this.apiUrl}/appointments`, dto);
  }

  findAppointment(confirmationCode: string, email: string): Observable<AppointmentResultDto> {
    return this.http.post<AppointmentResultDto>(`${this.apiUrl}/appointments/lookup`, { confirmationCode, email });
  }

  cancelAppointment(confirmationCode: string, email: string): Observable<AppointmentResultDto> {
    return this.http.post<AppointmentResultDto>(`${this.apiUrl}/appointments/cancel`, { confirmationCode, email });
  }
}
