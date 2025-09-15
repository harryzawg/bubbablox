import React, { useState, useEffect } from 'react';
import { createUseStyles } from 'react-jss';
import { getBaseUrl } from '../../lib/request';

const useStyles = createUseStyles({
  root: {
    position: 'fixed',
    top: 0,
    left: 0,
    width: '100vw',
    height: '100vh',
    overflow: 'hidden',
    margin: 0,
    padding: 0,
  },
  iframe: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    border: 'none',
  },
});

const Privacy = () => {
  const classes = useStyles();
  const [iframeSrc, setIframeSrc] = useState('');

  useEffect(() => {
    document.body.style.overflow = 'hidden';

    const iframe = `${getBaseUrl()}/UnsecuredContent/privacy.html`;
    setIframeSrc(iframe);

    return () => {
      document.body.style.overflow = '';
    };
  }, []);

  return (
    <div className={classes.root}>
      <iframe
        className={classes.iframe}
        src={iframeSrc}
        title="Privacy Policy - BubbaBlox"
        onError={(e) => {
          console.error("iframe failed to load:", e);
        }}
      />
    </div>
  );
};

export default Privacy;
