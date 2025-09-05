import {createUseStyles} from "react-jss";
import ActionButton from "../../actionButton";
import buttonStyles from "../../../styles/buttonStyles";
import {useEffect, useRef, useState} from "react";
import AvatarPageStore from "../stores/avatarPageStore";
import Link from "../../link";
import {ThumbnailFromState} from "./avatarCardList";
import AvatarInfoStore from "../stores/avatarInfoStore";
import {wearOutfit} from "../../../services/avatar";
import dayjs from "../../../lib/dayjs";
import AuthenticationStore from "../../../stores/authentication";
import DeleteOutfitModal from "./modals/deleteOutfit";
import UpdateOutfitModal from "./modals/updateOutfit";
import RenameOutfitModal from "./modals/renameOutfit";
import CreateOutfitModal from "./modals/createOutfit";

const useCardStyles = createUseStyles({
    avatarCardWrapper: {
        borderRadius: 3,
        display: "flex",
        flexDirection: "column",
        aspectRatio: "4 / 5",
        width: "calc(20% - 8px)",
        padding: 0,
        "@media(max-width: 767px)": {
            width: "calc(25% - 8px)",
        },
        "@media(max-width: 576px)": {
            width: "calc(33% - 8px)",
        },
    },
    avatarCardContainer: {
        width: 126,
        backgroundColor: "#fff",
        position: "relative",
        boxShadow: "0 1px 4px 0 rgba(25,25,25,0.3)",
        borderRadius: 3,
        maxWidth: 150,
        transition: "box-shadow 50ms ease",
        "-webkit-transition": "box-shadow 50ms ease",
        "&:hover": {
            boxShadow: "0 1px 6px 0 rgba(25,25,25,0.75)",
        },
        "@media(max-width: 992px)": {
            width: "100%",
            height: "100%",
            display: "flex",
            flexDirection: "column",
        }
    },
    avatarCardImage: {
        cursor: "pointer",
        width: "126px",
        height: "126px",
        borderTopLeftRadius: 3,
        borderTopRightRadius: 3,
        borderBottom: "1px solid #e3e3e3",
        "& img": {
            width: "100%",
            minHeight: "100%",
            height: "auto",
            borderTopLeftRadius: 3,
            borderTopRightRadius: 3,
        },
        "@media(max-width: 992px)": {
            width: "100%",
            aspectRatio: "1 / 1",
            height: "auto",
        }
    },
    avatarCardItemLinkContainer: {
        display: "flex",
        justifyContent: "space-between",
        padding: "6px 6px 0 6px",
        width: "100%",
        overflow: "hidden",
        "@media(max-width: 992px)": {
            padding: "0 6px",
        },
    },
    avatarCardItemLink: {
        lineHeight: "16px",
        flex: 1,
        display: "inline-block",
        width: "90%",
        "& span": {
            height: "20px",
            lineHeight: "16px",
            display: "inline-block",
            maxWidth: '100%',
            fontSize: 16,
            padding: 0,
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
    settingsBtnContainer: {
        display: "flex",
        height: "100%",
        justifyContent: "center",
        alignItems: "center",
        marginTop: 1,
    },
    settingsContainer: {
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        width: "100%",
        height: "100%",
        zIndex: 3,
        display: "none",
        backgroundColor: "rgba(255,255,255,0.9)",
        position: "absolute",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        padding: 6,
    },
    settingBtnStyle: {
        width: "calc(100% - 12px)",
        margin: 0,
        cursor: "pointer",
        userSelect: "none",
        "-webkit-transition": "all 200ms ease-in-out",
        transition: "all 200ms ease-in-out",
        backgroundColor: "transparent",
        border: "1px solid transparent",
        color: "#191919",
        outline: "none",
        fontWeight: 400,
        textAlign: "center",
        whiteSpace: "nowrap",
        verticalAlign: "middle",
        fontSize: "14px!important",
        padding: 4,
        borderRadius: 3,
        lineHeight: "100%!important",
        "&:hover": {
            backgroundColor: "#32B5FF",
            borderColor: "#32B5FF",
            color: "#fff",
            boxShadow: "0 1px 3px rgba(150,150,150,0.74)",
        }
    },
    iconSettings: {
        "@media(max-width: 992px)": {
        }
    },
});

/**
 * @param {{ outfit: SortedOutfit; setDeleteOutfitModal: (number) => {}; setRenameOutfitModal: (number) => {}; setUpdateOutfitModal: (number) => {}; }} props
 * @returns {JSX.Element}
 * @constructor
 */
function OutfitCard({outfit, setDeleteOutfitModal, setRenameOutfitModal, setUpdateOutfitModal}) {
    const s = useCardStyles();
    const store = AvatarInfoStore.useContainer();
    const [openSettings, setOpenSettings] = useState(false);
    const auth = AuthenticationStore.useContainer();
    
    const settings = [
        {
            name: "Update",
            onClick: () => {
                setUpdateOutfitModal(outfit.outfitId);
            },
        },
        {
            name: "Rename",
            onClick: () => {
                setRenameOutfitModal(outfit.outfitId);
            },
        },
        {
            name: "Download Image",
            onClick: () => {
                if (!outfit.thumbnail) {
                    console.error("Failed to download outfit thumbnail");
                    return;
                }
                const a = document.createElement("a");
                a.href = outfit.thumbnail;
                a.download = `${auth.username}'s Outfit: ${outfit.name}.png`
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
            },
        },
        {
            name: "Delete",
            onClick: () => {
                setDeleteOutfitModal(outfit.outfitId);
            },
        },
        {
            name: "Cancel",
            onClick: () => {
            },
        },
    ]
    
    return <div className={`${s.avatarCardWrapper}`}>
        <div className={`${s.avatarCardContainer}`}>
            <div className={s.avatarCardImage} onClick={async () => {
                await wearOutfit({outfitId: outfit.outfitId});
                await store.ReloadAvatar();
            }}>
                <img src={ThumbnailFromState(outfit.thumbnail, outfit.thumbnailState)} alt={outfit.name}/>
            </div>
            <div className={s.avatarCardItemLinkContainer}>
                <Link href="#">
                    <a className={s.avatarCardItemLink}
                       href="#">
                        <span className='text-overflow'>{outfit.name}</span>
                    </a>
                </Link>
                <div className={s.settingsBtnContainer}>
                                <span
                                    onClick={() => setOpenSettings(true)}
                                    className={`icon-settings-16x16 ${s.iconSettings}`}
                                    style={{cursor: "pointer"}}
                                />
                </div>
            </div>
            <div className={s.settingsContainer} style={openSettings ? {display: "flex"} : null}>
                {
                    settings.map(setting => {
                        return <button
                            className={s.settingBtnStyle}
                            onClick={e => {
                                setOpenSettings(false);
                                setting.onClick(e);
                            }}
                        >
                            {setting.name}
                        </button>
                    })
                }
                <span style={{marginTop: 3, fontSize: 10}}>{dayjs(outfit.createdAt).format('MM/D/YYYY')}</span>
            </div>
        </div>
    </div>
}

const useStyles = createUseStyles({
    outfitsWrapper: {
        margin: "9px 0 6px 0",
        "& *:not(h5)": {
            fontWeight: 300,
            fontSize: 16,
            lineHeight: "1.3em",
        }
    },
    outfitsHeader: {
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        marginTop: 12,
        marginBottom: 9,
    },
    outfitsContainer: {
        display: "flex",
        width: "100%",
        flexWrap: "wrap",
        position: "relative",
        gap: 10,
    },
});

function OutfitsTab() {
    const s = useStyles();
    const btnStyles = buttonStyles();
    const [loadingOutfits, setLoadingOutfits] = useState(true);
    const deb = useRef(false);
    const page = AvatarPageStore.useContainer();
    const store = AvatarInfoStore.useContainer();
    
    // Modals
    const [createOutfitModal, setCreateOutfitModal] = useState(false);
    const [deleteOutfitModal, setDeleteOutfitModal] = useState(false);
    const [updateOutfitModal, setUpdateOutfitModal] = useState(false);
    const [renameOutfitModal, setRenameOutfitModal] = useState(false);
    
    
    useEffect(async () => {
        if (deb.current) return;
        deb.current = true;
        store.setLoadingAvatar(false);
        if (page.outfits.length === 0) {
            setLoadingOutfits(true);
            await page.LoadOutfits();
            setLoadingOutfits(false);
        }
        deb.current = false;
    }, []);
    
    return <div className={s.outfitsWrapper}>
        {deleteOutfitModal ? <DeleteOutfitModal outfitId={deleteOutfitModal} openModal={setDeleteOutfitModal}/> : null}
        {updateOutfitModal ? <UpdateOutfitModal outfitId={updateOutfitModal} openModal={setUpdateOutfitModal}/> : null}
        {renameOutfitModal ? <RenameOutfitModal outfitId={renameOutfitModal} openModal={setRenameOutfitModal}/> : null}
        {createOutfitModal ? <CreateOutfitModal outfitId={createOutfitModal} openModal={setCreateOutfitModal}/> : null}
        <div className={s.outfitsHeader}>
            <span>Outfits</span>
            <ActionButton
                label="Create New Outfit"
                buttonStyle={btnStyles.newContinueButton}
                onClick={() => setCreateOutfitModal(true)}
            />
        </div>
        <div className={s.outfitsContainer}>
            {
                page.outfits.map(outfit => {
                    return <OutfitCard
                        outfit={outfit}
                        setDeleteOutfitModal={setDeleteOutfitModal}
                        setUpdateOutfitModal={setUpdateOutfitModal}
                        setRenameOutfitModal={setRenameOutfitModal}
                    />
                })
            }
            {
                !loadingOutfits && page.outfits.length === 0 && <span className={`section-content-off w-100`}>
                You don't have any outfits. Try creating some!
            </span>
            }
            {
                loadingOutfits &&
                <span className="spinner position-absolute" style={{height: "36px", backgroundSize: "auto 36px"}}/>
            }
        </div>
    </div>
}

export default OutfitsTab;
