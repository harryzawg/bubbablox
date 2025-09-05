import {createUseStyles} from "react-jss";
import Link from "../../link";
import AvatarPageStore from "../stores/avatarPageStore";
import AvatarInfoStore from "../stores/avatarInfoStore";
import ActionButton from "../../actionButton";
import React, {useEffect, useRef, useState} from "react";
import {wait} from "../../../lib/utils";
import buttonStyles from "../../../styles/buttonStyles";
import useButtonStyles from "../../../styles/buttonStyles";

const useAvCardStyles = createUseStyles({
	avatarCardWrapper: {
		borderRadius: 3,
		display: "flex",
		flexDirection: "column",
		width: "100%",
	},
    avatarCardContainer: {
        width: "100%",
        //height: "100%",
        flexDirection: "column",
        display: "flex",
        backgroundColor: "#fff",
        position: "relative",
        boxShadow: "0 1px 4px 0 rgba(25,25,25,0.3)",
        borderRadius: 3,
        //maxWidth: 150,
        transition: "box-shadow 200ms ease",
        "-webkit-transition": "box-shadow 200ms ease",
        "&:hover": {
            boxShadow: "0 1px 6px 0 rgba(25,25,25,0.75)",
        }
    },
    avatarCardImage: {
        cursor: "pointer",
        width: "100%",
        aspectRatio: "1 / 1",
        borderTopLeftRadius: 3,
        borderTopRightRadius: 3,
        borderBottom: "1px solid #e3e3e3",
        position: "relative",
        "& img": {
			width: "100%",
			height: "100%",
			objectFit: "cover",
            borderTopLeftRadius: 3,
            borderTopRightRadius: 3,
            minWidth: "85px",
        }
    },
    avatarCardItemLink: {
        lineHeight: "16px",
        width: "100%",
        padding: "0 6px",
        display: "inline-block",
        "& span": {
            height: "20px",
            lineHeight: "16px",
            display: "inline-block",
            maxWidth: '100%',
            fontSize: 16,
            padding: 0,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: 'nowrap',
        },
        "@media(min-width: 992px)": {
            paddingTop: 6,
        }
    },
    avatarCardEquipped: {
        borderRadius: 3,
        pointerEvents: "none",
        border: "2px solid #02b757",
        position: "absolute",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        "& span": {
            width: 0,
            height: 0,
            borderTop: "36px solid #02b757",
            borderLeft: "36px solid transparent",
            position: "absolute",
            top: 0,
            right: 0,
        }
    },
    restrictionsContainer: {
        position: 'absolute',
        bottom: 0,
        left: -2,
        overflow: 'hidden',
    },
});

export function ThumbnailFromState(thumbnail, state) {
    if (state) state = state.toLowerCase();
    switch (state) {
        case "pending":
            return "/img/placeholder.png";
        case "blocked":
            return "/img/blocked.png";
        case "completed":
            return thumbnail;
        default:
            return "/img/error.png";
    }
}

/**
 * @param {SortedItem} asset
 * @param {boolean} equipped
 * @returns {JSX.Element}
 * @constructor
 */
function AvatarCard({asset, equipped}) {
    const s = useAvCardStyles();
    const store = AvatarInfoStore.useContainer();
    
    return <div className={`${s.avatarCardWrapper}`}>
        <div className={`${s.avatarCardContainer}`}>
			<div className={s.avatarCardImage} onClick={() => {
				if (store.isRendering) return;
				if (equipped) {
					store.RemoveAsset(asset);
				} else {
					store.AddAsset(asset);
				}
			}}>
                <img src={ThumbnailFromState(asset.thumbnail, asset.thumbnailState)} alt={asset.name} style={{ filter: store.isRendering ? "grayscale(100%) opacity(0.5)" : "none", pointerEvents: store.isRendering ? "none" : "auto" }}/>
                <div className={s.restrictionsContainer}>
                    {
                        asset?.isLimitedUnique
                        ?
                        <span className="icon-labels-18 LimitedUnique"/>
                        :
                        asset?.isLimited
                        ?
                        <span className="icon-labels-18 Limited"/>
                        :
                        null
                    }
                </div>
            </div>
            <Link href={`/catalog/${asset.assetId}/${encodeURIComponent(asset.name)}`}>
                <a className={s.avatarCardItemLink}
                   href={`/catalog/${asset.assetId}/${encodeURIComponent(asset.name)}`}>
                    <span className='text-overflow'>{asset.name}</span>
                </a>
            </Link>
            {
                equipped ? <div className={s.avatarCardEquipped}>
                    <span/>
                </div> : null
            }
        </div>
    </div>
}

const useStyles = createUseStyles({
    btnContainer: {
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        width: "100%",
    },
    loadMoreBtn: {
        padding: 4,
        fontSize: 14,
        lineHeight: "100%",
    },
    avatarListContainer: {
        display: "grid",
        gap: 10,
        position: "relative",
        gridTemplateColumns: "repeat(auto-fill, minmax(120px, 1fr))",
        "@media(max-width: 767px)": {
            gridTemplateColumns: "repeat(auto-fill, minmax(100px, 1fr))",
        },
        "@media(max-width: 576px)": {
            gridTemplateColumns: "repeat(auto-fill, minmax(90px, 1fr))",
        },
    },
});

function AvatarCardList() {
    const s = useStyles();
    const page = AvatarPageStore.useContainer();
    const store = AvatarInfoStore.useContainer();
    const deb = useRef(false);
    const [wearingAssets, setWearingAssets] = useState(null);
    const buttonStyles = useButtonStyles();
    
    useEffect(() => {
        setWearingAssets(store.wearingAssets);
    }, [store.wearingAssets]);
    
    return <div className={s.avatarListContainer}>
        {
            store.loadingAvatar &&
            <span className="spinner position-absolute" style={{height: "36px", backgroundSize: "auto 36px"}}/>
        }
        {
            page.listItems.map(item => {
                const isEquipped = wearingAssets?.length && wearingAssets.map(d => d.assetId).includes(item.assetId);
                return <AvatarCard asset={item} equipped={isEquipped}/>
            })
        }
        {
            !store.loadingAvatar && page.listItems.length === 0 && <span className={`section-content-off w-100`}>
                You do not have any items here.
            </span>
        }
        {
            page?.listItemMetadata?.nextPageCursor && page?.listItemMetadata?.assetType &&
            <div className={s.btnContainer}>
                <ActionButton
                    label="Load More"
                    onClick={() => page.LoadAssetTypeToList(page.listItemMetadata.assetType, true)}
                    buttonStyle={buttonStyles.newCancelButton}
                    className={s.loadMoreBtn}
                />
            </div>
        }
    </div>
}

export default AvatarCardList;
