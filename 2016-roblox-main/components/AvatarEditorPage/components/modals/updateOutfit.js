import {createUseStyles} from "react-jss";
import NewModal from "../../../newModal";
import ActionButton from "../../../actionButton";
import buttonStyles from "../../../../styles/buttonStyles";
import {updateOutfit} from "../../../../services/avatar";
import AvatarPageStore from "../../stores/avatarPageStore";
import AvatarInfoStore from "../../stores/avatarInfoStore";

const useStyles = createUseStyles({
    footerClass: {
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
    },
    modalBtn: {
        padding: 9,
        margin: "0 5px",
        width: 90,
        fontSize: "18px!important",
        fontWeight: "400!important",
        lineHeight: "100%!important",
    },
    containerClass: {
        "& h5": {
            fontSize: 18,
            fontWeight: 400,
            lineHeight: "1em",
        }
    },
});

/**
 * @param {number} outfitId
 * @param {(boolean) => {}} openModal
 * @returns {JSX.Element}
 * @constructor
 */
const UpdateOutfitModal = ({ outfitId, openModal }) => {
    const s = useStyles();
    const page = AvatarPageStore.useContainer();
    const store = AvatarInfoStore.useContainer();
    const btnStyles = buttonStyles();
    
    return <NewModal
        title="Update Outfit"
        children={<p style={{ margin: 0 }}>Do you want to update this outfit? This will overwrite the outfit with your avatar's current appearance.</p>}
        footerElements={<>
            <ActionButton
                label="Update"
                buttonStyle={btnStyles.newContinueButton}
                onClick={async () => {
                    openModal(false);
                    await updateOutfit({ outfitId });
                    page.setOutfits(page.outfits.map(d => d.outfitId === outfitId ? {
                        ...d,
                        thumbnail: store.avThumb || "/img/placeholder.png",
                        thumbnailState: "Completed",
                    } : d));
                }}
                className={s.modalBtn}
            />
            <ActionButton
                label="Cancel"
                buttonStyle={btnStyles.newCancelButton}
                onClick={() => openModal(false)}
                className={s.modalBtn}
            />
        </>}
        footerClass={s.footerClass}
        containerClass={s.containerClass}
    />
}

export default UpdateOutfitModal;
