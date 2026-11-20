export interface MetaPagedResult<T> {
  items: T[];
  nextCursor?: string | null;
  count: number;
}

export interface MetaAdAccount {
  id: string;
  name: string;
  currency?: string;
  accountStatus?: string;
  businessName?: string;
  amountSpent?: string;
  balance?: string;
}

export interface MetaCampaign {
  id: string;
  name: string;
  objective?: string;
  status?: string;
  effectiveStatus?: string;
  dailyBudget?: string;
  createdTime?: string;
  updatedTime?: string;
  pageId?: string;
}

export interface MetaAdSet {
  id: string;
  name: string;
  campaignId?: string;
  status?: string;
  effectiveStatus?: string;
  dailyBudget?: string;
  lifetimeBudget?: string;
  startTime?: string;
  endTime?: string;
  targetingSummary?: string;
  placements?: string;
}

export interface MetaAd {
  id: string;
  name: string;
  adSetId?: string;
  campaignId?: string;
  status?: string;
  effectiveStatus?: string;
  creativeId?: string;
  pageId?: string;
}

export interface MetaInsightRow {
  campaignName?: string;
  adSetName?: string;
  adName?: string;
  dateStart?: string;
  dateStop?: string;
  impressions?: string;
  reach?: string;
  clicks?: string;
  spend?: string;
  ctr?: string;
  cpc?: string;
  cpm?: string;
  cpp?: string;
  actions?: string;
  purchaseRoas?: string;
  results?: string;
}

export interface MetaInsightsSummary {
  objectId: string;
  datePreset: string;
  level: string;
  spend: string;
  impressions: string;
  reach: string;
  clicks: string;
  ctr: string;
  cpc: string;
  cpm: string;
  cpp: string;
  results: string;
  rows: MetaInsightRow[];
}

export interface MetaUsageSnapshot {
  businessUseCasePercent: number;
  adAccountPercent: number;
  appPercent: number;
  overallPercent: number;
  isApproachingLimit: boolean;
}

export interface MetaApiHealthDailyPoint {
  date: string;
  calls: number;
  errors: number;
}

export interface MetaApiHealth {
  totalCalls15Days: number;
  errorRatePercent: number;
  tierLabel: string;
  tierStatus: string;
  currentUsage: MetaUsageSnapshot;
  dailyCalls: MetaApiHealthDailyPoint[];
}

export interface MetaCatalog {
  id: string;
  name: string;
  vertical?: string;
  productCount?: string;
  businessId?: string;
}

export interface MetaProduct {
  id: string;
  name: string;
  retailerId?: string;
  price?: string;
  currency?: string;
  availability?: string;
  imageUrl?: string;
  url?: string;
  reviewStatus?: string;
}

export interface MetaListQuery {
  adAccountId?: string;
  campaignId?: string;
  adSetId?: string;
  status?: string;
  search?: string;
  after?: string;
  includeCampaignId?: string;
  limit?: number;
}

export interface CreateMetaCampaignRequest {
  adAccountId: string;
  name: string;
  objective: string;
  status?: string;
  specialAdCategories?: string[];
  pageId?: string;
}

export interface UpdateMetaCampaignRequest {
  name?: string;
  status?: string;
}

export interface CreateMetaAdSetRequest {
  adAccountId: string;
  campaignId: string;
  name: string;
  status?: string;
  dailyBudget?: number;
  billingEvent?: string;
  optimizationGoal?: string;
  startTime?: string;
  endTime?: string;
}

export interface UpdateMetaAdSetRequest {
  name?: string;
  status?: string;
  dailyBudget?: number;
}

export interface UpdateMetaAdRequest {
  name?: string;
  status?: string;
}

export interface MetaInsightsQuery {
  objectId: string;
  datePreset?: string;
  since?: string;
  until?: string;
  level?: string;
  fields?: string;
}

export interface CreateMetaCatalogRequest {
  businessId?: string;
  name: string;
  vertical?: string;
}

export interface CreateMetaProductRequest {
  catalogId: string;
  businessId?: string;
  name: string;
  retailerId: string;
  price: string;
  currency?: string;
  availability?: string;
  url?: string;
  imageUrl?: string;
  description?: string;
}

export interface UpdateMetaProductRequest {
  name?: string;
  price?: string;
  currency?: string;
  availability?: string;
  url?: string;
  imageUrl?: string;
  description?: string;
}

export interface MetaPageOption {
  id: string;
  name: string;
}

export interface MetaLeadForm {
  id: string;
  name: string;
  status?: string;
  createdTime?: string;
}

export interface MetaLeadField {
  name: string;
  value?: string;
}

export interface MetaLead {
  id: string;
  createdTime?: string;
  formId?: string;
  formName?: string;
  pageId?: string;
  pageName?: string;
  adId?: string;
  adName?: string;
  campaignId?: string;
  campaignName?: string;
  customerName?: string;
  email?: string;
  phone?: string;
  status: string;
  notes?: string;
  source?: string;
  fields: MetaLeadField[];
}

export interface UpdateMetaLeadRequest {
  status?: string;
  notes?: string;
}

export interface CreateMetaAdRequest {
  adAccountId: string;
  adSetId: string;
  name: string;
  pageId: string;
  link: string;
  message?: string;
}

export interface MetaAdPreview {
  html?: string;
}

export interface MetaApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  metaErrorCode?: string;
  metaErrorMessage?: string;
  metaUsage?: MetaUsageSnapshot;
  graphApiCallCount?: number;
}
