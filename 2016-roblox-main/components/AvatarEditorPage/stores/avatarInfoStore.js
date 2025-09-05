import {createContainer} from "unstated-next";
import {useEffect, useRef, useState} from "react";
//import FeedbackStore from "../../../stores/feedback";
//import {FeedbackType} from "../../../models/feedback";
import { multiGetUserThumbnails } from "../../../services/thumbnails";
import AuthenticationStore from "../../../stores/authentication";
import {
    getItemRestrictions,
    getMyAvatar,
    getRules,
    redrawMyAvatar,
    setColors,
    setRigType,
    setScales
} from "../../../services/avatar";
import * as AvatarService from "../../../services/avatar";
import { Stopwatch, wait } from "../../../lib/utils";
import request from "../../../lib/request";

/**
 * @typedef WearingAsset
 * @property {string} name
 * @property {number} assetId
 * @property {number} assetType
 * @property {string} assetTypeName
 */

const AvatarInfoStore = createContainer(() => {
    // CURRENT STUFF
    /** @type {WearingAsset[]} */
    const [wearingAssets, setWearingAssets] = useState(null);
    const [bodyColors, setBodyColors] = useState(null);
    const [bodyScales, setBodyScales] = useState(null);
    const [bodyRigType, setBodyRigType] = useState(null);
    const [avThumb, setAvThumb] = useState(null);
    
    const [isRendering, setIsRendering] = useState(false);
    const [avRules, setAvRules] = useState(false);
    const [loadingAvatar, setLoadingAvatar] = useState(true);
    const [canForce, setCanForce] = useState(true);
    
    // changed assetId is number of which asset id has been changed
    // denoted by whether its positive or negative integer
    const [modifiedAsset, setModifiedAsset] = useState(null);
    // the rest of these should correspond to the actual value name in the API to get avatars
    // so modified scaling would be { height: 0.5 }
    const [modifiedBC, setModifiedBC] = useState(null);
    const [modifiedScaling, setModifiedScaling] = useState(null);
    const [modifiedRigType, setModifiedRigType] = useState(null);
    
    const debo = useRef(false);

    //const feedback = FeedbackStore.useContainer();
    const auth = AuthenticationStore.useContainer();
    
    function AddAsset(asset) {
        setModifiedAsset(asset);
    }
    
    function RemoveAsset(asset) {
        setModifiedAsset({
            ...asset,
            assetId: asset.assetId * -1,
        });
    }
    
    async function ReloadAvatar(){
        setLoadingAvatar(true);
        setAvThumb(null);
        setAvRules(await getRules());
        let avatar = await getMyAvatar();
        setWearingAssets(avatar.assets.map(v => {
            return {
                name: v.name,
                assetId: v.id,
                assetType: v.assetType.id,
                assetTypeName: v.assetType.name,
            }
        }));
        setBodyColors(avatar.bodyColors);
        setBodyRigType(avatar.playerAvatarType);
        setBodyScales(avatar.scales);
        setLoadingAvatar(false);
        setIsRendering(true);
    }
    
    async function ForceRender() {
        if (!canForce) return;
        setCanForce(false);
        
        await redrawMyAvatar();
        setAvThumb(null);
        await wait(0.2);
        setIsRendering(true);
        await wait(3);
        setCanForce(true);
    }
    
    async function GetUpdatedAvatar() {
        if (isRendering) {
            while (isRendering) {
                await wait(1);
            }
        }
        setAvThumb(null);
        setIsRendering(true);
    }
    
    useEffect(ReloadAvatar, []);
    
    useEffect(async () => {
        if (debo.current || !isRendering || avThumb != null) return;
        debo.current = true;
        
        let stopwatch = new Stopwatch();
        stopwatch.Start();
        let attempts = 0;
        while (avThumb == null && attempts <= 10) {
            let thumbnail = await multiGetUserThumbnails({userIds: [auth.userId]})
                .then(result => result[0]);
            if (thumbnail.state === "Completed" && typeof thumbnail.imageUrl === "string") {
                setAvThumb(thumbnail.imageUrl);
                break;
            } else {
                console.warn("User thumbnail has not completed rendering yet.");
            }
            attempts++;
            await wait(1);
        }
        stopwatch.Stop();
        if (attempts > 10 && avThumb == null)
            //feedback.addFeedback("Could not get new avatar render. Please try again later.", FeedbackType.ERROR);
			console.error("Could not get new avatar render. Please try again later.");
        console.log(`Got avatar render in ${stopwatch.ElapsedMilliseconds()}ms, in ${attempts} attempts.`);
        
        setIsRendering(false);
        debo.current = false;
    }, [isRendering]);
    
    useEffect(() => {
        if (!modifiedScaling) return;
        
        const applyScaling = async () => {
            setBodyScales(prev => {
                const newScales = { ...prev, ...modifiedScaling };
                setModifiedScaling(null);
                (async () => {
                    await setScales(newScales);
                    await GetUpdatedAvatar();
                })();
                return newScales;
            });
        };
        
        applyScaling().then();
    }, [modifiedScaling]);
    useEffect(() => {
        if (!modifiedBC) return;
        
        const applyBC = async () => {
            setBodyColors(prev => {
                const newBC = { ...prev, ...modifiedBC };
                setModifiedBC(null);
                (async () => {
                    await setColors(newBC);
                    await GetUpdatedAvatar();
                })();
                return newBC;
            });
        };
        
        applyBC().then();
    }, [modifiedBC]);
    useEffect(() => {
        if (!modifiedRigType) return;
        let newRigType = modifiedRigType;
        setModifiedRigType(null);
        
        const applyRigType = async () => {
            setBodyRigType(newRigType);
            await setRigType(newRigType);
            await GetUpdatedAvatar();
        };
        
        applyRigType().then();
    }, [modifiedRigType]);
    useEffect(() => {
        if (!modifiedAsset || wearingAssets === null) return;
        /** @type SortedItem */
        let newAsset = modifiedAsset;
        setModifiedAsset(null);
        
        // Limiting
        if (!IsNegative(newAsset.assetId)) {
            if (AssetTypeCategory.LimitToOne.includes(newAsset.assetType)) {
                for (const assetType of AssetTypeCategory.LimitToOne) {
                    let onlyOneAllowed = wearingAssets.filter(/** @param {WearingAsset} asset */ asset => {
                        return asset.assetType === assetType && newAsset.assetType === assetType;
                    });
                    if (onlyOneAllowed.length > 0) {
                        setWearingAssets(wearingAssets.filter(asset => asset.assetType !== assetType));
                    }
                }
            } else if (AssetTypeCategory.Accessories.includes(newAsset.assetType)) {
                let accessories = wearingAssets.filter(/** @param {WearingAsset} asset */ asset =>
                    AssetTypeCategory.Accessories.includes(asset.assetType)
                );
                if (accessories.length > 15) {
                    //feedback.addFeedback("Too many accessories equipped", FeedbackType.ERROR);
					console.error("Too many accessories equipped");
                    return;
                }
            } else if (AssetTypeCategory.Emotes.includes(newAsset.assetType)) {
                let emotes = wearingAssets.filter(/** @param {WearingAsset} asset */ asset =>
                    AssetTypeCategory.Emotes.includes(asset.assetType)
                )
                if (emotes.length > 8) {
                    //feedback.addFeedback("Too many emotes equipped", FeedbackType.ERROR);
					console.error("Too many emotes equipped");
                    return;
                }
            }
        }
        
        setWearingAssets(prev => {
            let updated;
            if (IsNegative(newAsset.assetId)) {
                updated = prev.filter(v => v.assetId !== newAsset.assetId * -1);
            } else {
                updated = [...prev, newAsset];
            }
            
            (async () => {
                await AvatarService.setWearingAssets({ assetIds: updated.map(d => d.assetId) });
                await GetUpdatedAvatar();
            })();
            
            return updated;
        });
    }, [modifiedAsset]);
    
    useEffect(() => {
        return () => {
            setAvThumb({});
        };
    }, []);
    
    return {
        // Functions
        AddAsset,
        RemoveAsset,
        ForceRender,
        GetUpdatedAvatar,
        ReloadAvatar,
        
        // States (all United)
        wearingAssets,
        
        bodyColors,
        setBodyColors,
        
        bodyScales,
        setBodyScales,
        
        bodyRigType,
        setBodyRigType,
        
        setModifiedRigType,
        setModifiedBC,
        setModifiedScaling,
        
        avThumb,
        setAvThumb,
        
        loadingAvatar,
        setLoadingAvatar,
        
        isRendering,
        /**
         * @type AvatarRules
         */
        avRules,
    }
})

/**
 * @type {{Accessories: number[], Emotes: number[], LimitToOne: number[], Unlimited: number[], All: number[]}}
 */
export const AssetTypeCategory = {
    Accessories: [8, 41, 42, 43, 44, 45, 46, 47], // all limit of 15
    Emotes: [61], // all limit of 8
    LimitToOne: [50, 51, 52, 53, 54, 55, 17, 27, 28, 29, 30, 31, 18, 19, 12, 11, 2],
    Unlimited: [24],
    All: [50, 51, 52, 53, 54, 55, 17, 27, 28, 29, 30, 31, 32, 18, 19, 12, 11, 2, 24, 61, 8, 41, 42, 43, 44, 45, 46, 47]
}

export function IsNegative(int) {
    return int < 0;
}

/**
 * @typedef AABB
 * @property {number} x
 * @property {number} y
 * @property {number} z
 */

export default AvatarInfoStore;
