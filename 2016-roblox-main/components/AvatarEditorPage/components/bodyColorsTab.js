import {createUseStyles} from "react-jss";
import AvatarInfoStore from "../stores/avatarInfoStore";
import AvatarPageStore from "../stores/avatarPageStore";
import {useState} from "react";
import AdvancedBodyColorsModal, {options} from "./modals/advancedBodyColors";

const useStyles = createUseStyles({
    skinTonesWrapper: {
        margin: "9px 0 6px 0",
        "& *:not(h5)": {
            fontWeight: 300,
            fontSize: 16,
            lineHeight: "1.3em",
        }
    },
    skinTonesHeader: {
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        marginTop: 12,
        marginBottom: 9,
    },
    skinTonesContainer: {
        display: "flex",
        width: "100%",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
    },
    skinToneBtn: {
        "-webkit-transition": "all 200ms ease",
        transition: "all 200ms ease",
        margin: 0,
        display: "inline-block",
        aspectRatio: "1 / 1",
        borderRadius: "50%",
        cursor: "pointer",
        flex: "0 0 calc(20% - 24px)",
        position: "relative",
        "&:last-child": {
            boxShadow: "0 0 0 1px #B8B8B8",
        },
        "&:hover": {
            boxShadow: "0 1px 6px 0 rgba(25,25,25,0.75)",
        },
        "&.active::after": {
            position: "absolute",
            content: '""',
            top: -4,
            left: -4,
            width: "112%",
            aspectRatio: "1 / 1",
            border: "2px solid var(--primary-color)",
            borderRadius: "50%",
        },
    },
    skinTones: {
        display: "flex",
        flexWrap: "wrap",
        width: "75%",
        gap: 30,
        margin: "15px 0",
    },
    advancedLink: {
        width: "100%",
        display: "inline-block",
        userSelect: "none",
        textAlign: "right",
    },
});

function BodyColorsTab() {
    const s = useStyles();
    const store = AvatarInfoStore.useContainer();
    const page = AvatarPageStore.useContainer();
    const [advancedColors, setAdvancedColors] = useState(false);
    
    /**
     * @param {BodyColor} skinTone
     */
    function skinToneBtnFunc(skinTone) {
        let newBC = Object.fromEntries(Object.keys(store.bodyColors).map(key => [key, skinTone.brickColorId]));
        store.setModifiedBC(newBC);
    }
    
    return <div className={s.skinTonesWrapper}>
        { advancedColors ? <AdvancedBodyColorsModal openModal={setAdvancedColors} /> : null }
        <div className={s.skinTonesHeader}>
            <span>Body {'>'} Skin Tone</span>
        </div>
        <div className={`${s.skinTonesContainer} section-content`}>
            <div className={s.skinTones}>
                {
                    store.avRules.basicBodyColorsPalette.map(color => {
                        let selected = options.filter(d => d.id !== "all")
                            .map(id => id.id).every(d => store.bodyColors[d] === color.brickColorId);
                        return <span
                            className={`${s.skinToneBtn} ${selected ? "active" : ""}`}
                            style={{backgroundColor: color.hexColor}}
                            onClick={() => skinToneBtnFunc(color)}
                        />
                    })
                }
            </div>
            <a
                className={`link2018 ${s.advancedLink}`}
                onClick={() => setAdvancedColors(true)}
            >Advanced</a>
        </div>
    </div>
}

export default BodyColorsTab;
