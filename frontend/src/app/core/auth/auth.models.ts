export type Role = 'Buyer' | 'Affiliate' | 'StoreOwner' | 'Admin';

export interface User {
  id: string;
  email: string;
  fullName: string;
  phone: string;
  roles: Role[];
  /** "DRF-45" — only for affiliates. Display it as "#DRF-45". */
  affiliateCode: string | null;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** What a new account starts as. Store owner (and every other extra role) is asked for later. */
export type AccountType = Extract<Role, 'Buyer' | 'Affiliate'>;

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  phone: string;
  accountType: AccountType;
  /** Required when accountType is 'Affiliate'. */
  acceptAffiliateTerms: boolean;
}
