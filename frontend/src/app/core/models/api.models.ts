export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface AuthResponse {
  token: string;
  email: string;
  fullName: string;
  expiresAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface SignupRequest {
  email: string;
  password: string;
  fullName: string;
  accessToken: string;
}

export interface PlatformCard {
  platformId: string;
  code: string;
  displayName: string;
  description: string;
  icon?: string;
  category: string;
  categoryLabel: string;
  sortOrder: number;
  canConnect: boolean;
  isConnected: boolean;
  accountName?: string;
  connectedAt?: string;
  supportsComments: boolean;
  supportsMessages: boolean;
  supportsPosts: boolean;
}

export interface SocialProfile {
  id: string;
  externalProfileId: string;
  profileType: string;
  name: string;
  username?: string;
}

export interface SocialAccount {
  id: string;
  platformId: string;
  platformCode: string;
  platformName: string;
  externalAccountId: string;
  displayName: string;
  username?: string;
  status: number;
  connectedAt?: string;
  lastSyncAt?: string;
  profiles: SocialProfile[];
}

/** Sent to Integrations/*Callback after Meta popup returns `code`. */
export interface OAuthCallbackRequest {
  code: string;
  redirectUri?: string;
}

export interface SocialPost {
  id: string;
  socialProfileId: string;
  platformId: string;
  platformCode?: string;
  profileName?: string;
  profileUsername?: string;
  externalPostId?: string;
  text?: string;
  caption?: string;
  status: number;
  likeCount: number;
  commentCount: number;
  shareCount: number;
  viewCount: number;
  publishedAt?: string;
  errorMessage?: string;
  createdAt: string;
}

export interface PublishPostResponse {
  success: boolean;
  post: SocialPost;
  errorMessage?: string;
}

export interface InboxPostMeta {
  postId: string;
  pageName: string;
  postText: string;
  postImageUrl?: string;
  likesCount: number;
  commentsCount: number;
  sharesCount: number;
  postedAt: string;
}

export interface InboxItem {
  id: string;
  itemKind: string;
  platformCode: string;
  externalId: string;
  authorName: string;
  authorId?: string;
  content: string;
  isHidden: boolean;
  isRead: boolean;
  isOutgoing?: boolean;
  conversationId?: string;
  receivedAt: string;
  /** Parent post context for Facebook / Instagram comments. */
  post?: InboxPostMeta;
  /** Likes on this comment. */
  commentLikes?: number;
  replyCount?: number;
}

export interface DashboardSummary {
  connectedAccountsCount: number;
  totalPostsCount: number;
  publishedPostsCount: number;
  failedPostsCount: number;
  scheduledPostsCount: number;
  unreadInboxCount: number;
  totalCommentsCount: number;
  totalMessagesCount: number;
}

export const PLATFORM_COLORS: Record<string, string> = {
  facebook: '#1877F2',
  instagram: '#E4405F',
  whatsapp: '#25D366',
  tiktok: '#010101',
  youtube: '#FF0000',
  linkedin: '#0A66C2',
  twitter: '#0F1419',
  x: '#0F1419'
};
