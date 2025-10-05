import React from "react";
import OldModal from "../../oldModal";
import MyAccountStore from "../stores/myAccountStore"
import ModalChangePassword from "./modalChangePassword";
import ModalChangeUsername from "./modalChangeUsername";
import Modal2FA from "./modal2FA";

const ModalHandler = props => {
  // TODO: once 2016 modal is created, switch this to 2016 modal
  const store = MyAccountStore.useContainer();
  switch (store.modal) {
    case 'MODAL_OK':
      return <OldModal title={store.modalMessage.title} onClose={() => {
        store.setModal(null);
      }}>
        <div className='row'>
          <div className='col-12 ps-4'>
            <p className='mb-0 text-center mt-4'>{store.modalMessage.message}</p>
          </div>
        </div>
      </OldModal>
    case 'CHANGE_PASSWORD':
      return <OldModal title='Change Password' onClose={() => {
        store.setModal(null);
      }}>
        <ModalChangePassword></ModalChangePassword>
      </OldModal>
    case 'CHANGE_USERNAME':
      return <OldModal height={290} title='Change Username' onClose={() => {
        store.setModal(null);
      }}>
        <ModalChangeUsername></ModalChangeUsername>
      </OldModal>
    case 'MODAL_ENABLE_2FA':
	  return <OldModal title='Enable 2FA' onClose={() => {
		store.setModal(null);
	  }}>
		<Modal2FA />
	  </OldModal>
    case 'MODAL_ENABLE_2FA_BIG':
	  return <OldModal height={450} width={500} title='Enable 2FA' onClose={() => {
		store.setModal(null);
	  }}>
		<Modal2FA />
	  </OldModal>
  }
  return null;
}

export default ModalHandler;