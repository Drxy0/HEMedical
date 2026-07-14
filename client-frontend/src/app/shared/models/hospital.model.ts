export type HospitalStatus = 'Pending' | 'Approved' | 'Blocked';

/** Mirror of the backend HospitalAdminView returned by /api/admin/hospitals. */
export interface HospitalAdminView {
  name: string;
  baseUrl: string;
  status: HospitalStatus;
  requestedUtc: string;
  lastSeenUtc: string;
  isActive: boolean;
}
