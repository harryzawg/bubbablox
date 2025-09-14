import request, { getBaseUrl, getFullUrl } from "../lib/request"

export const getUserRobloxBadges = ({ userId }) => {
  return request('GET', getFullUrl('accountinformation', `/v1/users/${userId}/roblox-badges`)).then(d => d.data)
}

export const getUserBadges = ({ userId }) => {
  return request('GET', getFullUrl('accountinformation', `/v1/users/${userId}/badges`)).then(d => d.data)
}

export const setUserDescription = ({ newDescription }) => {
  return request('POST', getFullUrl('accountinformation', `/v1/description`), {
    description: newDescription,
  })
}