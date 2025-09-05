const themeType = {
  obc2016: 'obc2016',
  light: 'light',
  dark: 'dark', // dark mode sucks and is janky asf
  default: 'light',
};

const isLocalStorageAvailable = (() => {
  if (typeof window === 'undefined' || !window.localStorage) return false;
  return true;
})();

const getTheme = () => {
  if (!isLocalStorageAvailable) return themeType.default;
  const savedTheme = localStorage.getItem('rbx_theme_v1');
  return Object.values(themeType).includes(savedTheme) ? savedTheme : themeType.default;
};

// Janky asf but it works
const darkmode = () => {
  if (typeof document === 'undefined') return;

  const styleId = 'rbx-dark-mode-styles';
  let style = document.getElementById(styleId);

  if (!style) {
    style = document.createElement('style');
    style.id = styleId;
    document.head.appendChild(style);
  }
  
  style.textContent = `
    html.dark {
      background: #121212 !important;
      color: #ffffff !important;
    }
    
    html.dark *:not(svg):not(svg *):not(img):not([class*="icon"]):not([class*="Icon"]):not(i):not([class*="fa-"]):not([class*="material-icon"]):not([class*="bi-"]) {
      background: #121212 !important;
      color: #ffffff !important;
      border-color: #333 !important;
    }
    
    html.dark svg,
    html.dark svg *,
    html.dark img,
    html.dark [class*="icon"],
    html.dark [class*="Icon"],
    html.dark i,
    html.dark [class*="fa-"],
    html.dark [class*="material-icon"],
    html.dark [class*="bi-"],
    html.dark .icon,
    html.dark .fa,
    html.dark .material-icons,
    html.dark .bi {
      background-color: transparent !important;
      color: unset !important;
      border-color: unset !important;
      fill: unset !important;
      stroke: unset !important;
    }
    
    html.dark svg:not([fill]) {
      fill: currentColor !important;
    }
    
    html.dark a {
      color: #4dabf7 !important;
    }
    
    html.dark input, 
    html.dark textarea, 
    html.dark select {
      background: #1e1e1e !important;
      color: #ffffff !important;
      border-color: #444 !important;
    }
  `;
};

const nodarkmode = () => {
  if (typeof document === 'undefined') return;
  const style = document.getElementById('rbx-dark-mode-styles');
  if (style) style.remove();
};

const setTheme = (theme) => {
  if (!isLocalStorageAvailable || !Object.values(themeType).includes(theme)) return;
  
  localStorage.setItem('rbx_theme_v1', theme);
  darkmode(theme);
};

const apply = (theme) => {
  if (typeof document === 'undefined') return;
  
  const html = document.documentElement;

  Object.values(themeType).forEach((t) => html.classList.remove(t));
  
  html.classList.add(theme);
  
  if (theme === 'dark') {
    darkmode();
  } else {
    nodarkmode();
  }
};

if (isLocalStorageAvailable && typeof window !== 'undefined') {
  apply(getTheme());
}

export { themeType, getTheme, setTheme };