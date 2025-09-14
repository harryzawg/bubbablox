import React, { useEffect, useRef, useState } from "react";
import { getCreatedItems, uploadBadgePass } from "../../../../services/develop";
import AuthenticationStore from "../../../../stores/authentication";
import AssetList from "../assetList";
import ActionButton from "../../../actionButton";

const Badges = () => {
  const auth = AuthenticationStore.useContainer();
  const [badges, setBadges] = useState(null);
  const [selectedPlaceId, setSelectedPlaceId] = useState(null);
  const [badgeName, setBadgeName] = useState('');
  const [badgeDescription, setBadgeDescription] = useState('');
  const [feedback, setFeedback] = useState(null);
  const [locked, setLocked] = useState(false);

  const fileRef = useRef(null);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const placeId = params.get('selectedPlaceId');
    if (placeId) setSelectedPlaceId(placeId);
  }, []);

  useEffect(() => {
    if (!auth.userId) return;
    getCreatedItems({ limit: 100, cursor: '', assetType: 34 })
      .then(data => setBadges(data))
      .catch(err => console.error(err));
  }, [auth.userId]);

  const onSubmit = async () => {
    if (locked) return;
    if (!fileRef.current.files.length) return setFeedback("You must select a file.");
    if (!badgeName) return setFeedback("You must provide a badge name.");
    if (!badgeDescription) return setFeedback("You must provide a description.");

    setFeedback(null);
    setLocked(true);

    try {
	  // why does upload asset not come with a description field Boi
      await uploadBadgePass({
        name: badgeName,
        description: badgeDescription,
        assetTypeId: 34,
		placeId: selectedPlaceId,
        file: fileRef.current.files[0]
      });
      window.location.href = "/develop?View=34";
    } catch (err) {
      setFeedback(err.message);
      setLocked(false);
    }
  };

  // Make the template an actual link
  if (selectedPlaceId) {
    return (
      <div>
        <h2>Upload a Game Pass</h2>
        <p>
          Don't know how? <a href="/img/Template.png">Click Here</a>.
        </p>

        <div style={{ marginBottom: '10px' }}>
          <label>Image:</label><br/>
          <input type="file" ref={fileRef} accept="image/*" style={{ width: '300px', height: '30px' }} />
          {feedback && <span style={{ color: 'red', marginLeft: '8px' }}>{feedback}</span>}
        </div>

        <div style={{ marginBottom: '10px' }}>
          <label>Name:</label><br/>
          <input
            type="text"
            value={badgeName}
            onChange={e => setBadgeName(e.target.value)}
            style={{ width: '300px', height: '30px', borderRadius: 0, border: '1px solid #ccc' }}
          />
        </div>

        <div style={{ marginBottom: '10px' }}>
          <label>Description:</label><br/>
          <textarea
            value={badgeDescription}
            onChange={e => setBadgeDescription(e.target.value)}
            style={{ width: '300px', height: '60px', borderRadius: 0, border: '1px solid #ccc' }}
          />
        </div>

        <ActionButton disabled={locked} label="Upload" onClick={onSubmit} />
      </div>
    );
  }

  return (
    <div>
      <h2>Your Game Passess</h2>
      {badges ? (
        badges.data.length === 0
          ? <p>You haven't created any game passess yet.</p>
          : <AssetList assets={badges.data} />
      ) : <p>Loading...</p>}
    </div>
  );
};

export default Badges;
