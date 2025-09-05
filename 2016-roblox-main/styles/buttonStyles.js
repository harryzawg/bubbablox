import { createUseStyles } from "react-jss";

const useButtonStyles = createUseStyles({
  buyButton: {
    width: '100%',
    paddingTop: '5px',
    paddingBottom: '5px',
    background: 'linear-gradient(0deg, rgba(0,113,0,1) 0%, rgba(64,193,64,1) 100%)', // 40c140 #007100
    '&:hover': {
      background: 'linear-gradient(0deg, rgba(71,232,71,1) 0%, rgba(71,232,71,1) 100%)', // 47e847 02a101
    },
  },
  normal: {
    width: 'auto!important',
  },
  cancelButton: {
    width: '100%',
    paddingTop: '5px',
    paddingBottom: '5px',
    background: 'linear-gradient(0deg, rgba(69,69,69,1) 0%, rgba(140,140,140,1) 100%)', // top #8c8c8c bottom #454545
    border: '1px solid #404041',
    '&:hover': {
      'background': 'grey!important',
    },
  },
  continueButton: {
    width: '100%',
    paddingTop: '5px',
    paddingBottom: '5px',
    background: 'linear-gradient(0deg, rgba(8,79,192,1) 0%, rgba(5,103,234,1) 100%)', // #0567ea #084fc0
    border: '1px solid #084ea6',
    '&:hover': {
      background: 'linear-gradient(0deg, rgba(2,73,198,1) 0%, rgba(7,147,253,1) 100%); ',
    },
  },
  badPurchaseRow: {
    marginTop: '70px',
  },
  newCancelButton: {
        background: '#fff',
        border: '1px solid var(--text-color-secondary)',
        borderColor: 'var(--text-color-secondary)!important',
        borderRadius: '3px',
        fontWeight: '500',
        color: 'var(--text-color-primary)!important',
        fontSize: '18px',
        userSelect: 'none',
        display: 'inline-block',
        height: 'auto',
        textAlign: 'center',
        whiteSpace: 'nowrap',
        verticalAlign: 'middle',
        padding: '9px',
        lineHeight: '100%',
        transition: "box-shadow 200ms ease-in-out",
        "-webkit-transition": "box-shadow 200ms ease-in-out",
        '&:hover': {
            background: '#fff',
            color: '#000',
            cursor: 'pointer',
            boxShadow: '0 1px 3px rgb(150 150 150 / 74%)'
        },
        '&:visited': {
            color: 'rgba(0,0,0,.6)'
        }
    },
	newBuyButton: {
        background: 'var(--success-color)',
        border: '1px solid var(--success-color)',
        borderColor: 'var(--success-color)!important',
        borderRadius: '3px',
        fontWeight: '500',
        color: '#fff!important',
        fontSize: '18px',
        userSelect: 'none',
        display: 'inline-block',
        height: 'auto',
        textAlign: 'center',
        whiteSpace: 'nowrap',
        verticalAlign: 'middle',
        padding: '9px',
        lineHeight: '100%',
        '&:hover': {
            background: 'var(--success-color-hover)!important',
            borderColor: 'var(--success-color-hover)!important',
            cursor: 'pointer',
        },
        "&:disabled": {
            opacity: .5,
            backgroundColor: "#a3e2bd",
            borderColor: "#a3e2bd!important",
            cursor: "not-allowed",
            pointerEvents: "none",
        },
        "&.tix": {
            background: 'var(--tix-color)',
            borderColor: 'var(--tix-color)!important',
            '&:hover': {
                background: 'var(--tix-color-hover)!important',
                borderColor: 'var(--tix-color-hover)!important',
            },
            "&:disabled": {
                backgroundColor: "#E3C7A1",
                borderColor: "#E3C7A1!important",
            },
        },
    },
});

export default useButtonStyles