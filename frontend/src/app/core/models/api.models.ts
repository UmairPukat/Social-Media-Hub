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
  /** True right after Meta login, while no page has been picked yet. */
  requiresPageSelection?: boolean;
}

/** Connected page details shown in the account information popup. */
export interface ConnectionDetails {
  platformCode: string;
  platformName: string;
  accountName: string;
  status: number;
  connectedAt?: string;
  lastSyncAt?: string;
  pageId?: string;
  pageName?: string;
  pageImage?: string;
  instagramId?: string;
  instagramUsername?: string;
  /** Page access token stored after Meta connect. */
  accessToken?: string;
  webhookSubscribed: boolean;
  subscribedFields: string[];
  webhookError?: string;
  profiles: SocialProfile[];
}

/** User-owned Meta app with its own App Id, secret, and callback URL. */
export interface MetaAppConnection {
  id: string;
  name: string;
  platformCode: string;
  platformName: string;
  appId: string;
  callbackUrl: string;
  graphApiVersion: string;
  scopes: string;
  isConnected: boolean;
  accountName?: string;
  connectedAt?: string;
  supportsComments: boolean;
  supportsMessages: boolean;
  supportsPosts: boolean;
  canConnect: boolean;
  requiresPageSelection?: boolean;
}

export interface CreateMetaAppConnectionRequest {
  name: string;
  platformCode: string;
  appId: string;
  appSecret: string;
  callbackUrl: string;
  graphApiVersion?: string;
  scopes?: string;
}

export interface UpdateMetaAppConnectionRequest {
  name: string;
  appId: string;
  appSecret: string;
  callbackUrl: string;
  graphApiVersion?: string;
  scopes?: string;
}

export interface AppConnectionDefaultScopes {
  platformCode: string;
  scopes: string;
}

/** App Connections account details popup. */
export interface AppConnectionDetails {
  appConnectionId: string;
  appConnectionName: string;
  platformCode: string;
  platformName: string;
  accountName: string;
  status: number;
  connectedAt?: string;
  lastSyncAt?: string;
  pageId?: string;
  pageName?: string;
  pageImage?: string;
  instagramId?: string;
  instagramUsername?: string;
  accessToken?: string;
  webhookSubscribed: boolean;
  subscribedFields: string[];
  webhookError?: string;
  appId: string;
  callbackUrl: string;
  scopes: string;
  profiles: SocialProfile[];
}

/** A Facebook Page offered in the page picker after Meta login. */
export interface MetaPage {
  pageId: string;
  pageName: string;
  pageImage?: string;
  instagramId?: string;
  instagramUsername?: string;
  isEligible: boolean;
  ineligibleReason?: string;
  isSelected: boolean;
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
  /** Set on a comment reply so the Inbox can nest it under its parent. */
  parentId?: string;
  /** Quoted message this one replies to. */
  replyToId?: string;
  replyToAuthor?: string;
  replyToContent?: string;
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
