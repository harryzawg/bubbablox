import {createUseStyles} from "react-jss";
import useButtonStyles from "../../styles/buttonStyles";

const useStyles = createUseStyles({
    pillToggle: {
        height: 24,
        display: "inline-block",
        backgroundColor: "#2c2c2c",
        padding: 2,
        borderRadius: "24px",
        "& input": {
            display: "none",
        },
        "& label": {
            color: "rgb(117, 117, 117)",
            userSelect: "none",
            minWidth: 36,
            textAlign: "center",
            float: "left",
            height: 20,
            lineHeight: "20px",
            verticalAlign: "middle",
            fontSize: 14,
            fontWeight: 400,
            cursor: "pointer",
            padding: "0 2px",
            borderRadius: 20,
        },
        "& input:checked+label": {
            backgroundColor: "#0074BD",
            color: "#fff",
        }
    }
});

// array of strings
/**
 *
 * @param {string[]} options
 * @param {string} selected
 * @param setSelected
 * @returns {JSX.Element}
 * @constructor
 */
function RadioPill({ options, selected, setSelected }) {
    const s = useStyles();
    
    return <div className={s.pillToggle}>
        {
            options.map(option => {
                return <>
                    <input onChange={(e) => setSelected(e.target.value)}
                           type="radio"
                           name="avatarType"
                           id={`radio-${option}`}
                           value={option}
                           checked={selected === option}
                    />
                    <label htmlFor={`radio-${option}`}>{option}</label>
                </>
            })
        }
    </div>
}

export default RadioPill;
