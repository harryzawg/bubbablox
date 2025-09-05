import {useEffect, useRef, useState} from "react";
import {createUseStyles} from "react-jss";
import AvatarPageStore from "../stores/avatarPageStore";
import {abbreviateNumber} from "../../../lib/numberUtils";
import {wait} from "../../../lib/utils";

const useStyles = createUseStyles({
    vTab: {
        flex: '1',
        display: 'inline-block',
        marginBottom: '-1px',
        float: 'left',
        height: "100%",
    },
    vTabLabel: {
        border: '0',
        backgroundColor: 'var(--white-color)',
        margin: 0,
        padding: '12px 2%',
        fontSize: '16px',
        fontWeight: '400',
        cursor: 'default',
        lineHeight: '100%',
        boxShadow: 'inset 0 -4px 0 0 var(--primary-color)',
        '&:hover': {
            background: '#f2f2f2'
        },
    },
    spanText: {
        display: 'inline-block',
        margin: 0,
        fontWeight: '500',
        lineHeight: '15px',
        fontSize: 18,
        "& *": {
            fontSize: "inherit",
            lineHeight: "inherit",
        }
    },
    vTagSelected: {},
    buttonCol: {
        //borderBottom: '2px solid var(--text-color-quinary)',
        color: 'var(--text-color-primary)',
        background: 'none',
        textAlign: 'center',
        width: '100%',
        padding: 0,
        border: 0,
        display: 'flex',
        flexDirection: 'row',
        boxShadow: "0 1px 3px rgba(150,150,150,0.74)",
        zIndex: 2,
        position: 'relative',
        "& *": {
            userSelect: "none",
            cursor: 'pointer',
        }
    },
    btnBottomSeperator: {
        width: '100%',
        height: '5px',
        background: 'white',
        marginBottom: '-5px',
    },
    vTabUnselected: {
        cursor: 'pointer',
        // 9e9e9e
        '&:not(:hover)': {
            boxShadow: 'none'
        }
    },
    count: {
        background: '#e0f1fc',
        border: '1px solid #84a5c9',
        paddingLeft: '4px',
        paddingRight: '4px',
    },
    selectedElement: {
        margin: 0,
    }
});

/**
 * Vertical tabs, custom for avatar
 * @param {{
 * options: {id: string; name: string; element: JSX.Element; onClick?: any; count?: number}[];
 * onChange?: (arg: {name: string; element: JSX.Element; count?: number;}) => void;
 * default?: string;
 * elementClass?: string;
 * }} props
 */
const avatarTabs = props => {
    const s = useStyles();
    const {options} = props;
    const {activeSubmenu, setActiveSubmenu, openSubmenu, setOpenSubmenu} = AvatarPageStore.useContainer();
    const [selectedMouseHover, setSelectedMouseHover] = useState(false);
    const [currentHovered, setCurrentHovered] = useState(null);
    
    useEffect(() => {
        //if (!openSubmenu) setOpenSubmenu(options[0]);
        
        const handleClick = () => {
            if (activeSubmenu) {
                setActiveSubmenu(null);
            }
        };
        document.addEventListener('click', handleClick);
        return () => {
            document.removeEventListener('click', handleClick);
        }
    }, []);
    
    useEffect(() => {
        if (openSubmenu && !activeSubmenu && currentHovered) setOpenSubmenu(currentHovered);
    }, [openSubmenu]);
    
    return <div style={{position: 'relative'}}>
        <div className={`${s.buttonCol} col-12`}>
            {
                options.map(option => {
                    const isSelected = option.id === openSubmenu?.id;
                    return <div key={option.name} className={s.vTab} onClick={() => {
                        setActiveSubmenu(activeSubmenu ? null : option);
                        if (props.onChange) props.onChange(option);
                        if (option.onClick) option.onClick(option);
                    }} onMouseEnter={() => {
                        setCurrentHovered(option);
                        setOpenSubmenu(option);
                    }} onMouseLeave={async () => {
                        setCurrentHovered(null);
                        if (!selectedMouseHover) {
                            await wait(0.1);
                            setOpenSubmenu(null);
                        }
                    }}>
                        <p className={`${!isSelected && s.vTabUnselected} ${s.vTabLabel}`}>
                            <span className={s.spanText}>
                                {option.name}
                                {
                                    typeof option.count === 'number' &&
                                    <span className={s.count}>{abbreviateNumber(option.count)}</span>
                                }
                            </span>
                        </p>
                    </div>
                })
            }
        </div>
        <div className={'col-12 ' + s.selectedElement + ' ' + props.elementClass}
             onMouseEnter={() => setSelectedMouseHover(true)} onMouseLeave={async () => {
            setSelectedMouseHover(false)
            if (!currentHovered) {
                await wait(0.1);
                setOpenSubmenu(null);
            }
        }}>
            {activeSubmenu?.element || openSubmenu?.element}
        </div>
    </div>
}

export default avatarTabs;