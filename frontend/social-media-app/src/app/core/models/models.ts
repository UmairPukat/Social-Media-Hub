export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export enum SocialPlatform {
  Facebook = 1,
  Instagram = 2,
  WhatsApp = 3,
  YouTube = 4,
  LinkedIn = 5,
  TikTok = 6,
  Twitter = 7,
  Other = 99
}

export enum ConnectionStatus {
  Disconnected = 0,
  Connected = 1,
  Expired = 2,
  Error = 3
}

export enum InboxItemType {
  Comment = 1,
  Message = 2
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  email: string;
  token: string;
  expiresAt: string;
}

export interface PlatformCard {
  platform: SocialPlatform;
  name: string;
  description: string;
  icon: string;
  isConnected: boolean;
  isImplemented: boolean;
  accountName?: string;
  status: ConnectionStatus;
}

export interface SocialPost {
  id: string;
  platform: SocialPlatform;
  content: string;
  mediaUrl?: string;
  externalPostId?: string;
  status: number;
  publishedAt?: string;
  errorMessage?: string;
  createdAt: string;
}

export interface InboxItem {
  id: string;
  platform: SocialPlatform;
  platformName: string;
  itemType: InboxItemType;
  externalId: string;
  parentExternalId?: string;
  senderName: string;
  senderId?: string;
  message: string;
  isHidden: boolean;
  isRead: boolean;
  receivedAt: string;
}
