import { useEffect, useState } from "react"
import { createUseStyles } from "react-jss"
import {getItemUrl, getBadgesForPlace, itemNameToEncodedName} from "../../../services/catalog"
import CreatorLink from "../../creatorLink"
import ItemImage from "../../itemImage"
import Link from "../../link";

const useEntryStyles = createUseStyles({
  creator: {
    fontSize: '12px',
    color: '#999',
    marginTop: '2px',
  },
  image: {
    maxWidth: '110px',
    display: 'block',
    margin: '0 auto',
  },
})

const BadgeEntry = props => {
  const s = useEntryStyles();
  return <div className='col-4 col-lg-2'>
    <div className={s.image}>
      <ItemImage id={props.id}/>
    </div>
    <p className='mb-0 text-center'>
      <Link href={getItemUrl({assetId: props.id, name: props.name})}>
        <a>
          {props.name}
        </a>
      </Link>
    </p>
  </div>
}

const useBadgeStyles = createUseStyles({
  row: {
    '@media (min-width: 500px)': {
      '& >div': {
        width: '20%',
      },
    }
  }
});

/**
 * Badges based off the {assetId}
 * @param {{assetId: number; assetType: number;}} props 
 */
const Badges = props => {
  const s = useBadgeStyles();
  const [badges, setBadges] = useState(null);

  useEffect(() => {
    getBadgesForPlace({
      placeId: props.assetId,
      limit: 50,
    }).then(result => {
      setBadges(result.data);
    });
  }, [props.assetId]);

  return <div className={`row ${s.row}`}>
    {
      badges && badges.map((v) => {
        return <BadgeEntry key={v.item.assetId} id={v.item.assetId} name={v.item.name}/>
      })
    }
  </div>
}

export default Badges;