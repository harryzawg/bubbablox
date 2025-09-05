import { useState } from "react";
import { createUseStyles } from "react-jss";

const useStyles = createUseStyles({
    modalBg: {
        background: 'rgba(0,0,0,0.8)',
        position: 'fixed',
        top: 0,
        width: '100%',
        height: '100%',
        left: 0,
        zIndex: 9999,
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
    },
    modalContainer: {
        height: '100%',
        width: '100%',
        outline: '0px',
        overflow: 'visible',
    },
    modalWrapper: {
        margin: '0 auto',
        marginTop: 'calc(50vh - 125px)',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
    },
    modalDialog: {
        boxSizing: 'border-box',
        maxWidth: '100%',
        width: '400px',
        display: 'inline-block',
        textAlign: 'left',
        verticalAlign: 'middle',
        margin: 0
    },
    modalContent: {
        backgroundColor: 'var(--white-color)',
        borderRadius: 0,
        position: 'relative',
        border: '1px solid rgba(0, 0, 0, 0.2)',
        backgroundClip: 'padding-box',
        outline: 0,
    },
    modalHeader: {
        borderColor: 'var(--background-color)',
        textAlign: 'left',
        padding: '12px',
        minHeight: '16.428571429px'
    },
    modalHeaderBottomBorder: {
        borderBottom: '1px solid var(--background-color)',
    },
    modalHeaderText: {
        fontSize: '16px',
        fontWeight: '600',
        lineHeight: '1.428571429',
        margin: 0,
        padding: '5px 0',
    },
    modalBody: {
        textAlign: 'left',
        padding: '12px',
        position: 'relative',
    },
    modalMessage: {
        fontWeight: '400',
        fontSize: '16px',
        lineHeight: '1.4em'
    },
    modalFooter: {
        borderTop: 0,
        margin: '0 12px 12px',
        padding: 0,
        textAlign: 'center',
        color: 'var(--text-color-secondary)',
        fontSize: '10px',
        fontWeight: '500'
    },
    noDisplay: {
        display: 'none'
    },
    fullOpacity: {
        opacity: '0.5!important',
    },
    exitButton: {
        opacity: 0.2,
        overflow: 'visible',
        position: 'absolute',
        top: '12px',
        right: '12px',
        padding: 0,
        zIndex: '10',
        background: 'none',
        border: 'none'
    },
});

/**
 *
 * @param {{
 * title: any;
 * children: any;
 * footerElements?: any;
 * footerText?: any;
 * footerClass?: string;
 * containerClass?: string;
 * exitFunction?: () => void;
 * containerWidth?: number;
 * offset?: number;
 * headerBorder?: boolean;
 * }} props
 * @returns
 */

const newModal = props => {
    const s = useStyles();
    const [hoverX, setHoverX] = useState(false);

    const modalHeader = props.title;
    const modalTopBody = props.children;
    const footer = props.footerElements || props.footerText;

    const footerClass = footer ? null : s.noDisplay;
    const opacityClass = hoverX ? s.fullOpacity : null
    const bottomBorder = !props.headerBorder || modalHeader == null || modalHeader == undefined || modalHeader == "" ? null : s.modalHeaderBottomBorder

    return <div className={s.modalBg}>
        <div className={`${s.modalContainer} ${props.containerClass ? props.containerClass : ""}`}>
            <div className={s.modalWrapper} style={props.offset ? {marginTop: `calc(50vh - ${props.offset}px)`} : {}}>
                <div className={s.modalDialog} style={props.containerWidth && {width: props.containerWidth + 'px'}}>
                    <div className={s.modalContent}>
                        <div className={`${s.modalHeader} ${bottomBorder}`}>
                            <h5 className={s.modalHeaderText}>{modalHeader}</h5>
                            {props.exitFunction && <button className={`${s.exitButton} ${opacityClass}`} onClick={props.exitFunction} onMouseEnter={() => setHoverX(true)} onMouseLeave={() => setHoverX(false)}>
                                <span className="icon-close" />
                            </button>}
                        </div>
                        <div className={s.modalBody}>
                            {modalTopBody}
                        </div>
                        <div className={`${s.modalFooter} ${footerClass} ${props.footerClass}`}>
                            {footer}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
}

export default newModal;