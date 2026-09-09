import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PROCESS_MODULE_LIST, ProcessMenuType } from '../config/process.config';
import {
  CreateMetaAdSetRequest,
  CreateMetaCampaignRequest,
  CreateMetaProductRequest,
  MetaAd,
  MetaAdAccount,
  MetaAdSet,
  MetaApiResponse,
  MetaCampaign,
  MetaCatalog,
  MetaInsightsQuery,
  MetaInsightsSummary,
  MetaListQuery,
  MetaPagedResult,
  MetaProduct,
  UpdateMetaAdRequest,
  UpdateMetaAdSetRequest,
  UpdateMetaCampaignRequest,
  UpdateMetaProductRequest
} from '../models/meta-ads.models';

@Injectable({ providedIn: 'root' })
export class MetaAdsApiService {
  private readonly root = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private base(menuType: ProcessMenuType): string {
    const module = PROCESS_MODULE_LIST.find(m => m.id === menuType)!;
    return `${this.root}/${module.apiBase}/meta-ads`;
  }

  getAdAccounts(menuType: ProcessMenuType): Observable<MetaApiResponse<MetaAdAccount[]>> {
    return this.http.get<MetaApiResponse<MetaAdAccount[]>>(`${this.base(menuType)}/ad-accounts`);
  }

  getCampaigns(menuType: ProcessMenuType, query: MetaListQuery): Observable<MetaApiResponse<MetaPagedResult<MetaCampaign>>> {
    return this.http.get<MetaApiResponse<MetaPagedResult<MetaCampaign>>>(`${this.base(menuType)}/campaigns`, {
      params: this.listParams(query)
    });
  }

  createCampaign(menuType: ProcessMenuType, body: CreateMetaCampaignRequest): Observable<MetaApiResponse<MetaCampaign>> {
    return this.http.post<MetaApiResponse<MetaCampaign>>(`${this.base(menuType)}/campaigns`, body);
  }

  updateCampaign(
    menuType: ProcessMenuType,
    campaignId: string,
    body: UpdateMetaCampaignRequest
  ): Observable<MetaApiResponse<MetaCampaign>> {
    return this.http.put<MetaApiResponse<MetaCampaign>>(`${this.base(menuType)}/campaigns/${campaignId}`, body);
  }

  getAdSets(menuType: ProcessMenuType, query: MetaListQuery): Observable<MetaApiResponse<MetaPagedResult<MetaAdSet>>> {
    return this.http.get<MetaApiResponse<MetaPagedResult<MetaAdSet>>>(`${this.base(menuType)}/adsets`, {
      params: this.listParams(query)
    });
  }

  createAdSet(menuType: ProcessMenuType, body: CreateMetaAdSetRequest): Observable<MetaApiResponse<MetaAdSet>> {
    return this.http.post<MetaApiResponse<MetaAdSet>>(`${this.base(menuType)}/adsets`, body);
  }

  updateAdSet(
    menuType: ProcessMenuType,
    adSetId: string,
    body: UpdateMetaAdSetRequest
  ): Observable<MetaApiResponse<MetaAdSet>> {
    return this.http.put<MetaApiResponse<MetaAdSet>>(`${this.base(menuType)}/adsets/${adSetId}`, body);
  }

  getAds(menuType: ProcessMenuType, query: MetaListQuery): Observable<MetaApiResponse<MetaPagedResult<MetaAd>>> {
    return this.http.get<MetaApiResponse<MetaPagedResult<MetaAd>>>(`${this.base(menuType)}/ads`, {
      params: this.listParams(query)
    });
  }

  updateAd(menuType: ProcessMenuType, adId: string, body: UpdateMetaAdRequest): Observable<MetaApiResponse<MetaAd>> {
    return this.http.put<MetaApiResponse<MetaAd>>(`${this.base(menuType)}/ads/${adId}`, body);
  }

  getInsights(menuType: ProcessMenuType, query: MetaInsightsQuery): Observable<MetaApiResponse<MetaInsightsSummary>> {
    let params = new HttpParams().set('objectId', query.objectId);
    if (query.datePreset) params = params.set('datePreset', query.datePreset);
    if (query.since) params = params.set('since', query.since);
    if (query.until) params = params.set('until', query.until);
    if (query.level) params = params.set('level', query.level);
    return this.http.get<MetaApiResponse<MetaInsightsSummary>>(`${this.base(menuType)}/insights`, { params });
  }

  getCatalogs(
    menuType: ProcessMenuType,
    after?: string,
    limit = 25
  ): Observable<MetaApiResponse<MetaPagedResult<MetaCatalog>>> {
    let params = new HttpParams().set('limit', limit);
    if (after) params = params.set('after', after);
    return this.http.get<MetaApiResponse<MetaPagedResult<MetaCatalog>>>(`${this.base(menuType)}/catalogs`, { params });
  }

  getProducts(
    menuType: ProcessMenuType,
    catalogId: string,
    search?: string,
    after?: string,
    limit = 25
  ): Observable<MetaApiResponse<MetaPagedResult<MetaProduct>>> {
    let params = new HttpParams().set('catalogId', catalogId).set('limit', limit);
    if (search) params = params.set('search', search);
    if (after) params = params.set('after', after);
    return this.http.get<MetaApiResponse<MetaPagedResult<MetaProduct>>>(
      `${this.base(menuType)}/catalogs/${encodeURIComponent(catalogId)}/products`,
      { params }
    );
  }

  createProduct(
    menuType: ProcessMenuType,
    catalogId: string,
    body: CreateMetaProductRequest
  ): Observable<MetaApiResponse<MetaProduct>> {
    return this.http.post<MetaApiResponse<MetaProduct>>(
      `${this.base(menuType)}/catalogs/${encodeURIComponent(catalogId)}/products`,
      body
    );
  }

  updateProduct(
    menuType: ProcessMenuType,
    catalogId: string,
    productId: string,
    body: UpdateMetaProductRequest
  ): Observable<MetaApiResponse<MetaProduct>> {
    return this.http.put<MetaApiResponse<MetaProduct>>(
      `${this.base(menuType)}/catalogs/${encodeURIComponent(catalogId)}/products/${encodeURIComponent(productId)}`,
      body
    );
  }

  deleteProduct(
    menuType: ProcessMenuType,
    catalogId: string,
    productId: string
  ): Observable<MetaApiResponse<object>> {
    return this.http.delete<MetaApiResponse<object>>(
      `${this.base(menuType)}/catalogs/${encodeURIComponent(catalogId)}/products/${encodeURIComponent(productId)}`
    );
  }

  private listParams(query: MetaListQuery): HttpParams {
    let params = new HttpParams();
    if (query.adAccountId) params = params.set('adAccountId', query.adAccountId);
    if (query.campaignId) params = params.set('campaignId', query.campaignId);
    if (query.adSetId) params = params.set('adSetId', query.adSetId);
    if (query.status) params = params.set('status', query.status);
    if (query.search) params = params.set('search', query.search);
    if (query.after) params = params.set('after', query.after);
    if (query.limit) params = params.set('limit', query.limit);
    return params;
  }
}
