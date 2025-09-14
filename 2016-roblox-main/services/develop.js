import getFlag from "../lib/getFlag";
import request, { getBaseUrl, getFullUrl } from "../lib/request"

export const uploadAsset = ({ name, assetTypeId, file, groupId }) => {
  let formData = new FormData();
  formData.append('name', name);
  formData.append('assetType', assetTypeId);
  formData.append('file', file);
  if (groupId) {
    formData.append('groupId', groupId);
  }
  return request('POST', getBaseUrl() + 'develop/upload', formData);
}

export const uploadBadgePass = ({ name, description, assetTypeId, placeId, file, groupId }) => {
  let formData = new FormData();
  formData.append('name', name);
  formData.append('description', description);
  formData.append('assetType', assetTypeId);
  formData.append('placeId', placeId);
  formData.append('file', file);
  if (groupId) {
    formData.append('groupId', groupId);
  }
  return request('POST', getBaseUrl() + 'develop/upload', formData);
}

export const uploadAssetVersion = ({assetId, file}) => {
  let form = new FormData();
  form.append('assetId', assetId);
  form.append('file', file);
  return request('POST', getBaseUrl() + 'develop/upload-version', form);
}

export const getCreatedAssetDetails = (assetIds) => {
  return request('POST', getFullUrl('itemconfiguration', '/v1/creations/get-asset-details'), {
    assetIds,
  })
}

export const getCreatedItems = ({ assetType, limit, cursor, groupId }) => {
  let url = '/v1/creations/get-assets?assetType=' + assetType + '&limit=' + limit + '&cursor=' + encodeURIComponent(cursor);
  if (groupId) {
    url = url +'&groupId=' + encodeURIComponent(groupId);
  }
  return request('GET', getFullUrl('itemconfiguration', url)).then(assets => {
    if (assets.data.data.length !== 0) {
      return getCreatedAssetDetails(assets.data.data.map(v => v.assetId)).then(d => {
        assets.data.data = d.data.sort((a, b) => a.assetId > b.assetId ? -1 : 1)
        return assets.data;
      })
    }
    return assets.data;
  })
}

export const updateAsset = async ({assetId, name, description, genres, isCopyingAllowed, enableComments}) => {
  return await request('PATCH', getFullUrl('develop', `/v1/assets/${assetId}`), {
    name,
    description,
    genres,
    isCopyingAllowed,
    enableComments,
  });
}

export const setAssetPrice = async ({assetId, priceInRobux, priceInTickets}) => {
  let obj = {
    priceInRobux, 
  };
  if (getFlag('sellItemForTickets', true)) {
    obj.priceInTickets = priceInTickets;
  }
  return await request('POST', getFullUrl('itemconfiguration', `/v1/assets/${assetId}/update-price`), obj);
}

export const getAllGenres = async () => {
  return (await request('GET', getFullUrl('develop', '/v1/assets/genres'))).data.data;
}

export const setUniverseMaxPlayers = async ({universeId, maxPlayers}) => {
  return await request('PATCH',getFullUrl('develop', `/v1/universes/${universeId}/max-player-count`), {
    maxPlayers,
  });
}

export const setGearPermissions = async ({universeId, enabled}) => {
  return await request('PATCH', getFullUrl('develop', `/v1/universes/${universeId}/gear-permissions`), {
    isEnabled: enabled,
  });
}

export const setPlayable = async ({universeId, isPlayable}) => {
  return await request('PATCH', getFullUrl('develop', `/v1/universes/${universeId}/playable`), {
    isPlayable,
  });
}

export const get2020Menu = async () => {
  try {
    const response = await request('GET', getFullUrl('users', '/v1/user/get-2020-menu'));
    return response.data;
  } catch (error) {
    console.error('failed to get 2020 menu pref:', error);
    throw error;
  }
}

export const set2020Menu = async ({ enabled }) => {
  try {
    const response = await request('PATCH', getFullUrl('users', '/v1/users/2020-menu'), {
      enabled,
    });
    return response.data;
  } catch (error) {
    console.error('failed to set 2020 menu pref:', error);
    throw error;
  }
}

export const setPlaceYear = async ({ universeId, year }) => {
  try {
    const response = await request('PATCH', getFullUrl('develop', `/v1/universes/${universeId}/year`), {
      year,
    });
    return response.data;
  } catch (error) {
    console.error('failed to set place year:', error);
    throw error;
  }
}
 
export const setRigType = async ({ universeId, rigType }) => {
  try {
    const response = await request('PATCH', getFullUrl('develop', `/v1/universes/${universeId}/rig-type`), {
      rigType,
    });
    return response.data;
  } catch (error) {
    console.error('failed to set rig type:', error);
    throw error;
  }
}