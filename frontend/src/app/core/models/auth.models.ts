export type Role = 'Admin' | 'Editor' | 'User' | 'Viewer';

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  roles: Role[];
}

export interface SsoSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
}
