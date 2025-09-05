import {createContainer} from "unstated-next";
import { useEffect, useRef, useState } from "react";
import { getItemRestrictions, getOutfits, getRecentItems, RECENT_ITEMS } from "../../../services/avatar";
import {getInventory} from "../../../services/inventory";
import {SUBMENU_MODE} from "../components/avatarTabSubmenu";
import AuthenticationStore from "../../../stores/authentication";
import {multiGetAssetThumbnails, multiGetOutfitThumbnails} from "../../../services/thumbnails";
import AvatarInfoStore from "./avatarInfoStore";

/** @typedef EmptyType */

/**
 * @typedef SortedItem
 * @property {string} name
 * @property {number} assetId
 * @property {number} assetType
 * @property {string?} thumbnail
 * @property {string} thumbnailState
 * @property {boolean} isLimited
 * @property {boolean} isLimitedUnique
 */

/**
 * @typedef SortedOutfit
 * @property {string} name
 * @property {number} outfitId
 * @property {string} createdAt - ISO 8601 timestamp indicating when the item was created.
 * @property {string?} thumbnail
 * @property {string} thumbnailState
 */

/**
 * @typedef ItemListMetadata
 * @property {number?} itemsLength
 * @property {number?} pageSize
 * @property {string?} nextPageCursor
 * @property {string?} previousPageCursor
 * @property {number?} assetType
 * @property {string?} recentType
 * @property {string?} listMode
 */

const AvatarPageStore = createContainer(() => {
    const [listItems, setListItems] = useState([]);
    const [listItemMetadata, setListItemMetadata] = useState({});
    const [outfits, setOutfits] = useState([]);
    // there is a state for the currently showing and selected list, which is like the Clothing > Hat text
    // there is state for the currently clicked tab. when a tab is clicked, ti will set this state to tiself and open submenu
    //
    // each click closes the open submenu, and resets the above state to the top most state
    // this should only change when list changes
    const [selectedList, setSelectedList] = useState({
        tab: "recent",
        subTab: "all",
    });
    const [openSubmenu, setOpenSubmenu] = useState(null);
    const [activeSubmenu, setActiveSubmenu] = useState(null);
    const [activeTab, setActiveTab] = useState(0);
    // 1 == 3D
    const [thumbnailType, setThumbnailType] = useState(0);
    const auth = AuthenticationStore.useContainer();
    const { setLoadingAvatar, isRendering } = AvatarInfoStore.useContainer();
    
    async function LoadRecentItemsToList(type) {
        setLoadingAvatar(true);
        let recent = (await getRecentItems(type)).data;
        ClearListItems();
        if (recent.length === 0) {
            setListItemMetadata({
                itemsLength: 0,
                pageSize: 50,
                nextPageCursor: null,
                previousPageCursor: null,
                recentType: type,
                listMode: SUBMENU_MODE.DEFAULT,
            });
            setLoadingAvatar(false);
            return;
        }
        let thumbnails = await multiGetAssetThumbnails({ assetIds: recent.map(item => item.id) });
        let itemRestrictions = await getItemRestrictions(recent.map(item => item.id));
        setListItems(recent.map(item => {
            let thumb = thumbnails?.find(v => v.targetId === item.id) || null;
            let rest = itemRestrictions?.find(v => v.assetId === item.id);
            setLoadingAvatar(false);
            return {
                name: item.name,
                assetId: item.id,
                assetType: item.assetType.id,
                thumbnail: thumb?.imageUrl,
                thumbnailState: thumb?.state ?? "Pending",
                isLimited: rest?.isLimited ?? undefined,
                isLimitedUnique: rest?.isLimitedUnique ?? undefined,
            }
        }));
        setListItemMetadata({
            itemsLength: listItems.length,
            pageSize: 50,
            nextPageCursor: null,
            previousPageCursor: null,
            recentType: type,
            listMode: SUBMENU_MODE.DEFAULT,
        });
        setLoadingAvatar(false);
        return listItems;
    }
    
    async function LoadAssetTypeToList(type, loadMore) {
        setLoadingAvatar(true);
        if (!loadMore) ClearListItems(); // TODO: is this right?
        let invList = (await getInventory({
            userId: auth.userId,
            assetTypeId: type,
            cursor: loadMore ? listItemMetadata?.nextPageCursor : null,
            limit: 25,
        })).Data;
        if (invList.Items.length === 0) {
            setListItems([]);
            setListItemMetadata({
                itemsLength: 0,
                pageSize: listItemMetadata?.pageSize ?? invList.ItemsPerPage,
                nextPageCursor: null,
                previousPageCursor: invList.previousPageCursor,
                assetType: type,
                listMode: SUBMENU_MODE.DEFAULT,
            });
            setLoadingAvatar(false);
            return;
        }
        let thumbnails = await multiGetAssetThumbnails({ assetIds: invList.Items.map(item => item.Item.AssetId) });
        let itemRestrictions = await getItemRestrictions(invList.Items.map(item => item.Item.AssetId));
        let newItems = invList.Items.map(item => {
            let thumb = thumbnails?.find(v => v.targetId === item.Item.AssetId) || null;
            let rest = itemRestrictions?.find(v => v.assetId === item.Item.AssetId);
            setLoadingAvatar(false);
            return {
                name: item.Item.Name,
                assetId: item.Item.AssetId,
                assetType: item.Item.AssetType,
                thumbnail: thumb?.imageUrl,
                thumbnailState: thumb?.state ?? "Pending",
                isLimited: rest?.isLimited ?? undefined,
                isLimitedUnique: rest?.isLimitedUnique ?? undefined,
            }
        });
        if (loadMore) {
            setListItems(prev => [...prev, ...newItems]);
        } else {
            setListItems(newItems);
        }
        setListItemMetadata({
            itemsLength: listItems.length,
            pageSize: listItemMetadata?.pageSize ?? invList.ItemsPerPage,
            nextPageCursor: invList.nextPageCursor,
            previousPageCursor: invList.previousPageCursor,
            assetType: type,
            listMode: SUBMENU_MODE.DEFAULT,
        });
        setLoadingAvatar(false);
        return listItems;
    }
    
    async function LoadOutfits() {
        setOutfits([]);
        let outfits = await getOutfits({ userId: auth.userId, limit: 100 });
        if (outfits.total === 0) return;
        
        let outfitThumbnails = await multiGetOutfitThumbnails(
            { userOutfitIds: outfits.data.map(v => v.id), size: '100x100' }
        );
        setOutfits(outfits.data.map(outfit => {
            let outfitThumb = outfitThumbnails?.find(v => v.targetId === outfit.id) || null;
            return {
                name: outfit.name,
                outfitId: outfit.id,
                createdAt: outfit.created,
                thumbnail: outfitThumb?.imageUrl,
                thumbnailState: outfitThumb?.state ?? "Pending",
            };
        }));
        return outfits;
    }
    
    function LoadNewThumbnailType(thumbnailType) {
        if (isRendering) return;
        setThumbnailType(thumbnailType);
    }
    
    function ClearListItems() {
        setListItems([]);
        setListItemMetadata({});
    }
    
    useEffect(async () => {
        if (listItems.length === 0) await LoadRecentItemsToList(RECENT_ITEMS.ALL);
    }, []);
    
    return {
        LoadRecentItemsToList,
        LoadAssetTypeToList,
        LoadOutfits,
        ClearListItems,
        LoadNewThumbnailType,
        
        /** @type SortedItem[] */
        listItems,
        /** @type ItemListMetadata */
        listItemMetadata,
        
        /** @type SortedOutfit[] */
        outfits,
        setOutfits,
        
        selectedList,
        setSelectedList,
        openSubmenu,
        setOpenSubmenu,
        
        activeSubmenu,
        setActiveSubmenu,
        
        activeTab,
        setActiveTab,
        
        thumbnailType,
    }
})

export default AvatarPageStore;
