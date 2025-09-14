import React from "react";
import Ads from "./components/subPages/ads";
import Clothing from "./components/subPages/clothing";
import GamesSubPage from "./components/subPages/games";
import Badges from "./components/subPages/badges";
import Passess from "./components/subPages/passess";

const developerPages = [
  {
    id: 0,
    name: 'Games',
    url: '/develop?View=0',
    element: props => <GamesSubPage {...props}/>,
  },
/*   {
    id: 9,
    name: 'Places',
    url: '/develop?View=9',
    disabled: true,
  }, */
  {
    id: 10,
    name: 'Models',
    url: '/develop?View=10',
    disabled: true,
  },
  {
    id: 102,
    name: 'Decals',
    url: '/develop?View=102',
    element: props => <Clothing id={1} {...props} />
  },
  {
    id: 21,
    name: 'Badges',
    url: '/develop?View=21',
	element: props => <Badges {...props}/>,
  },
  {
    id: 34,
    name: 'Passes',
    url: '/develop?View=34',
    element: props => <Passess {...props}/>,
  },
  {
    id: 3,
    name: 'Audio',
    url: '/develop?View=3',
    element: props => <Clothing id={3} {...props}/>,
  },
/*   {
    id: 24,
    name: 'Animations',
    url: '/develop?View=24',
    disabled: true,
  }, */
  {
    id: 4,
    name: 'Meshes',
    url: '/develop?View=4',
    element: props => <Clothing id={4} {...props}/>,
  },
  {
    id: 101,
    name: 'User Ads',
    url: '/develop?View=101',
    element: props => <Ads {...props}/>,
  },
/*   {
    id: 102,
    name: 'Sponsored Games',
    url: '/develop?View=102',
    disabled: true,
  }, */
  {
    id: 11,
    name: 'Shirts',
    url: '/develop?View=11',
    element: props => <Clothing id={11} {...props}/>,
  },
  {
    id: 2,
    name: 'T-Shirts',
    url: '/develop?View=2',
    element: props => <Clothing id={2} {...props}/>,
  },
  {
    id: 12,
    name: 'Pants',
    url: '/develop?View=12',
    element: props => <Clothing id={12} {...props}/>,
  },
/*   {
    id: 38,
    name: 'Plugins',
    url: '/develop?View=38',
    disabled: true,
  }, */
];

export {
  developerPages,
}