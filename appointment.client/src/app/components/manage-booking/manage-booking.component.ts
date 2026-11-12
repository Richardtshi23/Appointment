import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AppointmentService } from '../../services/appointment.service';
import { AppointmentResultDto } from '../../models/appointment.model';

@Component({
  selector: 'app-manage-booking',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './manage-booking.component.html',
  styleUrls: ['../booking-wizard/booking-wizard.component.css']
})
export class ManageBookingComponent implements OnInit {
  lookupForm: FormGroup;
  appointment: AppointmentResultDto | null = null;
  loading = false;
  cancelling = false;
  confirmingCancel = false;
  errorMessage = '';
  private email = '';

  constructor(
    private fb: FormBuilder,
    private appointmentService: AppointmentService,
    private route: ActivatedRoute
  ) {
    this.lookupForm = this.fb.group({
      code: ['', Validators.required],
      email: ['', [Validators.required, Validators.email, Validators.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/)]]
    });
  }

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    if (code) {
      this.lookupForm.patchValue({ code });
    }
  }

  find(): void {
    if (this.lookupForm.invalid) {
      this.lookupForm.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    const { code, email } = this.lookupForm.value;
    this.email = email.trim();

    this.appointmentService.findAppointment(code.trim(), this.email).subscribe({
      next: (result) => {
        this.appointment = result;
        this.loading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.message || 'Could not look up the appointment.';
        this.loading = false;
      }
    });
  }

  askCancel(): void {
    this.confirmingCancel = true;
    this.errorMessage = '';
  }

  dismissCancel(): void {
    this.confirmingCancel = false;
  }

  confirmCancel(): void {
    if (!this.appointment) return;

    this.cancelling = true;
    this.errorMessage = '';

    this.appointmentService.cancelAppointment(this.appointment.confirmationCode, this.email).subscribe({
      next: (result) => {
        this.appointment = result;
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

  back(): void {
    this.appointment = null;
    this.confirmingCancel = false;
    this.errorMessage = '';
  }
}
