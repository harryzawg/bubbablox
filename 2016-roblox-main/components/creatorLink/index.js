import { useState, useEffect } from "react";
import {itemNameToEncodedName} from "../../services/catalog";
import Link from "../link";
import {getUserInfo} from "../../services/users";

const verifiedCache = new Map();

/**
 * Creator link
 * @param {{type: string | number; id: number; name: string;}} props 
 * @returns 
 */
const CreatorLink = (props) => {
  const [isVerified, setIsVerified] = useState(false);
  const url = (props.type === 'User' || props.type === 1) ? '/users/' + props.id + "/profile" : '/My/Groups.aspx?gid=' + props.id;
  
  useEffect(() => {
    if (props.type === 'User' || props.type === 1) {
      const Key = `user-${props.id}`;

      if (verifiedCache.has(Key)) {
        setIsVerified(verifiedCache.get(Key));
        return;
      }
      
      getUserInfo({ userId: props.id })
        .then(userInfo => {
          const verified = userInfo.isVerified || false;
          setIsVerified(verified);
          verifiedCache.set(Key, verified);
        })
        .catch(error => {
          console.error('failed to get user info:', error);
        });
    }
  }, [props.id, props.type]);
  
  return <Link href={url}>
    <a>
      {props.name}
      {isVerified && <img src="/verified.svg" alt="Verified" style={{width: '17px', height: '17px', marginLeft: '3px'}} />}
    </a>
  </Link>
}

export default CreatorLink;