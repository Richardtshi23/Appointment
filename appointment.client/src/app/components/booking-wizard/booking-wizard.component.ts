import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AppointmentService } from '../../services/appointment.service';
import { Branch, ServiceItem, AppointmentResultDto, CreateAppointmentDto } from '../../models/appointment.model';

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
  readonly today = new Date().toISOString().substring(0, 10);

  constructor(private fb: FormBuilder, private appointmentService: AppointmentService) {
    this.bookingForm = this.fb.group({
      branchId: ['', Validators.required],
      serviceId: ['', Validators.required],
      date: ['', Validators.required],
      selectedSlot: ['', Validators.required],
      customerName: ['', [Validators.required, Validators.maxLength(100)]],
      customerEmail: ['', [Validators.required, Validators.email]],
      customerPhone: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    this.loadBranches();

    this.bookingForm.get('branchId')?.valueChanges.subscribe(branchId => {
      this.services = [];
      this.bookingForm.patchValue({ serviceId: '' }, { emitEvent: false });
      this.resetSlots();
      if (branchId) {
        this.loadServices(branchId);
      }
    });

    this.bookingForm.get('serviceId')?.valueChanges.subscribe(() => this.fetchSlots());
    this.bookingForm.get('date')?.valueChanges.subscribe(() => this.fetchSlots());
  }

  loadBranches(): void {
    this.appointmentService.getBranches().subscribe({
      next: (data) => this.branches = data,
      error: () => this.errorMessage = 'Failed to load branches.'
    });
  }

  loadServices(branchId: string): void {
    this.appointmentService.getServicesForBranch(branchId).subscribe({
      next: (data) => this.services = data,
      error: () => this.errorMessage = 'Failed to load services.'
    });
  }

  fetchSlots(): void {
    this.resetSlots();
    const { branchId, serviceId, date } = this.bookingForm.value;
    if (branchId && serviceId && date) {
      this.appointmentService.getAvailableSlots(branchId, serviceId, date).subscribe({
        next: (slots) => this.availableSlots = slots,
        error: () => this.errorMessage = 'Failed to fetch available time slots.'
      });
    }
  }

  // Slots may be "HH:mm" or full ISO strings depending on the backend
  slotLabel(slot: string): string {
    if (/^\d{2}:\d{2}/.test(slot)) return slot.substring(0, 5);
    const d = new Date(slot);
    return isNaN(d.getTime()) ? slot : d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  selectSlot(slot: string): void {
    this.bookingForm.patchValue({ selectedSlot: slot });
  }

  onSubmit(): void {
    if (this.bookingForm.invalid) return;

    this.loading = true;
    this.errorMessage = '';

    const v = this.bookingForm.value;
    const startTime = /^\d{2}:\d{2}/.test(v.selectedSlot)
      ? `${v.date}T${v.selectedSlot.substring(0, 5)}:00Z`
      : v.selectedSlot;

    const dto: CreateAppointmentDto = {
      branchId: v.branchId,
      serviceId: v.serviceId,
      customerName: v.customerName,
      customerEmail: v.customerEmail,
      customerPhone: v.customerPhone,
      startTime
    };

    this.appointmentService.bookAppointment(dto).subscribe({
      next: (result) => {
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

  reset(): void {
    this.confirmationResult = null;
    this.errorMessage = '';
    this.services = [];
    this.availableSlots = [];
    this.bookingForm.reset({ branchId: '', serviceId: '', date: '', selectedSlot: '', customerName: '', customerEmail: '', customerPhone: '' });
  }
}
