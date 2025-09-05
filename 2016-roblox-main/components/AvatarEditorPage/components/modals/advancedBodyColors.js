import {createUseStyles} from "react-jss";
import NewModal from "../../../newModal";
import ActionButton from "../../../actionButton";
import buttonStyles from "../../../../styles/buttonStyles";
import {updateOutfit} from "../../../../services/avatar";
import AvatarPageStore from "../../stores/avatarPageStore";
import AvatarInfoStore from "../../stores/avatarInfoStore";
import {useState} from "react";

const useStyles = createUseStyles({
    footerClass: {
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
		backgroundColor: "#fff",
    },
    modalBtn: {
        padding: 9,
        margin: "0 5px",
        width: 90,
        fontSize: "18px!important",
        fontWeight: "400!important",
        lineHeight: "100%!important",
		backgroundColor: "#007bff",
		"&:hover": {
            backgroundColor: "#0056b3",
        },
    },
    containerClass: {
		backgroundColor: "#fff",
		color: "#000",
        "& h5": {
            fontSize: 18,
            fontWeight: 400,
            lineHeight: "1em",
        }
    },
    optionsContainer: {
		color: "#000",
    },
    optionContainer: {
        margin: "6px 0",
        display: "flex",
        alignItems: "center",
        gap: 5,
        position: "relative",
        //paddingLeft: 15,
        "& *": {
            lineHeight: "100%",
			color: "#000",
        }
    },
    optionInput: {
        width: 16,
        height: 16,
        border: "2px solid #b8b8b8",
        backgroundColor: "#fff",
        borderRadius: "50%",
        cursor: "pointer",
        appearance: "none",
        outline: "none",
        "&:hover": {
            borderColor: "#007bff",
        },
        "&:checked+label::before": {
            display: "inline-block",
            content: '""',
        },
        "&:checked+label::after": {
            backgroundColor: "var(--primary-color)",
            top: "20%",
            left: "3%",
            borderRadius: "50%",
            width: 10,
            height: 10,
            content: '""',
            display: "inline-block",
            position: "absolute",
        },
		"&:checked": {
			borderColor: "#007bff",
			backgroundColor: "#007bff",
		},
    },
    optionLabel: {
        fontWeight: "400!important",
        lineHeight: "100%!important",
    },
    
    colorContainer: {
        marginLeft: 15,
        display: "flex",
        flexWrap: "wrap",
        flex: 1,
    },
    
    bodyColor: {
        margin: 6,
        // width: "14.29%",
        width: "10%",
        aspectRatio: "1 / 1",
        transition: "all 200ms ease",
        "-webkit-transition": "all 200ms ease",
        borderRadius: "50%",
        cursor: "pointer",
        position: "relative",
        "&:hover": {
            boxShadow: "0 1px 6px 0 rgba(25,25,25,0.75)",
        },
        "&.selected::after": {
            width: "130%",
            aspectRatio: "1 / 1",
            content: '""',
            position: "absolute",
            left: -4,
            top: -4,
            border: "2px solid #007bff",
            borderRadius: "50%",
        }
    },
});

/**
 * @param {(boolean) => {}} openModal
 * @returns {JSX.Element}
 * @constructor
 */
const AdvancedBodyColorsModal = ({ openModal }) => {
    const s = useStyles();
    const page = AvatarPageStore.useContainer();
    const store = AvatarInfoStore.useContainer();
    const btnStyles = buttonStyles();
    const [currentSection, setCurrentSection] = useState("all");
    
    /**
     * @param {BodyColor} color
     */
    function setBodyColor(color) {
        let newBC;
        switch (currentSection) {
            case "all":
                newBC = Object.fromEntries(Object.keys(store.bodyColors).map(key => [key, color.brickColorId]));
                break;
            default:
                newBC = {
                    ...store.bodyColors,
                    [currentSection]: color.brickColorId,
                };
        }
        store.setModifiedBC(newBC);
    }
    
    const modalBody = <div className="flex flex-nowrap flex-row">
        <div className={`${s.optionsContainer} flex flex-nowrap flex-column`}>
            {
                options.map(option => {
                    return <div className={s.optionContainer} key={option.id}>
                        <input
                            id={option.id}
                            type="radio"
                            value={currentSection}
                            onClick={() => setCurrentSection(option.id)}
                            checked={currentSection === option.id}
                            className={s.optionInput}
                        />
                        <label htmlFor={option.id} className={s.optionLabel}>{option.label}</label>
                    </div>
                })
            }
        </div>
        <div className={s.colorContainer}>
            {
                store.avRules.bodyColorsPalette.map(color => {
                    let selected = false;
                    if (currentSection !== "all" && store.bodyColors[currentSection] === color.brickColorId) {
                        selected = true;
                    } else if (currentSection === "all" && options.filter(d => d.id !== "all")
                        .map(id => id.id).every(d => store.bodyColors[d] === color.brickColorId)
                    ) {
                        selected = true;
                    }
                    return <span
                        className={`${s.bodyColor} ${selected ? "selected" : ""}`}
                        style={{ backgroundColor: color.hexColor }}
                        onClick={() => setBodyColor(color)}
                    />
                })
            }
        </div>
    </div>
    
    return <NewModal
        title="Skin Tone by Body Parts"
        children={modalBody}
        footerElements={
            <ActionButton
                label="Done"
                buttonStyle={btnStyles.newCancelButton}
                onClick={async () => openModal(false)}
                className={s.modalBtn}
            />
        }
        footerClass={s.footerClass}
        containerClass={s.containerClass}
        offset={320}
    />
}

/**
 * @type {BodyParts[]}
 */
export const options = [
    {
        id: "all",
        label: "All",
    },
    {
        id: "headColorId",
        label: "Head",
    },
    {
        id: "torsoColorId",
        label: "Torso",
    },
    {
        id: "leftArmColorId",
        label: "Left Arm",
    },
    {
        id: "rightArmColorId",
        label: "Right Arm",
    },
    {
        id: "leftLegColorId",
        label: "Left Leg",
    },
    {
        id: "rightLegColorId",
        label: "Right Leg",
    },
]

/**
 * @typedef BodyParts
 * @property {string} id
 * @property {string} label
 */

export default AdvancedBodyColorsModal;
