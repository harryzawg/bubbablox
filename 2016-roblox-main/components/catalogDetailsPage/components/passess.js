import { useEffect, useState } from "react"
import { createUseStyles } from "react-jss"
import {getItemUrl, getPassessForPlace, itemNameToEncodedName} from "../../../services/catalog"
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

const PassEntry = props => {
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

const usePassStyles = createUseStyles({
  row: {
    '@media (min-width: 500px)': {
      '& >div': {
        width: '20%',
      },
    }
  }
});

/**
 * Passess based off the {assetId}
 * @param {{assetId: number; assetType: number;}} props 
 */
const Passess = props => {
  const s = usePassStyles();
  const [passess, setPassess] = useState(null);

  useEffect(() => {
    getPassessForPlace({
      placeId: props.assetId,
      limit: 50,
    }).then(result => {
      setPassess(result.data);
    });
  }, [props.assetId]);

  return <div className={`row ${s.row}`}>
    {
      passess && passess.map((v) => {
        return <PassEntry key={v.item.assetId} id={v.item.assetId} name={v.item.name}/>
      })
    }
  </div>
}

export default Passess;