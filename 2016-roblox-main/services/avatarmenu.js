const menuType = {
  r15: 'R15',
  legacy: 'Legacy',
  default: 'R15',
};

const isLocalStorageAvailable = (() => {
  if (typeof window === 'undefined' || !window.localStorage) return false;
  return true;
})();

const getAvatarMenu = () => {
  if (!isLocalStorageAvailable) return menuType.default;
  const savedTheme = localStorage.getItem('rbx_avatarmenu_type');
  return Object.values(menuType).includes(savedTheme) ? savedTheme : menuType.default;
};

const setAvatarYear = (theme) => {
  if (!isLocalStorageAvailable || !Object.values(menuType).includes(theme)) return;
  
  localStorage.setItem('rbx_avatarmenu_type', theme);
};

export { menuType, getAvatarMenu, setAvatarYear };