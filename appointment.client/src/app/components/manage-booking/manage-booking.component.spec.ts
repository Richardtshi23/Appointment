import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ManageBookingComponent } from './manage-booking.component';
import { AppointmentService } from '../../services/appointment.service';
import { AppointmentResultDto } from '../../models/appointment.model';

describe('ManageBookingComponent', () => {
  let fixture: ComponentFixture<ManageBookingComponent>;
  let component: ManageBookingComponent;
  let api: jasmine.SpyObj<AppointmentService>;

  const confirmed: AppointmentResultDto = {
    id: '1', confirmationCode: 'APT-1', startTime: '2030-01-02T10:00:00Z', endTime: '2030-01-02T10:30:00Z', status: 'Confirmed'
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj('AppointmentService', ['findAppointment', 'cancelAppointment']);

    await TestBed.configureTestingModule({
      imports: [ManageBookingComponent],
      providers: [
        { provide: AppointmentService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ code: 'APT-1' }) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ManageBookingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('prefills the code from the link', () => {
    expect(component.lookupForm.value.code).toBe('APT-1');
  });

  it('does not call the API when the email is badly formatted', () => {
    component.lookupForm.patchValue({ email: 'not-an-email' });
    component.find();
    expect(api.findAppointment).not.toHaveBeenCalled();
  });

  it('finds the appointment and cancels it with the same code and email', () => {
    api.findAppointment.and.returnValue(of(confirmed));
    api.cancelAppointment.and.returnValue(of({ ...confirmed, status: 'Cancelled' }));
    component.lookupForm.patchValue({ email: 'jane@example.com' });

    component.find();
    expect(component.appointment?.status).toBe('Confirmed');

    component.confirmCancel();
    expect(api.cancelAppointment).toHaveBeenCalledWith('APT-1', 'jane@example.com');
    expect(component.appointment?.status).toBe('Cancelled');
  });

  it('shows the server message when nothing is found', () => {
    api.findAppointment.and.returnValue(throwError(() => ({ error: { message: 'No appointment found.' } })));
    component.lookupForm.patchValue({ email: 'jane@example.com' });
    component.find();
    expect(component.errorMessage).toBe('No appointment found.');
    expect(component.appointment).toBeNull();
  });
});
