import {createUseStyles} from "react-jss";
import {useEffect, useState} from "react";

const useStyles = createUseStyles({
    sliderContainer: {
        height: 24,
    },
    slider: {
        "-webkit-appearance": "none",
        width: '100%',
        height: 40,
        marginTop: "-6px",
        background: "none",
        padding: 0,
        lineHeight: 'normal',
        "&:focus": {
            outline: "none",
        },
    },
    // chat gpt'd because im not rewriting all this
    // from ROBLOX normal css converted to jss (thats what i gpt'd)
    '@global': {
        'input[type="range"]': {
            '-webkit-appearance': 'none',
            width: '100%',
            height: '40px',
            marginTop: '-6px',
            background: 'none',
            padding: 0,
        },
        
        'input[type="range"]:focus': {
            outline: 'none',
        },
        
        'input[type="range"]::-webkit-slider-runnable-track': {
            width: '100%',
            height: '6px',
            boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
            background: 'linear-gradient(to right, var(--progress-color) var(--progress), #E3E3E3 var(--progress))',
            borderRadius: '6px',
            border: '0 solid #000',
            cursor: 'pointer',
            // composes: '$sliderTrack',
        },
        
        'input[type="range"]::-moz-range-track': {
            width: '100%',
            height: '6px',
            boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
            background: '#E3E3E3',
            borderRadius: '6px',
            border: '0 solid #000',
            cursor: 'pointer',
        },
        
        'input[type="range"]::-ms-track': {
            width: '100%',
            height: '6px',
            boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
            background: '#E3E3E3',
            borderRadius: '6px',
            border: '0 solid #000',
            cursor: 'pointer',
            borderColor: 'transparent',
            color: 'transparent',
        },
        
        'input[type="range"]:disabled::-webkit-slider-runnable-track': {
            cursor: 'not-allowed',
        },
        
        'input[type="range"]:disabled::-moz-range-track': {
            cursor: 'not-allowed',
        },
        
        'input[type="range"]:disabled::-ms-track': {
            cursor: 'not-allowed',
        },
        
        'input[type="range"]::-ms-fill-lower': {
            background: '#00A2FF',
            border: '0 solid #000',
            borderRadius: '6px',
            boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
            cursor: 'pointer',
        },
        
        'input[type="range"]::-ms-fill-upper': {
            background: '#E3E3E3',
            border: '0 solid #000',
            borderRadius: '6px',
            boxShadow: '0 1px 4px 0 rgba(25,25,25,0.3)',
        },
        
        'input[type="range"]::-ms-tooltip': {
            display: 'none',
        },
        
        'input[type="range"]::-moz-range-progress': {
            backgroundColor: '#00A2FF',
            height: '6px',
            borderRadius: '6px',
            cursor: 'pointer',
        },
      
        'input[type="range"]:disabled::-moz-range-progress': {
            background: '#B8B8B8',
            cursor: 'not-allowed',
        },
        
        'input[type="range"]:disabled::-ms-fill-lower': {
            background: '#B8B8B8',
            cursor: 'not-allowed',
        },
        
        'input[type="range"]::-webkit-slider-thumb': {
            '-webkit-transition': 'box-shadow 200ms ease',
            '-o-transition': 'box-shadow 200ms ease',
            transition: 'box-shadow 200ms ease',
            border: '2px solid #00A2FF',
            height: '24px',
            width: '24px',
            borderRadius: '24px',
            background: '#fff',
            boxShadow: '0 0 0 0 rgba(0,162,255,0.5),0 1px 4px 0 rgba(25,25,25,0.3)',
            boxSizing: 'border-box',
            cursor: 'pointer',
            '-webkit-appearance': 'none',
            marginTop: '-9px',
        },
        
        'input[type="range"]::-moz-range-thumb': {
            '-webkit-transition': 'box-shadow 200ms ease',
            '-o-transition': 'box-shadow 200ms ease',
            transition: 'box-shadow 200ms ease',
            border: '2px solid #00A2FF',
            height: '24px',
            width: '24px',
            borderRadius: '24px',
            background: '#fff',
            boxShadow: '0 0 0 0 rgba(0,162,255,0.5),0 1px 4px 0 rgba(25,25,25,0.3)',
            boxSizing: 'border-box',
            cursor: 'pointer',
        },
        
        'input[type="range"]::-ms-thumb': {
            '-webkit-transition': 'box-shadow 200ms ease',
            '-o-transition': 'box-shadow 200ms ease',
            transition: 'box-shadow 200ms ease',
            border: '2px solid #00A2FF',
            height: '24px',
            width: '24px',
            borderRadius: '24px',
            background: '#fff',
            boxShadow: '0 0 0 0 rgba(0,162,255,0.5),0 1px 4px 0 rgba(25,25,25,0.3)',
            boxSizing: 'border-box',
            cursor: 'pointer',
            marginTop: '-1px',
        },
        
        'input[type="range"]:not(:disabled)::-webkit-slider-thumb:hover': {
            boxShadow: '0 0 0 6px rgba(0,162,255,0.5),0 1px 4px 0 rgba(25,25,25,0.3)',
        },
        
        'input[type="range"]:not(:disabled)::-moz-range-thumb:hover': {
            boxShadow: '0 0 0 6px rgba(0,162,255,0.5),0 1px 4px 0 rgba(25,25,25,0.3)',
        },
        
        'input[type="range"]:not(:disabled)::-ms-thumb:hover': {
            boxShadow: '0 0 0 6px rgba(0,162,255,0.5),0 1px 4px 0 rgba(25,25,25,0.3)',
        },
        
        'input[type="range"]:disabled::-webkit-slider-thumb': {
            cursor: 'not-allowed',
            border: '2px solid #B8B8B8',
        },
        
        'input[type="range"]:disabled::-moz-range-thumb': {
            cursor: 'not-allowed',
            border: '2px solid #B8B8B8',
        },
        
        'input[type="range"]:disabled::-ms-thumb': {
            cursor: 'not-allowed',
            border: '2px solid #B8B8B8',
        },
    },
});

function Slider({ className, step, min, max, value, setValue, changeValue, disabled }) {
    const s = useStyles();
    const [downStats, setDownStats] = useState(null);
    
    useEffect(() => {
        const sliders = document.querySelectorAll('input[type="range"]');
        sliders.forEach(slider => {
            const updateProgress = () => {
                const percentage = (100 * (slider.value - slider.min)) / (slider.max - slider.min);
                slider.style.setProperty('--progress', `${percentage}%`);
                slider.style.setProperty('--progress-color', disabled ? "#ADADAD" : "#00A2FF");
            };
            slider.addEventListener('input', updateProgress);
            updateProgress();
        });
    }, [disabled, value]);
    
    const onInputUp = e => {
        if (downStats === value) return;
        setDownStats(null);
        changeValue(e);
    }
    
    return <input
        type="range"
        disabled={disabled}
        className={`${className} ${s.slider}`}
        step={step}
        min={min}
        max={max}
        value={value}
        onMouseDown={() => setDownStats(value)}
        onMouseUp={onInputUp}
        onTouchEnd={onInputUp}
        onChange={setValue}
    />
}

export default Slider;
