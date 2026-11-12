import { Routes } from '@angular/router';
import { BookingWizardComponent } from './components/booking-wizard/booking-wizard.component';
import { ManageBookingComponent } from './components/manage-booking/manage-booking.component';

export const routes: Routes = [
  { path: '', component: BookingWizardComponent },
  { path: 'manage', component: ManageBookingComponent },
  { path: '**', redirectTo: '' }
];
