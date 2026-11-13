import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, map, merge, of, switchMap, tap } from 'rxjs';
import { AppointmentService } from '../../services/appointment.service';
import { SA_OFFSET, SA_OFFSET_PIPE, todayInSouthAfrica } from '../../sa-time';
import { Branch, ServiceItem, AppointmentResultDto, CreateAppointmentDto } from '../../models/appointment.model';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
const SA_PHONE_PATTERN = /^0[1-8][0-9]{8}$/;

@Component({
  selector: 'app-booking-wizard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './booking-wizard.component.html',
  styleUrls: ['./booking-wizard.component.css']
})
export class BookingWizardComponent implements OnInit {
  bookingForm: FormGroup;
  branches: Branch[] = [];
  services: ServiceItem[] = [];
  availableSlots: string[] = [];
  confirmationResult: AppointmentResultDto | null = null;
  loading = false;
  errorMessage = '';
  confirmingCancel = false;
  cancelling = false;
  readonly today = todayInSouthAfrica();
  readonly offset = SA_OFFSET_PIPE;

  private bookedEmail = '';
  private destroyRef = inject(DestroyRef);

  constructor(private fb: FormBuilder, private appointmentService: AppointmentService) {
    this.bookingForm = this.fb.group({
      branchId: ['', Validators.required],
      serviceId: ['', Validators.required],
      date: ['', Validators.required],
      selectedSlot: ['', Validators.required],
      customerName: ['', [Validators.required, Validators.maxLength(100)]],
      customerEmail: ['', [Validators.required, Validators.email, Validators.pattern(EMAIL_PATTERN)]],
      customerPhone: ['', [Validators.required, Validators.pattern(SA_PHONE_PATTERN)]]
    });
  }

  ngOnInit(): void {
    this.appointmentService.getBranches().subscribe({
      next: (data) => this.branches = data,
      error: () => this.errorMessage = 'Failed to load branches.'
    });

    const branchId = this.bookingForm.get('branchId')!;
    const serviceId = this.bookingForm.get('serviceId')!;
    const date = this.bookingForm.get('date')!;

    // switchMap drops the previous request, so a slow response can't overwrite a newer one
    branchId.valueChanges.pipe(
      tap(() => {
        this.services = [];
        this.bookingForm.patchValue({ serviceId: '' }, { emitEvent: false });
      }),
      switchMap(id => id
        ? this.appointmentService.getServicesForBranch(id).pipe(
            catchError(() => {
              this.errorMessage = 'Failed to load services.';
              return of([] as ServiceItem[]);
            }))
        : of([] as ServiceItem[])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(services => this.services = services);

    merge(branchId.valueChanges, serviceId.valueChanges, date.valueChanges).pipe(
      tap(() => this.resetSlots()),
      map(() => this.bookingForm.value),
      switchMap(v => v.branchId && v.serviceId && v.date
        ? this.appointmentService.getAvailableSlots(v.branchId, v.serviceId, v.date).pipe(
            catchError(() => {
              this.errorMessage = 'Failed to fetch available time slots.';
              return of([] as string[]);
            }))
        : of([] as string[])),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(slots => this.availableSlots = slots);
  }

  selectSlot(slot: string): void {
    this.bookingForm.patchValue({ selectedSlot: slot });
  }

  onSubmit(): void {
    if (this.bookingForm.invalid) return;

    this.loading = true;
    this.errorMessage = '';

    const v = this.bookingForm.value;
    const dto: CreateAppointmentDto = {
      branchId: v.branchId,
      serviceId: v.serviceId,
      customerName: v.customerName,
      customerEmail: v.customerEmail,
      customerPhone: v.customerPhone,
      // slots are South African time (SAST, no daylight saving)
      startTime: `${v.date}T${v.selectedSlot}:00${SA_OFFSET}`
    };

    this.appointmentService.bookAppointment(dto).subscribe({
      next: (result) => {
        this.bookedEmail = dto.customerEmail;
        this.confirmationResult = result;
        this.loading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.message || 'An error occurred while booking.';
        this.loading = false;
      }
    });
  }

  private resetSlots(): void {
    this.availableSlots = [];
    this.bookingForm.patchValue({ selectedSlot: '' }, { emitEvent: false });
  }

  askCancel(): void {
    this.confirmingCancel = true;
    this.errorMessage = '';
  }

  dismissCancel(): void {
    this.confirmingCancel = false;
  }

  confirmCancel(): void {
    if (!this.confirmationResult) return;
    this.cancelling = true;
    this.errorMessage = '';

    this.appointmentService.cancelAppointment(this.confirmationResult.confirmationCode, this.bookedEmail).subscribe({
      next: (result) => {
        this.confirmationResult = result;
        this.confirmingCancel = false;
        this.cancelling = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.message || 'An error occurred while cancelling.';
        this.confirmingCancel = false;
        this.cancelling = false;
      }
    });
  }

  reset(): void {
    this.confirmationResult = null;
    this.confirmingCancel = false;
    this.cancelling = false;
    this.errorMessage = '';
    this.bookedEmail = '';
    this.services = [];
    this.availableSlots = [];
    this.bookingForm.reset({ branchId: '', serviceId: '', date: '', selectedSlot: '', customerName: '', customerEmail: '', customerPhone: '' });
  }
}
