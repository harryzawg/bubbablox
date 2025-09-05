import AdBanner from "../../components/ad/adBanner";
import React, { useEffect, useState } from "react";
import AvatarEditor from "../../components/AvatarEditorPage";
import AvatarInfoStore from "../../components/AvatarEditorPage/stores/avatarInfoStore";
import Theme2016 from "../../components/theme2016";
import AvatarPageStore from "../../components/AvatarEditorPage/stores/avatarPageStore";
import AuthenticationStore from "../../stores/authentication";
import Head from "next/head";

// Sorry pekora

const AvatarPage = () => {
    const [isMounted, setIsMounted] = useState(false);

    useEffect(() => {
        setIsMounted(true);
    }, []);
	
	// stupid ass hack
    if (!isMounted) {
        return (
            <Theme2016>
                <Head>
                    <title>Avatar - BubbaBlox</title>
                </Head>
                <div className="container flex flex-column ssp">
                    <AdBanner context="MyCharacterPage"/>
                    <div>Loading</div>
                </div>
            </Theme2016>
        );
    }

    return (
        <Theme2016>
            <Head>
                <title>Avatar - BubbaBlox</title>
            </Head>
            <div className="container flex flex-column ssp">
                <AdBanner context="MyCharacterPage"/>
                
                <AuthenticationStore.Provider>
                    <AvatarInfoStore.Provider>
                        <AvatarPageStore.Provider>
                            <AvatarEditor />
                        </AvatarPageStore.Provider>
                    </AvatarInfoStore.Provider>
                </AuthenticationStore.Provider>
            </div>
        </Theme2016>
    );
}

export default AvatarPage;