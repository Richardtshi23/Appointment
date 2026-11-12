import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AppointmentService } from './appointment.service';
import { CreateAppointmentDto } from '../models/appointment.model';

describe('AppointmentService', () => {
  let service: AppointmentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(AppointmentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets branches', () => {
    service.getBranches().subscribe(b => expect(b.length).toBe(1));
    http.expectOne('/api/branches').flush([{ id: '1', name: 'A', address: '', phoneNumber: '' }]);
  });

  it('gets services for a branch', () => {
    service.getServicesForBranch('b1').subscribe();
    expect(http.expectOne('/api/branches/b1/services').request.method).toBe('GET');
  });

  it('passes slot query parameters', () => {
    service.getAvailableSlots('b1', 's1', '2030-01-02').subscribe();
    const req = http.expectOne(r => r.url === '/api/appointments/available-slots');
    expect(req.request.params.get('branchId')).toBe('b1');
    expect(req.request.params.get('serviceId')).toBe('s1');
    expect(req.request.params.get('date')).toBe('2030-01-02');
  });

  it('posts a booking', () => {
    const dto: CreateAppointmentDto = {
      branchId: 'b1', serviceId: 's1', customerName: 'Jane', customerEmail: 'j@x.com',
      customerPhone: '0821234567', startTime: '2030-01-02T10:00:00Z'
    };
    service.bookAppointment(dto).subscribe();
    const req = http.expectOne('/api/appointments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(dto);
  });

  it('posts the code and email in the body when looking up or cancelling', () => {
    service.findAppointment('APT-1', 'a@b.com').subscribe();
    const lookup = http.expectOne('/api/appointments/lookup');
    expect(lookup.request.method).toBe('POST');
    expect(lookup.request.body).toEqual({ confirmationCode: 'APT-1', email: 'a@b.com' });

    service.cancelAppointment('APT-1', 'a@b.com').subscribe();
    const cancel = http.expectOne('/api/appointments/cancel');
    expect(cancel.request.body).toEqual({ confirmationCode: 'APT-1', email: 'a@b.com' });
  });
});
