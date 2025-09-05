import { createUseStyles } from "react-jss";
import AvatarInfoStore from "./stores/avatarInfoStore";
import AuthenticationStore from "../../stores/authentication";
import ActionButton from "../actionButton";
import useButtonStyles from "../../styles/buttonStyles";
import AvatarCardList from "./components/avatarCardList";
import RadioPill from "../radioPill";
import Slider from "../slider";
import AvatarTabSubmenu, { SUBMENU_MODE } from "./components/avatarTabSubmenu";
import AvatarPageStore from "./stores/avatarPageStore";
import AvatarTabs from "./components/avatarTabs";
import { IsNullOrEmpty, wait } from "../../lib/utils";
import { useEffect, useRef, useState } from "react";
import OutfitsTab from "./components/outfitsTab";
import BodyColorsTab from "./components/bodyColorsTab";
import { useRouter } from "next/router";

const useStyles = createUseStyles({
    sliderInput: {
        width: "100%",
    },
    avatarHeader: {
        "@media(max-width: 576px)": {
            flexDirection: "column",
            marginBottom: 15,
        },
    },
    avatarHeaderText: {
        fontSize: 36,
        fontWeight: 500,
        padding: "15px 0",
        margin: 0,
        "@media(max-width: 576px)": {
            padding: "0 0 10px 0",
            fontSize: 48,
        },
    },
    avatarThumbContainer: {
        position: "relative",
        backgroundImage: "url(/img/avatar-background.svg)",
        backgroundSize: "352px 352px",
        overflow: "hidden",
        height: 352,
        width: 277,
        "& img, & canvas": {
            width: 352,
            height: "100%",
            verticalAlign: "middle",
            opacity: 1,
            transition: "opacity .5s ease",
            position: "absolute",
            top: 18,
            right: "-37.5px",
            userSelect: "none",
        },
        "& canvas": {
            top: "0!important",
        },
    },
    scalingContainer: {
        padding: 15,
        paddingTop: 0,
        "@media(max-width: 720px)": {
            flex: 1,
            paddingBottom: 0,
        },
    },
    scalingContainerDesktop: {
        "@media(max-width: 576px)": {
            display: "none",
        }
    },
    scalingContainerMobile: {
        display: "none",
        "@media(max-width: 576px)": {
            display: "block",
        }
    },
    scalingHeaderContainer: {
        textAlign: 'start',
        fontSize: 21,
        padding: "15px 0px 13px 0",
        margin: 0,
        "@media(max-width: 720px)": {
            padding: "10px 0 8px 0"
        },
    },
    scalingHeader: {
        margin: 0,
    },
    contentContainer: {
        padding: 0,
        "@media(max-width: 720px)": {
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
        },
        "@media(max-width: 576px)": {
            justifyContent: "center",
        }
    },
    avatarRigTypeSelector: {
        position: "absolute",
        right: 10,
        top: 10,
    },
    itemContainer: {
        flex: 1,
    },
    moreBut: {
        padding: 9,
        fontSize: 18,
        margin: 0,
		backgroundColor: '#02b757',
		color: '#fff',
    },
    iconDown: {
        backgroundPosition: "0 -204px",
        width: 12,
        height: 12,
        backgroundSize: "24px auto",
        bottom: "2px",
        position: "relative",
        marginLeft: "6px",
        "@media(max-width: 992px)": {
            backgroundPosition: "0 -169px",
            width: 10,
            height: 10,
            backgroundSize: "20px auto",
        },
        "@media(max-width: 767px)": {
            display: "none",
        }
    },
    redrawContainer: {
        "& span": {
            fontSize: 16,
        }
    },
    redrawBtn: {
		color: '#fff',
        backgroundColor: '#6c6c6c',
        padding: 4,
        fontSize: 14,
        lineHeight: "100%",
    },
    idekbuh: {
        "@media(max-width: 720px)": {
            width: "100%",
        }
    },
    content: {
        gap: 15,
        "@media(max-width: 720px)": {
            flexDirection: "column",
        }
    },
    firstRedraw: {
        marginBottom: 18,
        "@media(max-width: 720px)": {
            marginBottom: 8,
        }
    },
	dropdownItem: {
        '&:hover': {
            backgroundColor: '#6c757d !important',
            color: 'white !important',
        }
    },
});

// REF: https://youtu.be/iXI3aut2UWs
// REF2: https://youtu.be/pF-jlI9OJGs

function AvatarEditor() {
    const s = useStyles();
    const buttonStyles = useButtonStyles();
    const auth = AuthenticationStore.useContainer();
    const store = AvatarInfoStore.useContainer();
    const page = AvatarPageStore.useContainer();
    const { activeTab, setActiveTab } = page;
    const listItemMetadata = useRef(page.listItemMetadata);
    const debounce = useRef(false);
    const [avThumb, setAvThumb] = useState(null);
    const [isRendering, setIsRendering] = useState(false);
    
    /** @type RefObject<HTMLElement> */
    const canvasParentRef = useRef(null);
    
    useEffect(() => {
        listItemMetadata.current = page.listItemMetadata;
    }, [page.listItemMetadata]); // keeps listItemMetadata fresh for below functions
    // (because apparently that doesnt happen by default?? i have no clue)
    
    /**
     * @param {SubmenuData} item
     * @param {FormEvent<HTMLDivElement>} e
     * @constructor
     */
    async function AssetTypeClick(item, e) {
        if (debounce.current || listItemMetadata.current.assetType === item.typeId) return;
        debounce.current = true;
        page.setSelectedList({
            tab: item.tabId,
            subTab: item.name,
        });
        if (!item.tabType) {
            setActiveTab(0);
            await page.LoadAssetTypeToList(item.typeId);
        } else {
            setActiveTab(item.tabType);
        }
        await wait(0.5);
        debounce.current = false;
    }
    
    /**
     * @param {SubmenuData} item
     * @param {FormEvent<HTMLDivElement>} e
     * @constructor
     */
    async function RecentClick(item, e) {
        // DO NOT PUT === HERE!!
        if (debounce.current || listItemMetadata.current.recentType == item.typeId) return;
        debounce.current = true;
        page.setSelectedList({
            tab: item.tabId,
            subTab: item.name,
        });
        if (!item.tabType) {
            setActiveTab(0);
            await page.LoadRecentItemsToList(item.typeId);
        } else {
            setActiveTab(item.tabType);
        }
        await wait(0.5);
        debounce.current = false;
    }
    
    useEffect(async () => {
        if (debounce.current) {
            await wait(3);
            if (debounce.current) debounce.current = false;
        }
    }, [debounce]);
    
    useEffect(() => {
        setAvThumb(store.avThumb)
    }, [store.avThumb]);
    
    useEffect(() => {
        setIsRendering(store.isRendering);
    }, [store.isRendering]);
    
    return <div>
        <div className={`${s.avatarHeader} flex justify-content-between align-items-center`}>
            <h1 className={s.avatarHeaderText}>Avatar Editor</h1>
            <div className="flex justify-content-center align-items-center" style={{ gap: 12 }}>
                <span>Explore the catalog to find more clothes!</span>
                <ActionButton label="Get More" className={s.moreBut} buttonStyle={buttonStyles.newBuyButton}
                              onClick={() => {
                                  const router = useRouter();
                                  router.push("/catalog").then();
                              }}/>
            </div>
        </div>
        <div className={`flex ${s.content}`}>
            <div className={s.idekbuh}>
                <div className={`section-content ${s.contentContainer}`}>
                    <div className={s.avatarThumbContainer}>
                        {
                            avThumb && page.thumbnailType !== 1 ?
                            <img src={avThumb} alt={`${auth.username}'s Avatar`}/>
                            :
                            store.avThumb3D && is3DReady ?
                            null
                            :
                            <span className="spinner" style={{ height: "100%", backgroundSize: "auto 36px" }}/>
                        }
                        <div className={s.avatarRigTypeSelector}>
                            <RadioPill options={[
                                "R6",
                                "R15"
                            ]} selected={store?.bodyRigType} setSelected={store?.setModifiedRigType}/>
                        </div>
                    </div>
                    <div className={`${s.scalingContainer} ${s.scalingContainerDesktop}`}>
                        <div className={s.scalingHeaderContainer}>
                            <h1 className={s.scalingHeader}>Scaling</h1>
                        </div>
                        <div>
                            {
                                store?.avRules && store?.bodyScales &&
                                Object.entries(store.avRules.scales).map(([key, value]) => (
                                    <>
                                        <div
                                            style={{ color: store.bodyRigType === "R6" ? "#b8b8b8" : "var(--text-color-primary)" }}
                                            className="flex justify-content-between">
                                            <span style={{ color: 'inherit' }}>{CapitalizeVariable(key)}</span>
                                            <span>{Math.round(store.bodyScales[key] * 100)}%</span>
                                        </div>
										<Slider
											className={s.sliderInput}
											min={value.min}
											max={value.max}
											step={value.increment * 5}
											value={store.bodyScales[key]}
											setValue={(val) => {
												if (isRendering) return;
												store.setBodyScales(prev => ({
													...prev,
													[key]: Number(val.target.value)
												}));
											}}
											changeValue={(val) => {
												if (isRendering) return;
												store.setModifiedScaling({
													[key]: Number(val.target.value),
												});
											}}
											disabled={store.bodyRigType === "R6" || isRendering}
										/>
                                    </>
                                ))
                            }
                        </div>
                    </div>
                </div>
                <div className={`flex justify-content-between ${s.redrawContainer} ${s.firstRedraw}`}>
                    <span>Avatar isn't updated?</span>
                    <ActionButton onClick={async () => {
                        await store.GetUpdatedAvatar();
                    }} label="Refetch" buttonStyle={buttonStyles.newCancelButton} className={s.redrawBtn}/>
                </div>
                <div className={`flex justify-content-between ${s.redrawContainer}`}>
                    <span>Avatar isn't loading correctly?</span>
                    <ActionButton onClick={async () => {
                        await store.ForceRender();
                    }} label="Redraw" buttonStyle={buttonStyles.newCancelButton} className={s.redrawBtn}/>
                </div>
            </div>
            <div className={s.itemContainer}>
                <AvatarTabs
                    options={[
                        {
                            id: "recent",
                            name: <span>Recent <span
                                className={`icon-down ${s.iconDown}`}/></span>,
                            element: <AvatarTabSubmenu
                                data={[
                                    {
                                        tabId: "recent",
                                        name: "All",
                                        typeId: "all",
                                    }
                                    // TODO: implement recent outfits {
                                    //     name: "Outfits",
                                    //     typeId: "outfits",
                                    // },
                                ]}
                                onButtonClick={async (item, e) => await RecentClick(item, e)}
                            />
                        },
                        {
                            id: "clothing",
                            name: <span>Clothing <span
                                className={`icon-down ${s.iconDown}`}/></span>,
                            element: <AvatarTabSubmenu
                                data={[
                                    {
                                        label: "Accessories",
                                        items: [
                                            {
                                                name: "Hat",
                                                tabId: "clothing",
                                                typeId: 8,
                                            },
                                            {
                                                name: "Hair",
                                                tabId: "clothing",
                                                typeId: 41,
                                            },
                                            {
                                                name: "Face",
                                                tabId: "clothing",
                                                typeId: 42,
                                            },
                                            {
                                                name: "Neck",
                                                tabId: "clothing",
                                                typeId: 43,
                                            },
                                            {
                                                name: "Shoulders",
                                                tabId: "clothing",
                                                typeId: 44,
                                            },
                                            {
                                                name: "Front",
                                                tabId: "clothing",
                                                typeId: 45,
                                            },
                                            {
                                                name: "Back",
                                                tabId: "clothing",
                                                typeId: 46,
                                            },
                                            {
                                                name: "Waist",
                                                tabId: "clothing",
                                                typeId: 47,
                                            },
                                        ],
                                    },
                                    {
                                        label: "Clothes",
                                        items: [
                                            {
                                                name: "Shirts",
                                                typeId: 11,
                                                tabId: "clothing",
                                            },
                                            {
                                                name: "Pants",
                                                typeId: 12,
                                                tabId: "clothing",
                                            },
                                            {
                                                name: "T-Shirts",
                                                typeId: 2,
                                                tabId: "clothing",
                                            },
                                        ],
                                    },
                                    {
                                        label: "Gear",
                                        items: [{ name: "Gear", typeId: 19, tabId: "clothing" }],
                                    },
                                ]}
                                onButtonClick={async (item, e) => await AssetTypeClick(item, e)}
                                mode={SUBMENU_MODE.NESTED}
                            />
                        },
                        {
                            id: "body",
                            name: <span>Body <span
                                className={`icon-down ${s.iconDown}`}/></span>,
                            element: <AvatarTabSubmenu
                                data={[
                                    {
                                        name: "Skin Tone",
                                        typeId: 0,
                                        tabType: 2,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Packages",
                                        typeId: 32,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Face",
                                        typeId: 18,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Head",
                                        typeId: 17,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Torso",
                                        typeId: 27,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Left Arms",
                                        typeId: 29,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Right Arms",
                                        typeId: 28,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Left Legs",
                                        typeId: 30,
                                        tabId: "body",
                                    },
                                    {
                                        name: "Right Legs",
                                        typeId: 31,
                                        tabId: "body",
                                    },
                                ]}
                                onButtonClick={async (item, e) => await AssetTypeClick(item, e)}
                            />
                        },
                        {
                            id: "animations",
                            name: <span>Animations <span className={`icon-down ${s.iconDown}`}/></span>,
                            element: <AvatarTabSubmenu
                                data={[
                                    {
                                        name: "Walk",
                                        typeId: 55,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Run",
                                        typeId: 53,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Fall",
                                        typeId: 50,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Jump",
                                        typeId: 52,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Swim",
                                        typeId: 54,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Climb",
                                        typeId: 48,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Idle",
                                        typeId: 51,
                                        tabId: "animations",
                                    },
                                    {
                                        name: "Emotes",
                                        typeId: 61,
                                        tabId: "animations",
                                    },
                                ]}
                                onButtonClick={async (item, e) => await AssetTypeClick(item, e)}
                            />
                        },
                        {
                            id: "outfits",
                            name: <span>Outfits</span>,
                            element: null,
                            onClick: () => {
                                page.setOutfits([]);
                                page.setSelectedList({
                                    tab: "outfits",
                                    subTab: "fsgvbjfsgbjsdgjajsndmadkgil",
                                });
                                page.ClearListItems();
                            },
                            tabType: 1,
                        },
                    ]}
                    setTabType={setActiveTab}
                    default={<span>Recent <span className={`icon-down ${s.iconDown}`}/></span>}
                    tabType={activeTab}
                />
                {
                    (() => {
                        switch (activeTab) {
                            case 1:
                                return <OutfitsTab/>
                            case 2:
                                return <BodyColorsTab/>
                            default:
                                return <div>
                                    <div style={{ display: "flex" }}>
                                        <span
                                            style={{ paddingTop: 9, paddingBottom: 4 }}
                                        >{CapitalizeVariable(page.selectedList.tab)}
                                            {!IsNullOrEmpty(page?.selectedList?.subTab) && ` > ${CapitalizeVariable(page?.selectedList?.subTab)}`}
                                        </span>
                                    </div>
                                    <AvatarCardList/>
                                </div>;
                        }
                    })()
                }
            </div>
            {
                /** this had to be copy and pasted twice for mobile support but its SO ugly */
            }
            <div className={`section-content ${s.scalingContainerMobile}`} style={{ padding: 0 }}>
                <div className={`${s.scalingContainer}`}>
                    <div className={s.scalingHeaderContainer}>
                        <h1 className={s.scalingHeader}>Scaling</h1>
                    </div>
                    <div>
                        {
                            store?.avRules && store?.bodyScales &&
                            Object.entries(store.avRules.scales).map(([key, value]) => (
                                <>
                                    <div
                                        style={{ color: store.bodyRigType === "R6" ? "#b8b8b8" : "var(--text-color-primary)" }}
                                        className="flex justify-content-between">
                                        <span style={{ color: 'inherit' }}>{CapitalizeVariable(key)}</span>
                                        <span>{Math.round(store.bodyScales[key] * 100)}%</span>
                                    </div>
                                    <Slider
                                        className={s.sliderInput}
                                        min={value.min}
                                        max={value.max}
                                        step={value.increment * 5}
                                        value={store.bodyScales[key]}
                                        setValue={(val) => {
                                            store.setBodyScales(prev => ({ ...prev, [key]: Number(val.target.value) }));
                                        }}
                                        changeValue={(val) => {
                                            store.setModifiedScaling({
                                                [key]: Number(val.target.value),
                                            });
                                        }}
                                        disabled={store.bodyRigType === "R6"}
                                    />
                                </>
                            ))
                        }
                    </div>
                </div>
            </div>
        </div>
    </div>
}

export function CapitalizeVariable(str) {
    if (!str) return str;
    return str
        .replace(/([A-Z])/g, " $1")
        .replace(/^./, str => str?.toUpperCase());
}

export default AvatarEditor;
