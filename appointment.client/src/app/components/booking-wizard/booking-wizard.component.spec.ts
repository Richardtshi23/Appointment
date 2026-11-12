import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { BookingWizardComponent } from './booking-wizard.component';
import { AppointmentService } from '../../services/appointment.service';

describe('BookingWizardComponent', () => {
  let fixture: ComponentFixture<BookingWizardComponent>;
  let component: BookingWizardComponent;
  let api: jasmine.SpyObj<AppointmentService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj('AppointmentService', ['getBranches', 'getServicesForBranch', 'getAvailableSlots', 'bookAppointment']);
    api.getBranches.and.returnValue(of([{ id: 'b1', name: 'Branch', address: 'Addr', phoneNumber: '1' }]));
    api.getServicesForBranch.and.returnValue(of([{ id: 's1', name: 'Svc', description: '', durationMinutes: 30 }]));
    api.getAvailableSlots.and.returnValue(of(['09:00', '09:30']));

    await TestBed.configureTestingModule({
      imports: [BookingWizardComponent],
      providers: [{ provide: AppointmentService, useValue: api }]
    }).compileComponents();

    fixture = TestBed.createComponent(BookingWizardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads branches on init', () => {
    expect(component.branches.length).toBe(1);
  });

  it('loads services when a branch is chosen', () => {
    component.bookingForm.patchValue({ branchId: 'b1' });
    expect(api.getServicesForBranch).toHaveBeenCalledWith('b1');
    expect(component.services.length).toBe(1);
  });

  it('fetches slots once branch, service and date are set', () => {
    component.bookingForm.patchValue({ branchId: 'b1' });
    component.bookingForm.patchValue({ serviceId: 's1' });
    component.bookingForm.patchValue({ date: '2030-01-02' });
    expect(api.getAvailableSlots).toHaveBeenCalledWith('b1', 's1', '2030-01-02');
    expect(component.availableSlots).toEqual(['09:00', '09:30']);
  });

  it('submits a booking with a combined start time', () => {
    api.bookAppointment.and.returnValue(of({ id: '1', confirmationCode: 'APT-1', startTime: '', endTime: '', status: 'Confirmed' }));
    component.bookingForm.patchValue({
      branchId: 'b1', serviceId: 's1', date: '2030-01-02', selectedSlot: '09:30',
      customerName: 'Jane', customerEmail: 'jane@example.com', customerPhone: '0821234567'
    });
    component.onSubmit();
    expect(api.bookAppointment.calls.mostRecent().args[0].startTime).toBe('2030-01-02T09:30:00Z');
    expect(component.confirmationResult?.confirmationCode).toBe('APT-1');
  });

  it('shows the server message on failure', () => {
    api.bookAppointment.and.returnValue(throwError(() => ({ error: { message: 'Slot taken' } })));
    component.bookingForm.patchValue({
      branchId: 'b1', serviceId: 's1', date: '2030-01-02', selectedSlot: '09:30',
      customerName: 'Jane', customerEmail: 'jane@example.com', customerPhone: '0821234567'
    });
    component.onSubmit();
    expect(component.errorMessage).toBe('Slot taken');
    expect(component.loading).toBeFalse();
  });
});
