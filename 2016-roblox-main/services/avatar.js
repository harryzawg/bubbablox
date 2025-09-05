import request from "../lib/request"
import { getFullUrl } from "../lib/request";

export const getRules = () => {
  return request('GET', getFullUrl('avatar', '/v1/avatar-rules')).then(d => d.data);
}

export const getAvatar = ({ userId }) => {
  return request('GET', getFullUrl('avatar', '/v1/users/' + userId + '/avatar')).then(d => d.data);
}

export const getMyAvatar = () => {
  return request('GET', getFullUrl('avatar', '/v1/avatar')).then(d => d.data);
}

/**
 * @typedef ItemRestrictionsClass
 * @property {number} assetId
 * @property {boolean} isLimited
 * @property {boolean} isLimitedUnique
 * @property {boolean} exists
 */

/**
 * @param {number[]} assetIds
 * @returns {Promise<ItemRestrictionsClass[]>}
 */
export const getItemRestrictions = (assetIds) => {
  return request('GET', getFullUrl('api', `/v1/items/restrictions?assetIds=${assetIds}`)).then(d => d.data);
}

/**
 *
 * @param {string} listType
 * @returns {Promise<PekoraCollection<Asset>>}
 */
export const getRecentItems = async (listType) => {
  let req = await request("GET", getFullUrl("avatar", `/v1/recent-items/${listType}/list`));
  return req.data;
}

export const RECENT_ITEMS = Object.freeze({
  ALL: "all",
  CLOTHING: "clothing",
  BODY_PARTS: "bodyparts",
  ANIMATIONS: "avataranimations",
  ACCESSORIES: "accessories",
  OUTFITS: "outfits",
  GEAR: "gear",
})

export const redrawMyAvatar = () => {
  return request('POST', getFullUrl('avatar', '/v1/avatar/redraw-thumbnail')).then(d => d.data);
}

export const setWearingAssets = ({ assetIds }) => {
  return request('POST', getFullUrl('avatar', '/v1/avatar/set-wearing-assets'), {
    assetIds,
  });
}

export const setColors = (bodyColors) => {
  return request('POST', getFullUrl('avatar', '/v1/avatar/set-body-colors'), bodyColors);
}

export const setRigType = (rigType) => {
  return request('POST', getFullUrl('avatar', '/v1/avatar/set-player-avatar-type'), {
    playerAvatarType: rigType === "R15" ? 2 : 1,
  });
}

export const setScales = (scales) => {
  return request('POST', getFullUrl('avatar', '/v1/avatar/set-scales'), scales);
}

/**
 * @typedef {Object} OutfitItem
 * @property {number} id - Unique identifier for the item.
 * @property {string} name - Name of the item.
 * @property {string} created - ISO 8601 timestamp indicating when the item was created.
 */

/**
 * @typedef {Object} OutfitResponse
 * @property {number} filteredCount - Number of items after applying filters (can be 0).
 * @property {OutfitItem[]} data - Array of item objects.
 * @property {number} total - Total number of items available.
 */

/**
 * @param {number} userId
 * @param {number?} limit
 * @returns {OutfitResponse}
 */
export const getOutfits = ({ userId, limit = 50 }) => {
  return request('GET', getFullUrl('avatar', '/v1/users/' + userId + `/outfits?itemsPerPage=${limit}&page=1`)).then(d => d.data);
}

export const createOutfit = ({ name }) => {
  return request('POST', getFullUrl('avatar', '/v1/outfits/create'), {
    name,
  });
}

export const wearOutfit = ({ outfitId }) => {
  return request('POST', getFullUrl('avatar', '/v1/outfits/' + outfitId + '/wear'));
}

export const deleteOutfit = ({ outfitId }) => {
  return request('POST', getFullUrl('avatar', '/v1/outfits/' + outfitId + '/delete'));
}

export const renameOutfit = ({ outfitId, name }) => {
  return request('POST', getFullUrl('avatar', `/v1/outfits/${outfitId}/rename`), {
    name,
  });
}

export const updateOutfit = ({ outfitId }) => {
  return request('PATCH', getFullUrl('avatar', '/v1/outfits/' + outfitId), {});
}
