import { useEffect, useState } from "react";
import { createUseStyles } from "react-jss";
import { QRCodeSVG } from 'qrcode.react';
import { getFullUrl } from "../../../lib/request";
import request from "../../../lib/request";
import useModalStyles from "../styles/modal";
import ActionButton from "../../actionButton";
import MyAccountStore from "../stores/myAccountStore";

const ModalEnable2FA = () => {
  const s = useModalStyles();
  const store = MyAccountStore.useContainer();

  const [enabled, setEnabled] = useState(null);
  const [totp, setTotp] = useState(null);
  const [code, setCode] = useState("");
  const [feedback, setFeedback] = useState(null);
  const [locked, setLocked] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    request('GET', getFullUrl('auth', '/v2/user/two-step/setup'))
      .then(res => {
        console.log('setup res:', res);
        setEnabled(res.data.enabled);
        setTotp(res.data.totp || null);
        setFeedback(null);
      })
      .catch(err => {
        console.error('setup err:', err);
        setFeedback("Failed to generate TOTP, please try again!");
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);
  
  const handleEnable2FA = () => {
    setLocked(true);
    setFeedback(null);

    request('POST', getFullUrl('auth', '/v2/user/two-step/enable'), {
      code,
    })
      .then(() => {
        store.setModalMessage({
          title: '2FA enabled!',
          message: 'Two-factor authentication is now enabled on your account.',
        });
        store.setModal('MODAL_OK');
        setEnabled(true);
        setCode("")
      })
      .catch(err => {
        setFeedback(
          err?.response?.data?.errors?.[0]?.message ||
          "Failed to enable 2FA. Please check your code and try again."
        );
      })
      .finally(() => setLocked(false));
  };
  
  const handleDisable2FA = () => {
    setLocked(true);
    setFeedback(null);

    request('POST', getFullUrl('auth', '/v2/user/two-step/disable'), {
      code,
    })
      .then(() => {
        store.setModalMessage({
          title: '2FA disabled',
          message: 'Two-factor authentication has been disabled on your account.',
        });
        store.setModal('MODAL_OK');
        setEnabled(false);
        setCode("");
      })
      .catch(err => {
        setFeedback(
          err?.response?.data?.errors?.[0]?.message ||
          "Failed to disable 2FA. Please check your code and try again."
        );
      })
      .finally(() => setLocked(false));
  };
  
  if (loading) {
    return <p className="ps-4">Loading 2FA...</p>;
  }

  if (feedback) {
    return <p className="ps-4 text-danger">{feedback}</p>;
  }

  console.log("enabled:", enabled, "totp:", totp);
  
  if (enabled === false && totp) {
	store.setModal('MODAL_ENABLE_2FA_BIG');
    const OTP = `otpauth://totp/BubbaBlox?secret=${totp}&issuer=BubbaBlox`;

    return (
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'center',
          alignItems: 'center',
          height: '100%',
          textAlign: 'center',
          padding: 20,
        }}
      >
        <p className="mb-2">
          Scan the QR code below with your authenticator app (e.g. Google Authenticator, Authy):
        </p>

        <div className="mb-3" style={{ textAlign: 'center' }}>
          <QRCodeSVG value={OTP} size={180} />
        </div>

        <p className="fw-700" style={{ marginBottom: 16 }}>
          Or enter this code manually:
        </p>
        <p style={{ wordBreak: 'break-word', marginBottom: 24 }}>
          {totp}
        </p>

        <input
          type="text"
          className={s.input}
          placeholder="Enter 6-digit code"
          value={code}
          disabled={locked}
          onChange={(e) => setCode(e.currentTarget.value)}
          style={{ marginBottom: 16 }}
        />

        <div style={{ marginBottom: 20, width: '100%', display: 'flex', justifyContent: 'center' }}>
          <ActionButton
            disabled={locked || !code}
            label="Enable"
            onClick={handleEnable2FA}
          />
        </div>
      </div>
    );
  }

  if (enabled === false && !totp) {
    return <p className="ps-4">Something went wrong creating your TOTP, please try again!</p>;
  }

  if (enabled) {
    return (
      <div className="row ps-4">
        <div className="col-12">
          <p className="mb-3">Two-factor authentication is currently <strong>enabled</strong> on your account.</p>

          <input
            type="text"
            className={s.input}
            placeholder="Enter 6-digit code"
            value={code}
            disabled={locked}
            onChange={(e) => setCode(e.currentTarget.value)}
          />

          <div className={s.confirmWrapper + ' mt-4 ' + s.confirmWrapperWide}>
            <ActionButton
              disabled={locked || code.length !== 6}
              label="Disable 2FA"
              onClick={handleDisable2FA}
            />
          </div>
        </div>
      </div>
    );
  }

  return <p className="ps-4">Something went wrong, please try again!</p>;
};

export default ModalEnable2FA;