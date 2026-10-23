import { PartnershipStatus } from '../../core/partnerships/partnership-api';
import { StoreOrderStatus } from '../../core/orders/order-api';
import { LocalizedName } from '../../shared/i18n/localized';
import { Role } from '../../core/auth/auth.models';

export type UserStatus = 'Active' | 'Blocked';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface AdminUserListItem {
  id: string;
  email: string;
  fullName: string;
  phone: string;
  roles: Role[];
  status: UserStatus;
  affiliateCode: string | null;
  lastLoginAt: string | null;
  createdAt: string;
  /** The store this user owns, if any. */
  store: AdminUserStore | null;
}

export interface AdminUserStore {
  id: string;
  name: string;
  slug: string;
  status: string;
}

export interface AdminUser extends AdminUserListItem {
  blockedAt: string | null;
  blockedReason: string | null;
  /** Set when an admin deactivated the affiliate code (012). */
  affiliateCodeDisabledAt: string | null;
  affiliateCodeDisabledReason: string | null;
}

export interface UserCounts {
  total: number;
  active: number;
  blocked: number;
  buyers: number;
  affiliates: number;
  storeOwners: number;
  admins: number;
}

export interface UserListQuery {
  search?: string;
  role?: Role;
  status?: UserStatus;
  page?: number;
  pageSize?: number;
}

export interface AuditEntry {
  id: string;
  createdAt: string;
  actorId: string;
  actorEmail: string;
  action: string;
  targetType: string;
  targetId: string;
  targetLabel: string | null;
  reason: string | null;
  before: string | null;
  after: string | null;
}

export interface AuditQuery {
  targetType?: string;
  targetId?: string;
  actorId?: string;
  action?: string;
  page?: number;
  pageSize?: number;
}

export type RoleRequestStatus = 'Pending' | 'Approved' | 'Refused' | 'Cancelled';

export interface AdminRoleRequest {
  id: string;
  userId: string;
  userEmail: string;
  userFullName: string;
  /** Roles the user holds right now. */
  userRoles: Role[];
  role: Role;
  status: RoleRequestStatus;
  message: string | null;
  decisionReason: string | null;
  decidedAt: string | null;
  createdAt: string;
}

export interface RoleRequestQuery {
  status?: RoleRequestStatus;
  role?: Role;
  page?: number;
  pageSize?: number;
}

export type SettingType = 'Text' | 'WholeNumber' | 'Number';

/** A platform setting with its limits. Values are invariant strings ("80", "0.5"). */
export interface AdminSetting {
  key: string;
  group: string;
  type: SettingType;
  value: string;
  defaultValue: string;
  min: number | null;
  max: number | null;
  unit: string | null;
  editable: boolean;
  updatedAt: string | null;
  updatedByEmail: string | null;
}

export interface AdminStoreCategory {
  id: string;
  slug: string;
  name: LocalizedName;
  icon: string | null;
  imageId: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SaveStoreCategory {
  nameFr: string;
  nameAr: string | null;
  nameEn: string | null;
  /** Empty: made from the French name. */
  slug: string | null;
  icon: string | null;
  imageId: string | null;
}

/** Every affiliate ↔ store partnership (012). */
export interface AdminPartnership {
  id: string;
  status: PartnershipStatus;
  affiliateId: string;
  affiliateName: string;
  affiliateEmail: string;
  affiliateCode: string | null;
  storeId: string;
  storeName: string;
  storeSlug: string;
  storeOwnerId: string;
  storeOwnerName: string;
  storeOwnerEmail: string;
  message: string | null;
  decisionReason: string | null;
  endReason: string | null;
  createdAt: string;
  decidedAt: string | null;
  endedAt: string | null;
}

export interface PartnershipQuery {
  status?: PartnershipStatus;
  search?: string;
  storeId?: string;
  affiliateId?: string;
  page?: number;
  pageSize?: number;
}

/** Every order (016). */
export interface AdminOrderListItem {
  id: string;
  reference: string;
  createdAt: string;
  clientName: string;
  clientPhone: string;
  city: string;
  placedByAffiliate: boolean;
  total: number;
  storeOrders: { id: string; storeId: string; storeName: string; status: StoreOrderStatus; amountToCollect: number }[];
}

export interface AdminOrderQuery {
  status?: StoreOrderStatus;
  search?: string;
  storeId?: string;
  /** Drill-down from the dashboard (023). */
  affiliateId?: string;
  clientId?: string;
  productId?: string;
  /** Inclusive days on `dateField` (placed by default). */
  from?: string;
  to?: string;
  dateField?: 'Placed' | 'Delivered';
  page?: number;
  pageSize?: number;
}
