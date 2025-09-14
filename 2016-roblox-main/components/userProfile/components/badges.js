import { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import { getUserBadges } from "../../../services/accountInformation";
import { multiGetAssetThumbnails } from "../../../services/thumbnails";
import SmallButtonLink from "./smallButtonLink";
import Subtitle from "./subtitle";
import Link from "../../link";

const useBadgeStyles = createUseStyles({
  label: {
    width: '100%',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  imageWrapper: {
    border: '1px solid #b8b8b8',
    borderRadius: '4px',
    width: '100%',
    height: 'auto',
    overflow: 'hidden',
    minWidth: '142px',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
  },
  badgeImage: {
    maxWidth: '100%',
    maxHeight: '100%',
  },
  buttonWrapper: {
    width: '100px',
    float: 'right',
  },
  badgeLink: {
    color: 'inherit',
    textDecoration: 'none',
  },
});

const Badges = ({ userId }) => {
  const s = useBadgeStyles();
  const [badges, setBadges] = useState([]);
  const [badgeThumbnails, setBadgeThumbnails] = useState({});
  const [showAll, setShowAll] = useState(false);

  useEffect(() => {
    getUserBadges({ userId }).then((data) => {
      setBadges(data);

      const badgeIds = data.map(b => b.id);
      multiGetAssetThumbnails({ assetIds: badgeIds }).then((thumbs) => {
        const thumbMap = {};
        thumbs.forEach(t => {
          thumbMap[t.targetId || t.id] = t.imageUrl;
        });
        setBadgeThumbnails(thumbMap);
      });
    });
  }, [userId]);

  if (!badges.length) return null;

  return (
    <div className='row d-none d-lg-flex'>
      <div className='col-10'>
        <Subtitle>Player Badges ({badges.length})</Subtitle>
      </div>
      <div className='col-6 col-lg-2'>
        {badges.length > 6 && (
          <div className={s.buttonWrapper + ' mt-2'}>
            <SmallButtonLink
              onClick={(e) => {
                e.preventDefault();
                setShowAll(!showAll);
              }}
            >
              {showAll ? 'See Less' : 'See More'}
            </SmallButtonLink>
          </div>
        )}
      </div>
      <div className='col-12'>
        <div className='card pt-4 pb-4 pe-4 ps-4'>
          <div className='row'>
            {badges.slice(0, showAll ? badges.length : 6).map((badge) => (
              <div className='col-4 col-lg-2' key={badge.id}>
                <Link href={`/catalog/${badge.id}/Badge`} className={s.badgeLink}>
                  <a>
                    <div className={s.imageWrapper}>
                      {badgeThumbnails[badge.id] ? (
                        <img
                          src={badgeThumbnails[badge.id]}
                          alt={badge.name}
                          className={s.badgeImage}
                        />
                      ) : (
                        <span>Loading...</span>
                      )}
                    </div>
                    <p className={`${s.label} mb-0 text-dark`}>{badge.name}</p>
                  </a>
                </Link>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Badges;