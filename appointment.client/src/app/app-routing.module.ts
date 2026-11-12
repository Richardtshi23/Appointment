import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { BookingWizardComponent } from './components/booking-wizard/booking-wizard.component';
import { ManageBookingComponent } from './components/manage-booking/manage-booking.component';

const routes: Routes = [
  { path: '', component: BookingWizardComponent },
  { path: 'manage', component: ManageBookingComponent },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
