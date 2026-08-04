import { InboxItem } from '../models/api.models';

/** Demo inbox data so FB / IG comment threads and WhatsApp chats render without live webhooks. */
export const INBOX_DEMO_ITEMS: InboxItem[] = [
  // —— Facebook comments (post-centric) ——
  {
    id: 'demo-fb-c1',
    itemKind: 'comment',
    platformCode: 'facebook',
    externalId: 'fb_c_1',
    authorName: 'Umair Khan',
    authorId: 'fb_u_1',
    content: 'This looks amazing! When is the next drop?',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T18:21:00Z',
    commentLikes: 24,
    replyCount: 3,
    post: {
      postId: 'fb_post_1',
      pageName: 'SocialHub Page',
      postText: 'Excited to share our new summer collection — soft tones, everyday fits, and limited colors. Tell us which one you’d wear first!',
      postImageUrl: 'https://images.unsplash.com/photo-1523381210714-942fd1c3c2e7?w=900&q=80',
      likesCount: 1284,
      commentsCount: 86,
      sharesCount: 42,
      postedAt: '2026-08-02T14:00:00Z'
    }
  },
  {
    id: 'demo-fb-c2',
    itemKind: 'comment',
    platformCode: 'facebook',
    externalId: 'fb_c_2',
    authorName: 'Sara Ahmed',
    authorId: 'fb_u_2',
    content: 'Love the blue one. Already shared with my friends!',
    isHidden: false,
    isRead: true,
    receivedAt: '2026-08-03T16:05:00Z',
    commentLikes: 11,
    replyCount: 1,
    post: {
      postId: 'fb_post_1',
      pageName: 'SocialHub Page',
      postText: 'Excited to share our new summer collection — soft tones, everyday fits, and limited colors. Tell us which one you’d wear first!',
      postImageUrl: 'https://images.unsplash.com/photo-1523381210714-942fd1c3c2e7?w=900&q=80',
      likesCount: 1284,
      commentsCount: 86,
      sharesCount: 42,
      postedAt: '2026-08-02T14:00:00Z'
    }
  },
  {
    id: 'demo-fb-c3',
    itemKind: 'comment',
    platformCode: 'facebook',
    externalId: 'fb_c_3',
    authorName: 'Ali Raza',
    authorId: 'fb_u_3',
    content: 'Do you ship internationally?',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-04T09:12:00Z',
    commentLikes: 4,
    replyCount: 0,
    post: {
      postId: 'fb_post_2',
      pageName: 'SocialHub Page',
      postText: 'Behind the scenes from yesterday’s studio shoot. Full gallery drops Friday.',
      postImageUrl: 'https://images.unsplash.com/photo-1490481651871-ab68de25d43d?w=900&q=80',
      likesCount: 512,
      commentsCount: 29,
      sharesCount: 18,
      postedAt: '2026-08-03T11:30:00Z'
    }
  },

  // —— Instagram comments ——
  {
    id: 'demo-ig-c1',
    itemKind: 'comment',
    platformCode: 'instagram',
    externalId: 'ig_c_1',
    authorName: 'maya.styles',
    authorId: 'ig_u_1',
    content: 'The lighting on this shot is perfect ✨',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T20:40:00Z',
    commentLikes: 58,
    replyCount: 2,
    post: {
      postId: 'ig_post_1',
      pageName: 'socialhub.official',
      postText: 'Golden hour mood. New lookbook frames are live.',
      postImageUrl: 'https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?w=900&q=80',
      likesCount: 9420,
      commentsCount: 214,
      sharesCount: 96,
      postedAt: '2026-08-03T17:00:00Z'
    }
  },
  {
    id: 'demo-ig-c2',
    itemKind: 'comment',
    platformCode: 'instagram',
    externalId: 'ig_c_2',
    authorName: 'travel.with.zoe',
    authorId: 'ig_u_2',
    content: 'Where was this taken?',
    isHidden: false,
    isRead: true,
    receivedAt: '2026-08-03T21:10:00Z',
    commentLikes: 9,
    replyCount: 0,
    post: {
      postId: 'ig_post_1',
      pageName: 'socialhub.official',
      postText: 'Golden hour mood. New lookbook frames are live.',
      postImageUrl: 'https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?w=900&q=80',
      likesCount: 9420,
      commentsCount: 214,
      sharesCount: 96,
      postedAt: '2026-08-03T17:00:00Z'
    }
  },
  {
    id: 'demo-ig-c3',
    itemKind: 'comment',
    platformCode: 'instagram',
    externalId: 'ig_c_3',
    authorName: 'fit.kareem',
    authorId: 'ig_u_3',
    content: 'Need this in black 🔥',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-04T08:02:00Z',
    commentLikes: 17,
    replyCount: 1,
    post: {
      postId: 'ig_post_2',
      pageName: 'socialhub.official',
      postText: 'Street edit — volume 03.',
      postImageUrl: 'https://images.unsplash.com/photo-1483985988355-763728e1935b?w=900&q=80',
      likesCount: 3102,
      commentsCount: 74,
      sharesCount: 33,
      postedAt: '2026-08-01T15:20:00Z'
    }
  },

  // —— Facebook Messenger-style messages ——
  {
    id: 'demo-fb-m1',
    itemKind: 'message',
    platformCode: 'facebook',
    externalId: 'fb_m_1',
    authorName: 'Umair Khan',
    authorId: 'fb_u_1',
    content: 'Hi',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T18:21:00Z'
  },
  {
    id: 'demo-fb-m2',
    itemKind: 'message',
    platformCode: 'facebook',
    externalId: 'fb_m_2',
    authorName: 'Umair Khan',
    authorId: 'fb_u_1',
    content: 'Is the navy jacket still available in size M?',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T18:22:30Z'
  },
  {
    id: 'demo-fb-m3',
    itemKind: 'message',
    platformCode: 'facebook',
    externalId: 'fb_m_3',
    authorName: 'Nadia Malik',
    authorId: 'fb_u_4',
    content: 'Thanks for the quick reply earlier!',
    isHidden: false,
    isRead: true,
    receivedAt: '2026-08-02T12:40:00Z'
  },

  // —— Instagram DMs ——
  {
    id: 'demo-ig-m1',
    itemKind: 'message',
    platformCode: 'instagram',
    externalId: 'ig_m_1',
    authorName: 'maya.styles',
    authorId: 'ig_u_1',
    content: 'Can you collab on a reel next week?',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T19:55:00Z'
  },
  {
    id: 'demo-ig-m2',
    itemKind: 'message',
    platformCode: 'instagram',
    externalId: 'ig_m_2',
    authorName: 'maya.styles',
    authorId: 'ig_u_1',
    content: 'I can shoot Tuesday afternoon.',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T19:56:10Z'
  },

  // —— WhatsApp messages ——
  {
    id: 'demo-wa-m1',
    itemKind: 'message',
    platformCode: 'whatsapp',
    externalId: 'wa_m_1',
    authorName: '+92 300 1234567',
    authorId: 'wa_u_1',
    content: 'Assalam o Alaikum — I placed an order yesterday. Tracking?',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T10:15:00Z'
  },
  {
    id: 'demo-wa-m2',
    itemKind: 'message',
    platformCode: 'whatsapp',
    externalId: 'wa_m_2',
    authorName: '+92 300 1234567',
    authorId: 'wa_u_1',
    content: 'Order #SH-20418',
    isHidden: false,
    isRead: false,
    receivedAt: '2026-08-03T10:15:40Z'
  },
  {
    id: 'demo-wa-m3',
    itemKind: 'message',
    platformCode: 'whatsapp',
    externalId: 'wa_m_3',
    authorName: '+92 321 9876543',
    authorId: 'wa_u_2',
    content: 'Please confirm store hours for Saturday.',
    isHidden: false,
    isRead: true,
    receivedAt: '2026-08-02T08:05:00Z'
  },
  {
    id: 'demo-wa-m4',
    itemKind: 'message',
    platformCode: 'whatsapp',
    externalId: 'wa_m_4',
    authorName: '+92 333 5551212',
    authorId: 'wa_u_3',
    content: 'Test Message',
    isHidden: false,
    isRead: true,
    receivedAt: '2026-08-01T15:00:00Z'
  }
];
