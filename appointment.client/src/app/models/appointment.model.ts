export interface Branch {
  id: string;
  name: string;
  address: string;
  phoneNumber: string;
}

export interface ServiceItem {
  id: string;
  name: string;
  description: string;
  durationMinutes: number;
}

export interface CreateAppointmentDto {
  branchId: string;
  serviceId: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  startTime: string; // ISO string format
}

export interface AppointmentResultDto {
  id: string;
  confirmationCode: string;
  startTime: string;
  endTime: string;
  status: string;
}
