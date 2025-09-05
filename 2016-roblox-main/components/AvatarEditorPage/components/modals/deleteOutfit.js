import {createUseStyles} from "react-jss";
import NewModal from "../../../newModal";
import ActionButton from "../../../actionButton";
import buttonStyles from "../../../../styles/buttonStyles";
import {deleteOutfit} from "../../../../services/avatar";
import AvatarPageStore from "../../stores/avatarPageStore";

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
const DeleteOutfitModal = ({ outfitId, openModal }) => {
    const s = useStyles();
    const page = AvatarPageStore.useContainer();
    const btnStyles = buttonStyles();
    
    return <NewModal
        title="Delete Outfit"
        children={<p style={{ margin: 0 }}>Are you sure you want to delete this outfit?</p>}
        footerElements={<>
            <ActionButton
                label="Delete"
                buttonStyle={btnStyles.newWarningButton}
                onClick={async () => {
                    openModal(false);
                    await deleteOutfit({ outfitId });
                    page.setOutfits(page.outfits.filter(v => v.outfitId !== outfitId));
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

export default DeleteOutfitModal;
