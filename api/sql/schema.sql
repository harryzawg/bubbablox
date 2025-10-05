--
-- PostgreSQL database dump
--

-- Dumped from database version 17.4
-- Dumped by pg_dump version 17.4

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: abuse_report; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.abuse_report (
    id character varying(128) NOT NULL,
    user_id bigint NOT NULL,
    author_id bigint,
    report_reason integer NOT NULL,
    report_status integer NOT NULL,
    report_message character varying(1024) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.abuse_report OWNER TO postgres;

--
-- Name: asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset (
    roblox_asset_id bigint,
    id bigint NOT NULL,
    name character varying(255) NOT NULL,
    description character varying(4096) DEFAULT NULL::character varying,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    asset_type smallint NOT NULL,
    asset_genre smallint NOT NULL,
    creator_type smallint NOT NULL,
    creator_id bigint NOT NULL,
    moderation_status smallint NOT NULL,
    is_for_sale boolean DEFAULT false NOT NULL,
    price_robux bigint,
    price_tix bigint,
    is_limited boolean DEFAULT false NOT NULL,
    is_limited_unique boolean DEFAULT false NOT NULL,
    serial_count bigint,
    sale_count bigint DEFAULT '0'::bigint NOT NULL,
    offsale_at timestamp with time zone,
    recent_average_price bigint,
    comments_enabled boolean DEFAULT false NOT NULL,
    is_18_plus boolean DEFAULT false NOT NULL,
    visible boolean DEFAULT true
);


ALTER TABLE public.asset OWNER TO postgres;

--
-- Name: asset_advertisement; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_advertisement (
    id bigint NOT NULL,
    target_id bigint NOT NULL,
    target_type smallint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    advertisement_type smallint NOT NULL,
    advertisement_asset_id bigint NOT NULL,
    name character varying(512) NOT NULL,
    impressions_all bigint DEFAULT '0'::bigint NOT NULL,
    clicks_all bigint DEFAULT '0'::bigint NOT NULL,
    bid_amount_tix_all bigint DEFAULT '0'::bigint NOT NULL,
    bid_amount_robux_all bigint DEFAULT '0'::bigint NOT NULL,
    impressions_last_run bigint DEFAULT '0'::bigint NOT NULL,
    clicks_last_run bigint DEFAULT '0'::bigint NOT NULL,
    bid_amount_robux_last_run bigint DEFAULT '0'::bigint NOT NULL,
    bid_amount_tix_last_run bigint DEFAULT '0'::bigint NOT NULL
);


ALTER TABLE public.asset_advertisement OWNER TO postgres;

--
-- Name: asset_advertisement_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_advertisement_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_advertisement_id_seq OWNER TO postgres;

--
-- Name: asset_advertisement_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_advertisement_id_seq OWNED BY public.asset_advertisement.id;


--
-- Name: asset_comment; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_comment (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    user_id bigint NOT NULL,
    comment character varying(1024) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.asset_comment OWNER TO postgres;

--
-- Name: asset_comment_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_comment_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_comment_id_seq OWNER TO postgres;

--
-- Name: asset_comment_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_comment_id_seq OWNED BY public.asset_comment.id;


--
-- Name: asset_datastore; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_datastore (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    universe_id bigint NOT NULL,
    scope character varying(255) NOT NULL,
    key character varying(255) NOT NULL,
    name character varying(255) NOT NULL,
    value character varying(1048576) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.asset_datastore OWNER TO postgres;

--
-- Name: asset_datastore_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_datastore_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_datastore_id_seq OWNER TO postgres;

--
-- Name: asset_datastore_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_datastore_id_seq OWNED BY public.asset_datastore.id;


--
-- Name: asset_favorite; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_favorite (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    asset_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.asset_favorite OWNER TO postgres;

--
-- Name: asset_favorite_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_favorite_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_favorite_id_seq OWNER TO postgres;

--
-- Name: asset_favorite_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_favorite_id_seq OWNED BY public.asset_favorite.id;


--
-- Name: asset_icon; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_icon (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    content_url character varying(512) NOT NULL,
    moderation_status smallint NOT NULL
);


ALTER TABLE public.asset_icon OWNER TO postgres;

--
-- Name: asset_icon_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_icon_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_icon_id_seq OWNER TO postgres;

--
-- Name: asset_icon_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_icon_id_seq OWNED BY public.asset_icon.id;


--
-- Name: asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_id_seq OWNER TO postgres;

--
-- Name: asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_id_seq OWNED BY public.asset.id;


--
-- Name: asset_media; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_media (
    id bigint NOT NULL,
    asset_type integer NOT NULL,
    asset_id bigint NOT NULL,
    media_asset_id bigint,
    media_video_hash character varying(128) DEFAULT NULL::character varying,
    media_video_title character varying(128) DEFAULT NULL::character varying,
    is_approved boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.asset_media OWNER TO postgres;

--
-- Name: asset_media_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_media_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_media_id_seq OWNER TO postgres;

--
-- Name: asset_media_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_media_id_seq OWNED BY public.asset_media.id;


--
-- Name: asset_package; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_package (
    package_asset_id bigint NOT NULL,
    asset_id bigint NOT NULL
);


ALTER TABLE public.asset_package OWNER TO postgres;

--
-- Name: asset_place; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_place (
    asset_id bigint NOT NULL,
    max_player_count integer DEFAULT 10 NOT NULL,
    server_fill_mode integer DEFAULT 1 NOT NULL,
    server_slot_size integer,
    is_vip_enabled boolean DEFAULT false NOT NULL,
    vip_price integer,
    is_public_domain boolean DEFAULT false NOT NULL,
    access integer DEFAULT 1 NOT NULL,
    visit_count bigint DEFAULT '0'::bigint NOT NULL,
    gear_permission boolean DEFAULT false NOT NULL,
    playable boolean DEFAULT true NOT NULL,
    year integer DEFAULT 2016 NOT NULL,
    rig_type character varying(20) DEFAULT 'playerChoice'::character varying NOT NULL
);


ALTER TABLE public.asset_place OWNER TO postgres;

--
-- Name: asset_place_badge; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_place_badge (
    badge_id bigint NOT NULL,
    place_id bigint NOT NULL,
    awarded bigint DEFAULT 0 NOT NULL,
    CONSTRAINT asset_place_badge_unique UNIQUE (badge_id, place_id)
);

ALTER TABLE public.asset_place_badge OWNER TO postgres;


--
-- Name: asset_place_pass; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_place_pass (
    pass_id bigint NOT NULL,
    place_id bigint NOT NULL
);


ALTER TABLE public.asset_place_pass OWNER TO postgres;

--
-- Name: asset_play_history; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_play_history (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    user_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ended_at timestamp with time zone
);


ALTER TABLE public.asset_play_history OWNER TO postgres;

--
-- Name: asset_play_history_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_play_history_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_play_history_id_seq OWNER TO postgres;

--
-- Name: asset_play_history_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_play_history_id_seq OWNED BY public.asset_play_history.id;


--
-- Name: asset_server; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_server (
    id uuid NOT NULL,
    asset_id bigint NOT NULL,
    ip character varying(45),
    port integer NOT NULL,
    rccconnection character varying(100),
    created_at timestamp without time zone DEFAULT now(),
    updated_at timestamp without time zone DEFAULT now()
);


ALTER TABLE public.asset_server OWNER TO postgres;

--
-- Name: asset_server_player; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_server_player (
    server_id uuid NOT NULL,
    user_id bigint NOT NULL,
    asset_id bigint NOT NULL
);


ALTER TABLE public.asset_server_player OWNER TO postgres;

--
-- Name: asset_thumbnail; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_thumbnail (
    asset_id bigint NOT NULL,
    asset_version_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    content_url character varying(512) NOT NULL,
    moderation_status smallint NOT NULL
);


ALTER TABLE public.asset_thumbnail OWNER TO postgres;

--
-- Name: asset_version; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_version (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    version_number integer NOT NULL,
    content_url character varying(512) DEFAULT NULL::character varying,
    creator_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    content_id bigint
);


ALTER TABLE public.asset_version OWNER TO postgres;

--
-- Name: asset_version_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_version_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_version_id_seq OWNER TO postgres;

--
-- Name: asset_version_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_version_id_seq OWNED BY public.asset_version.id;


--
-- Name: asset_version_metadata_image; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_version_metadata_image (
    asset_version_id bigint NOT NULL,
    image_format integer NOT NULL,
    resolution_x integer NOT NULL,
    resolution_y integer NOT NULL,
    size_bytes integer NOT NULL,
    hash character varying(64) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.asset_version_metadata_image OWNER TO postgres;

--
-- Name: asset_vote; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.asset_vote (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    asset_id bigint NOT NULL,
    type integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.asset_vote OWNER TO postgres;

--
-- Name: asset_vote_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.asset_vote_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.asset_vote_id_seq OWNER TO postgres;

--
-- Name: asset_vote_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.asset_vote_id_seq OWNED BY public.asset_vote.id;


--
-- Name: collectible_sale_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.collectible_sale_logs (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    amount bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.collectible_sale_logs OWNER TO postgres;

--
-- Name: collectible_sale_logs_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.collectible_sale_logs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.collectible_sale_logs_id_seq OWNER TO postgres;

--
-- Name: collectible_sale_logs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.collectible_sale_logs_id_seq OWNED BY public.collectible_sale_logs.id;


--
-- Name: forum_post; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.forum_post (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    post character varying(1024) NOT NULL,
    title character varying(255) DEFAULT NULL::character varying,
    thread_id bigint,
    sub_category_id integer NOT NULL,
    is_pinned boolean DEFAULT false NOT NULL,
    is_locked boolean DEFAULT false NOT NULL,
    views bigint DEFAULT '0'::bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.forum_post OWNER TO postgres;

--
-- Name: forum_post_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.forum_post_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.forum_post_id_seq OWNER TO postgres;

--
-- Name: forum_post_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.forum_post_id_seq OWNED BY public.forum_post.id;


--
-- Name: forum_post_read; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.forum_post_read (
    forum_post_id bigint NOT NULL,
    user_id bigint NOT NULL
);


ALTER TABLE public.forum_post_read OWNER TO postgres;

--
-- Name: group; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."group" (
    id bigint NOT NULL,
    user_id bigint,
    locked boolean DEFAULT false NOT NULL,
    name character varying(255) NOT NULL,
    description character varying(1024) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public."group" OWNER TO postgres;

--
-- Name: group_audit_log; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_audit_log (
    id bigint NOT NULL,
    group_id bigint NOT NULL,
    user_id bigint NOT NULL,
    action integer NOT NULL,
    new_owner_user_id bigint,
    old_role_id bigint,
    new_role_id bigint,
    user_id_range_change bigint,
    role_set_id bigint,
    old_rank integer,
    new_rank integer,
    old_name character varying(255) DEFAULT NULL::character varying,
    new_name character varying(255) DEFAULT NULL::character varying,
    old_description character varying(255) DEFAULT NULL::character varying,
    new_description character varying(255) DEFAULT NULL::character varying,
    post_desc character varying(255) DEFAULT NULL::character varying,
    post_user_id bigint,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    fund_recipient_user_id bigint,
    currency_amount bigint,
    currency_type integer
);


ALTER TABLE public.group_audit_log OWNER TO postgres;

--
-- Name: group_audit_log_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_audit_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_audit_log_id_seq OWNER TO postgres;

--
-- Name: group_audit_log_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_audit_log_id_seq OWNED BY public.group_audit_log.id;


--
-- Name: group_economy; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_economy (
    group_id bigint NOT NULL,
    balance_robux integer NOT NULL,
    balance_tickets integer NOT NULL
);


ALTER TABLE public.group_economy OWNER TO postgres;

--
-- Name: group_icon; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_icon (
    group_id bigint NOT NULL,
    name character varying(255) NOT NULL,
    is_approved integer DEFAULT 0 NOT NULL,
    user_id bigint
);


ALTER TABLE public.group_icon OWNER TO postgres;

--
-- Name: group_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_id_seq OWNER TO postgres;

--
-- Name: group_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_id_seq OWNED BY public."group".id;


--
-- Name: group_role; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_role (
    id bigint NOT NULL,
    group_id bigint NOT NULL,
    name character varying(255) NOT NULL,
    description character varying(255) NOT NULL,
    rank integer NOT NULL,
    member_count bigint DEFAULT '0'::bigint NOT NULL
);


ALTER TABLE public.group_role OWNER TO postgres;

--
-- Name: group_role_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_role_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_role_id_seq OWNER TO postgres;

--
-- Name: group_role_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_role_id_seq OWNED BY public.group_role.id;


--
-- Name: group_role_permission; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_role_permission (
    group_role_id bigint NOT NULL,
    delete_from_wall boolean DEFAULT false NOT NULL,
    post_to_wall boolean DEFAULT false NOT NULL,
    invite_members boolean DEFAULT false NOT NULL,
    post_to_status boolean DEFAULT false NOT NULL,
    remove_members boolean DEFAULT false NOT NULL,
    view_status boolean DEFAULT false NOT NULL,
    view_wall boolean DEFAULT false NOT NULL,
    change_rank boolean DEFAULT false NOT NULL,
    advertise_group boolean DEFAULT false NOT NULL,
    manage_relationships boolean DEFAULT false NOT NULL,
    add_group_places boolean DEFAULT false NOT NULL,
    view_audit_logs boolean DEFAULT false NOT NULL,
    create_items boolean DEFAULT false NOT NULL,
    manage_items boolean DEFAULT false NOT NULL,
    spend_group_funds boolean DEFAULT false NOT NULL,
    manage_clan boolean DEFAULT false NOT NULL,
    manage_group_games boolean DEFAULT false NOT NULL
);


ALTER TABLE public.group_role_permission OWNER TO postgres;

--
-- Name: group_settings; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_settings (
    group_id bigint NOT NULL,
    approval_required boolean DEFAULT false NOT NULL,
    enemies_allowed boolean DEFAULT false NOT NULL,
    funds_visible boolean DEFAULT false NOT NULL,
    games_visible boolean DEFAULT false NOT NULL
);


ALTER TABLE public.group_settings OWNER TO postgres;

--
-- Name: group_social_link; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_social_link (
    id bigint NOT NULL,
    group_id bigint NOT NULL,
    type integer NOT NULL,
    url character varying(255) NOT NULL,
    title character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.group_social_link OWNER TO postgres;

--
-- Name: group_social_link_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_social_link_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_social_link_id_seq OWNER TO postgres;

--
-- Name: group_social_link_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_social_link_id_seq OWNED BY public.group_social_link.id;


--
-- Name: group_status; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_status (
    id bigint NOT NULL,
    group_id bigint NOT NULL,
    user_id bigint NOT NULL,
    status character varying(255),
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.group_status OWNER TO postgres;

--
-- Name: group_status_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_status_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_status_id_seq OWNER TO postgres;

--
-- Name: group_status_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_status_id_seq OWNED BY public.group_status.id;


--
-- Name: group_user; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_user (
    id bigint NOT NULL,
    group_role_id bigint NOT NULL,
    user_id bigint NOT NULL
);


ALTER TABLE public.group_user OWNER TO postgres;

--
-- Name: group_user_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_user_id_seq OWNER TO postgres;

--
-- Name: group_user_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_user_id_seq OWNED BY public.group_user.id;


--
-- Name: group_wall; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.group_wall (
    id bigint NOT NULL,
    group_id bigint NOT NULL,
    user_id bigint NOT NULL,
    content character varying(1024) NOT NULL,
    is_deleted boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.group_wall OWNER TO postgres;

--
-- Name: group_wall_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.group_wall_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.group_wall_id_seq OWNER TO postgres;

--
-- Name: group_wall_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.group_wall_id_seq OWNED BY public.group_wall.id;


--
-- Name: join_application; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.join_application (
    id character varying(128) NOT NULL,
    preferred_name character varying(512) NOT NULL,
    about character varying(4096) NOT NULL,
    social_presence character varying(512) NOT NULL,
    user_id bigint,
    author_id bigint,
    reject_reason character varying(512) DEFAULT NULL::character varying,
    status integer DEFAULT 1 NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    join_id character varying(128) DEFAULT NULL::character varying,
    locked_at timestamp with time zone,
    locked_by_user_id bigint,
    is_verified boolean DEFAULT false NOT NULL,
    verified_url character varying(512) DEFAULT NULL::character varying,
    verified_id character varying(512) DEFAULT NULL::character varying,
    verification_phrase character varying(512) DEFAULT NULL::character varying
);


ALTER TABLE public.join_application OWNER TO postgres;

--
-- Name: knex_migrations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.knex_migrations (
    id integer NOT NULL,
    name character varying(255),
    batch integer,
    migration_time timestamp with time zone
);


ALTER TABLE public.knex_migrations OWNER TO postgres;

--
-- Name: knex_migrations_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.knex_migrations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.knex_migrations_id_seq OWNER TO postgres;

--
-- Name: knex_migrations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.knex_migrations_id_seq OWNED BY public.knex_migrations.id;


--
-- Name: knex_migrations_lock; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.knex_migrations_lock (
    index integer NOT NULL,
    is_locked integer
);


ALTER TABLE public.knex_migrations_lock OWNER TO postgres;

--
-- Name: knex_migrations_lock_index_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.knex_migrations_lock_index_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.knex_migrations_lock_index_seq OWNER TO postgres;

--
-- Name: knex_migrations_lock_index_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.knex_migrations_lock_index_seq OWNED BY public.knex_migrations_lock.index;


--
-- Name: moderation_admin_message; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_admin_message (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    subject character varying(1024) NOT NULL,
    body character varying(4096) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_admin_message OWNER TO postgres;

--
-- Name: moderation_admin_message_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_admin_message_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_admin_message_id_seq OWNER TO postgres;

--
-- Name: moderation_admin_message_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_admin_message_id_seq OWNED BY public.moderation_admin_message.id;


--
-- Name: moderation_bad_username; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_bad_username (
    id bigint NOT NULL,
    username character varying(512) NOT NULL
);


ALTER TABLE public.moderation_bad_username OWNER TO postgres;

--
-- Name: moderation_bad_username_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_bad_username_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_bad_username_id_seq OWNER TO postgres;

--
-- Name: moderation_bad_username_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_bad_username_id_seq OWNED BY public.moderation_bad_username.id;


--
-- Name: moderation_bad_username_log; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_bad_username_log (
    id bigint NOT NULL,
    username character varying(512) NOT NULL,
    user_id bigint NOT NULL,
    author_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_bad_username_log OWNER TO postgres;

--
-- Name: moderation_bad_username_log_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_bad_username_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_bad_username_log_id_seq OWNER TO postgres;

--
-- Name: moderation_bad_username_log_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_bad_username_log_id_seq OWNED BY public.moderation_bad_username_log.id;


--
-- Name: moderation_ban; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_ban (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    reason character varying(1024) NOT NULL,
    internal_reason character varying(1024),
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    expired_at timestamp with time zone
);


ALTER TABLE public.moderation_ban OWNER TO postgres;

--
-- Name: moderation_ban_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_ban_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_ban_id_seq OWNER TO postgres;

--
-- Name: moderation_ban_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_ban_id_seq OWNED BY public.moderation_ban.id;


--
-- Name: moderation_change_join_app; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_change_join_app (
    id bigint NOT NULL,
    application_id character varying(255) NOT NULL,
    author_user_id bigint NOT NULL,
    new_status integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_change_join_app OWNER TO postgres;

--
-- Name: moderation_change_join_app_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_change_join_app_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_change_join_app_id_seq OWNER TO postgres;

--
-- Name: moderation_change_join_app_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_change_join_app_id_seq OWNED BY public.moderation_change_join_app.id;


--
-- Name: moderation_give_item; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_give_item (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    author_user_id bigint NOT NULL,
    user_asset_id bigint NOT NULL,
    user_id_from bigint,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_give_item OWNER TO postgres;

--
-- Name: moderation_give_item_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_give_item_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_give_item_id_seq OWNER TO postgres;

--
-- Name: moderation_give_item_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_give_item_id_seq OWNED BY public.moderation_give_item.id;


--
-- Name: moderation_give_robux; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_give_robux (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    author_user_id bigint NOT NULL,
    amount bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_give_robux OWNER TO postgres;

--
-- Name: moderation_give_robux_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_give_robux_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_give_robux_id_seq OWNER TO postgres;

--
-- Name: moderation_give_robux_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_give_robux_id_seq OWNED BY public.moderation_give_robux.id;


--
-- Name: moderation_give_tickets; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_give_tickets (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    author_user_id bigint NOT NULL,
    amount bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_give_tickets OWNER TO postgres;

--
-- Name: moderation_give_tickets_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_give_tickets_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_give_tickets_id_seq OWNER TO postgres;

--
-- Name: moderation_give_tickets_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_give_tickets_id_seq OWNED BY public.moderation_give_tickets.id;


--
-- Name: moderation_manage_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_manage_asset (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    action integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_manage_asset OWNER TO postgres;

--
-- Name: moderation_manage_asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_manage_asset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_manage_asset_id_seq OWNER TO postgres;

--
-- Name: moderation_manage_asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_manage_asset_id_seq OWNED BY public.moderation_manage_asset.id;


--
-- Name: moderation_migrate_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_migrate_asset (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    roblox_asset_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_migrate_asset OWNER TO postgres;

--
-- Name: moderation_migrate_asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_migrate_asset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_migrate_asset_id_seq OWNER TO postgres;

--
-- Name: moderation_migrate_asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_migrate_asset_id_seq OWNED BY public.moderation_migrate_asset.id;


--
-- Name: moderation_modify_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_modify_asset (
    id integer NOT NULL,
    asset_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    old_name text NOT NULL,
    new_name text NOT NULL,
    old_description text,
    new_description text,
    created_at timestamp with time zone DEFAULT now()
);


ALTER TABLE public.moderation_modify_asset OWNER TO postgres;

--
-- Name: moderation_modify_asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_modify_asset_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_modify_asset_id_seq OWNER TO postgres;

--
-- Name: moderation_modify_asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_modify_asset_id_seq OWNED BY public.moderation_modify_asset.id;


--
-- Name: moderation_overwrite_thumbnail; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_overwrite_thumbnail (
    id bigint NOT NULL,
    asset_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    content_url text NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.moderation_overwrite_thumbnail OWNER TO postgres;

--
-- Name: moderation_overwrite_thumbnail_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_overwrite_thumbnail_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_overwrite_thumbnail_id_seq OWNER TO postgres;

--
-- Name: moderation_overwrite_thumbnail_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_overwrite_thumbnail_id_seq OWNED BY public.moderation_overwrite_thumbnail.id;


--
-- Name: moderation_purchase_resale_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_purchase_resale_asset (
    id bigint NOT NULL,
    user_asset_id bigint NOT NULL,
    buyer_user_id bigint NOT NULL,
    seller_user_id bigint NOT NULL,
    asset_id bigint NOT NULL,
    purchase_price bigint NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_purchase_resale_asset OWNER TO postgres;

--
-- Name: moderation_purchase_resale_asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_purchase_resale_asset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_purchase_resale_asset_id_seq OWNER TO postgres;

--
-- Name: moderation_purchase_resale_asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_purchase_resale_asset_id_seq OWNED BY public.moderation_purchase_resale_asset.id;


--
-- Name: moderation_refund_transaction; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_refund_transaction (
    id bigint NOT NULL,
    transaction_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    user_id_one bigint NOT NULL,
    user_id_two bigint NOT NULL,
    asset_id bigint,
    user_asset_id bigint,
    amount bigint NOT NULL,
    currency_type integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_refund_transaction OWNER TO postgres;

--
-- Name: moderation_refund_transaction_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_refund_transaction_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_refund_transaction_id_seq OWNER TO postgres;

--
-- Name: moderation_refund_transaction_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_refund_transaction_id_seq OWNED BY public.moderation_refund_transaction.id;


--
-- Name: moderation_reset_password; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_reset_password (
    id integer NOT NULL,
    user_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.moderation_reset_password OWNER TO postgres;

--
-- Name: moderation_reset_password_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_reset_password_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_reset_password_id_seq OWNER TO postgres;

--
-- Name: moderation_reset_password_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_reset_password_id_seq OWNED BY public.moderation_reset_password.id;


--
-- Name: moderation_sell_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_sell_asset (
    id bigint NOT NULL,
    user_asset_id bigint NOT NULL,
    user_id bigint NOT NULL,
    asset_id bigint NOT NULL,
    old_price bigint DEFAULT 0 NOT NULL,
    new_price bigint NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.moderation_sell_asset OWNER TO postgres;

--
-- Name: moderation_sell_asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_sell_asset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_sell_asset_id_seq OWNER TO postgres;

--
-- Name: moderation_sell_asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_sell_asset_id_seq OWNED BY public.moderation_sell_asset.id;


--
-- Name: moderation_set_alert; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_set_alert (
    id bigint NOT NULL,
    actor_id bigint NOT NULL,
    alert character varying(4096) DEFAULT NULL::character varying,
    alert_url character varying(4096) DEFAULT NULL::character varying,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_set_alert OWNER TO postgres;

--
-- Name: moderation_set_alert_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_set_alert_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_set_alert_id_seq OWNER TO postgres;

--
-- Name: moderation_set_alert_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_set_alert_id_seq OWNED BY public.moderation_set_alert.id;


--
-- Name: moderation_set_rap; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_set_rap (
    id integer NOT NULL,
    asset_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    new_rap bigint NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.moderation_set_rap OWNER TO postgres;

--
-- Name: moderation_set_rap_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_set_rap_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_set_rap_id_seq OWNER TO postgres;

--
-- Name: moderation_set_rap_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_set_rap_id_seq OWNED BY public.moderation_set_rap.id;


--
-- Name: moderation_transcripts; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_transcripts (
    id bigint NOT NULL,
    ticket_id bigint NOT NULL,
    user_id bigint NOT NULL,
    discord_id character varying(255) NOT NULL,
    message text NOT NULL,
    name text DEFAULT 'Ticket'::text,
    created_at timestamp with time zone DEFAULT now(),
    updated_at timestamp with time zone DEFAULT now()
);


ALTER TABLE public.moderation_transcripts OWNER TO postgres;

--
-- Name: moderation_transcripts_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_transcripts_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_transcripts_id_seq OWNER TO postgres;

--
-- Name: moderation_transcripts_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_transcripts_id_seq OWNED BY public.moderation_transcripts.id;


--
-- Name: moderation_unban; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_unban (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    actor_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_unban OWNER TO postgres;

--
-- Name: moderation_unban_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_unban_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_unban_id_seq OWNER TO postgres;

--
-- Name: moderation_unban_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_unban_id_seq OWNED BY public.moderation_unban.id;


--
-- Name: moderation_update_product; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_update_product (
    id bigint NOT NULL,
    actor_id bigint NOT NULL,
    asset_id bigint NOT NULL,
    is_limited boolean NOT NULL,
    is_limited_unique boolean NOT NULL,
    is_for_sale boolean NOT NULL,
    price_in_robux integer,
    price_in_tickets integer,
    max_copies integer,
    offsale_at timestamp with time zone,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.moderation_update_product OWNER TO postgres;

--
-- Name: moderation_update_product_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_update_product_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_update_product_id_seq OWNER TO postgres;

--
-- Name: moderation_update_product_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_update_product_id_seq OWNED BY public.moderation_update_product.id;


--
-- Name: moderation_user_ban; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.moderation_user_ban (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    author_user_id bigint NOT NULL,
    reason character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    expired_at timestamp with time zone,
    internal_reason text
);


ALTER TABLE public.moderation_user_ban OWNER TO postgres;

--
-- Name: moderation_user_ban_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.moderation_user_ban_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.moderation_user_ban_id_seq OWNER TO postgres;

--
-- Name: moderation_user_ban_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.moderation_user_ban_id_seq OWNED BY public.moderation_user_ban.id;


--
-- Name: password_reset_tokens; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.password_reset_tokens (
    user_id bigint NOT NULL,
    token text NOT NULL,
    expires_at timestamp without time zone NOT NULL,
    used boolean DEFAULT false NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


ALTER TABLE public.password_reset_tokens OWNER TO postgres;

--
-- Name: promocode_redemptions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.promocode_redemptions (
    id integer NOT NULL,
    promocode integer NOT NULL,
    user_id bigint NOT NULL,
    redeemed_at timestamp without time zone DEFAULT now() NOT NULL,
    asset_id bigint,
    robux integer
);


ALTER TABLE public.promocode_redemptions OWNER TO postgres;

--
-- Name: promocode_redemptions_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.promocode_redemptions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.promocode_redemptions_id_seq OWNER TO postgres;

--
-- Name: promocode_redemptions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.promocode_redemptions_id_seq OWNED BY public.promocode_redemptions.id;


--
-- Name: promocodes; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.promocodes (
    id integer NOT NULL,
    code character varying(50) NOT NULL,
    asset_id bigint,
    robux integer,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    expires_at timestamp with time zone,
    maxuses integer,
    uses integer DEFAULT 0 NOT NULL,
    active boolean DEFAULT true NOT NULL
);


ALTER TABLE public.promocodes OWNER TO postgres;

--
-- Name: promocodes_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.promocodes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.promocodes_id_seq OWNER TO postgres;

--
-- Name: promocodes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.promocodes_id_seq OWNED BY public.promocodes.id;


--
-- Name: trade_currency_log; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.trade_currency_log (
    id bigint NOT NULL,
    order_id bigint NOT NULL,
    user_id bigint NOT NULL,
    source_amount bigint NOT NULL,
    destination_amount bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.trade_currency_log OWNER TO postgres;

--
-- Name: trade_currency_log_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.trade_currency_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.trade_currency_log_id_seq OWNER TO postgres;

--
-- Name: trade_currency_log_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.trade_currency_log_id_seq OWNED BY public.trade_currency_log.id;


--
-- Name: trade_currency_order; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.trade_currency_order (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    start_amount bigint NOT NULL,
    balance bigint NOT NULL,
    exchange_rate bigint NOT NULL,
    source_currency integer NOT NULL,
    destination_currency integer NOT NULL,
    is_closed boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    closed_at timestamp with time zone
);


ALTER TABLE public.trade_currency_order OWNER TO postgres;

--
-- Name: trade_currency_order_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.trade_currency_order_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.trade_currency_order_id_seq OWNER TO postgres;

--
-- Name: trade_currency_order_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.trade_currency_order_id_seq OWNED BY public.trade_currency_order.id;


--
-- Name: universe; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.universe (
    id bigint NOT NULL,
    root_asset_id bigint NOT NULL,
    is_public boolean DEFAULT false NOT NULL,
    creator_id bigint NOT NULL,
    creator_type integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.universe OWNER TO postgres;

--
-- Name: universe_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.universe_asset (
    universe_id bigint NOT NULL,
    asset_id bigint NOT NULL
);


ALTER TABLE public.universe_asset OWNER TO postgres;

--
-- Name: universe_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.universe_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.universe_id_seq OWNER TO postgres;

--
-- Name: universe_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.universe_id_seq OWNED BY public.universe.id;


--
-- Name: user; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."user" (
    id bigint NOT NULL,
    username character varying(64) NOT NULL,
    password character varying(255) NOT NULL,
    status integer DEFAULT 1 NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    description character varying(1024) DEFAULT NULL::character varying,
    online_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    session_key integer DEFAULT 0 NOT NULL,
    is_18_plus boolean DEFAULT false NOT NULL,
    session_expired_at timestamp with time zone,
    verified boolean DEFAULT false NOT NULL
);


ALTER TABLE public."user" OWNER TO postgres;

--
-- Name: user_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_asset (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    asset_id bigint NOT NULL,
    serial bigint,
    price bigint DEFAULT '0'::bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_asset OWNER TO postgres;

--
-- Name: user_asset_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_asset_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_asset_id_seq OWNER TO postgres;

--
-- Name: user_asset_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_asset_id_seq OWNED BY public.user_asset.id;


--
-- Name: user_avatar; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_avatar (
    user_id bigint NOT NULL,
    thumbnail_url character varying(255),
    avatar_type integer DEFAULT 1 NOT NULL,
    scale_height double precision DEFAULT '1'::double precision NOT NULL,
    scale_width double precision DEFAULT '1'::double precision NOT NULL,
    scale_head double precision DEFAULT '1'::double precision NOT NULL,
    scale_depth double precision DEFAULT '1'::double precision NOT NULL,
    scale_proportion double precision DEFAULT '0'::double precision NOT NULL,
    scale_body_type double precision DEFAULT '0'::double precision NOT NULL,
    head_color_id integer NOT NULL,
    torso_color_id integer NOT NULL,
    right_arm_color_id integer NOT NULL,
    left_arm_color_id integer NOT NULL,
    right_leg_color_id integer NOT NULL,
    left_leg_color_id integer NOT NULL,
    headshot_thumbnail_url character varying(255) DEFAULT NULL::character varying
);


ALTER TABLE public.user_avatar OWNER TO postgres;

--
-- Name: user_avatar_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_avatar_asset (
    user_id bigint NOT NULL,
    asset_id bigint NOT NULL
);


ALTER TABLE public.user_avatar_asset OWNER TO postgres;

--
-- Name: user_avatar_type; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_avatar_type (
    user_id integer NOT NULL,
    r15 boolean DEFAULT false NOT NULL,
    height integer DEFAULT 100 NOT NULL,
    width integer DEFAULT 100 NOT NULL,
    head integer DEFAULT 100 NOT NULL,
    proportion integer DEFAULT 100 NOT NULL,
    body_type integer DEFAULT 100 NOT NULL
);


ALTER TABLE public.user_avatar_type OWNER TO postgres;

--
-- Name: user_badge; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_badge (
    user_id bigint NOT NULL,
    badge_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_badge OWNER TO postgres;

--
-- Name: user_ban; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_ban (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    author_user_id bigint NOT NULL,
    reason character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    expired_at timestamp with time zone,
    internal_reason character varying(4096) DEFAULT NULL::character varying
);


ALTER TABLE public.user_ban OWNER TO postgres;

--
-- Name: user_ban_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_ban_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_ban_id_seq OWNER TO postgres;

--
-- Name: user_ban_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_ban_id_seq OWNED BY public.user_ban.id;


--
-- Name: user_conversation; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_conversation (
    id bigint NOT NULL,
    title character varying(255) DEFAULT NULL::character varying,
    creator_id bigint NOT NULL,
    conversation_type integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_conversation OWNER TO postgres;

--
-- Name: user_conversation_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_conversation_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_conversation_id_seq OWNER TO postgres;

--
-- Name: user_conversation_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_conversation_id_seq OWNED BY public.user_conversation.id;


--
-- Name: user_conversation_message; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_conversation_message (
    id character varying(64) NOT NULL,
    conversation_id bigint NOT NULL,
    user_id bigint NOT NULL,
    message text NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_conversation_message OWNER TO postgres;

--
-- Name: user_conversation_message_read; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_conversation_message_read (
    conversation_id bigint NOT NULL,
    user_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_conversation_message_read OWNER TO postgres;

--
-- Name: user_conversation_participant; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_conversation_participant (
    conversation_id bigint NOT NULL,
    user_id bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_conversation_participant OWNER TO postgres;

--
-- Name: user_discord_links; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_discord_links (
    user_id bigint NOT NULL,
    discord_id character varying(64) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);


ALTER TABLE public.user_discord_links OWNER TO postgres;

--
-- Name: user_economy; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_economy (
    user_id bigint NOT NULL,
    balance_robux integer NOT NULL,
    balance_tickets integer NOT NULL
);


ALTER TABLE public.user_economy OWNER TO postgres;

--
-- Name: user_email; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_email (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    email character varying(255) NOT NULL,
    status integer DEFAULT 1 NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_email OWNER TO postgres;

--
-- Name: user_email_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_email_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_email_id_seq OWNER TO postgres;

--
-- Name: user_email_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_email_id_seq OWNED BY public.user_email.id;


--
-- Name: user_following; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_following (
    id bigint NOT NULL,
    user_id_being_followed bigint NOT NULL,
    user_id_who_is_following bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_following OWNER TO postgres;

--
-- Name: user_following_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_following_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_following_id_seq OWNER TO postgres;

--
-- Name: user_following_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_following_id_seq OWNED BY public.user_following.id;


--
-- Name: user_friend; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_friend (
    id bigint NOT NULL,
    user_id_one bigint NOT NULL,
    user_id_two bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_friend OWNER TO postgres;

--
-- Name: user_friend_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_friend_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_friend_id_seq OWNER TO postgres;

--
-- Name: user_friend_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_friend_id_seq OWNED BY public.user_friend.id;


--
-- Name: user_friend_request; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_friend_request (
    id bigint NOT NULL,
    user_id_one bigint NOT NULL,
    user_id_two bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_friend_request OWNER TO postgres;

--
-- Name: user_friend_request_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_friend_request_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_friend_request_id_seq OWNER TO postgres;

--
-- Name: user_friend_request_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_friend_request_id_seq OWNED BY public.user_friend_request.id;


--
-- Name: user_hashed_ips; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_hashed_ips (
    user_id bigint NOT NULL,
    hashed_ip text NOT NULL,
    last_seen timestamp without time zone NOT NULL,
    block_status integer DEFAULT 0 NOT NULL,
    poisoned boolean DEFAULT false NOT NULL
);


ALTER TABLE public.user_hashed_ips OWNER TO postgres;

--
-- Name: user_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_id_seq OWNER TO postgres;

--
-- Name: user_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_id_seq OWNED BY public."user".id;


--
-- Name: user_invite; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_invite (
    id character varying(128) NOT NULL,
    user_id bigint,
    author_id bigint,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_invite OWNER TO postgres;

--
-- Name: user_membership; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_membership (
    user_id bigint NOT NULL,
    membership_type integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_membership OWNER TO postgres;

--
-- Name: user_message; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_message (
    id bigint NOT NULL,
    user_id_from bigint NOT NULL,
    user_id_to bigint NOT NULL,
    is_read boolean,
    subject character varying(255) NOT NULL,
    body character varying(8192) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    is_archived boolean DEFAULT false NOT NULL
);


ALTER TABLE public.user_message OWNER TO postgres;

--
-- Name: user_message_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_message_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_message_id_seq OWNER TO postgres;

--
-- Name: user_message_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_message_id_seq OWNED BY public.user_message.id;


--
-- Name: user_outfit; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_outfit (
    id bigint NOT NULL,
    name character varying(255) NOT NULL,
    user_id bigint NOT NULL,
    thumbnail_url character varying(255),
    avatar_type integer DEFAULT 1 NOT NULL,
    scale_height double precision DEFAULT '1'::double precision NOT NULL,
    scale_width double precision DEFAULT '1'::double precision NOT NULL,
    scale_head double precision DEFAULT '1'::double precision NOT NULL,
    scale_depth double precision DEFAULT '1'::double precision NOT NULL,
    scale_proportion double precision DEFAULT '0'::double precision NOT NULL,
    scale_body_type double precision DEFAULT '0'::double precision NOT NULL,
    head_color_id integer NOT NULL,
    torso_color_id integer NOT NULL,
    right_arm_color_id integer NOT NULL,
    left_arm_color_id integer NOT NULL,
    right_leg_color_id integer NOT NULL,
    left_leg_color_id integer NOT NULL,
    headshot_thumbnail_url character varying(255),
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_outfit OWNER TO postgres;

--
-- Name: user_outfit_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_outfit_asset (
    outfit_id bigint NOT NULL,
    asset_id bigint NOT NULL
);


ALTER TABLE public.user_outfit_asset OWNER TO postgres;

--
-- Name: user_outfit_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_outfit_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_outfit_id_seq OWNER TO postgres;

--
-- Name: user_outfit_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_outfit_id_seq OWNED BY public.user_outfit.id;


--
-- Name: user_password_reset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_password_reset (
    id character varying(64) NOT NULL,
    user_id bigint NOT NULL,
    status integer NOT NULL,
    social_url character varying(1024) NOT NULL,
    verification_phrase character varying(1024) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_password_reset OWNER TO postgres;

--
-- Name: user_permission; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_permission (
    user_id bigint NOT NULL,
    permission integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_permission OWNER TO postgres;

--
-- Name: user_previous_username; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_previous_username (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    username character varying(255) NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_previous_username OWNER TO postgres;

--
-- Name: user_previous_username_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_previous_username_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_previous_username_id_seq OWNER TO postgres;

--
-- Name: user_previous_username_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_previous_username_id_seq OWNED BY public.user_previous_username.id;


--
-- Name: user_settings; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_settings (
    user_id bigint NOT NULL,
    inventory_privacy integer DEFAULT 1 NOT NULL,
    theme integer DEFAULT 1 NOT NULL,
    gender integer DEFAULT 3 NOT NULL,
    trade_privacy integer DEFAULT 1 NOT NULL,
    trade_filter integer DEFAULT 1 NOT NULL,
    private_message_privacy integer DEFAULT 1 NOT NULL,
    "2020_menu_enabled" integer DEFAULT 1 NOT NULL
);


ALTER TABLE public.user_settings OWNER TO postgres;

--
-- Name: user_signup_tokens; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_signup_tokens (
    token character varying(512) NOT NULL,
    user_id bigint,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    used_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    discord_id character varying(64)
);


ALTER TABLE public.user_signup_tokens OWNER TO postgres;

--
-- Name: user_status; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_status (
    id bigint NOT NULL,
    user_id bigint NOT NULL,
    status character varying(255),
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


ALTER TABLE public.user_status OWNER TO postgres;

--
-- Name: user_status_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_status_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_status_id_seq OWNER TO postgres;

--
-- Name: user_status_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_status_id_seq OWNED BY public.user_status.id;


--
-- Name: user_trade; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_trade (
    id bigint NOT NULL,
    user_id_one bigint NOT NULL,
    user_id_two bigint NOT NULL,
    user_id_one_robux bigint,
    user_id_two_robux bigint,
    status integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    expires_at timestamp with time zone NOT NULL
);


ALTER TABLE public.user_trade OWNER TO postgres;

--
-- Name: user_trade_asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_trade_asset (
    trade_id bigint NOT NULL,
    user_asset_id bigint NOT NULL,
    user_id bigint NOT NULL
);


ALTER TABLE public.user_trade_asset OWNER TO postgres;

--
-- Name: user_trade_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_trade_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_trade_id_seq OWNER TO postgres;

--
-- Name: user_trade_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_trade_id_seq OWNED BY public.user_trade.id;


--
-- Name: user_transaction; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_transaction (
    id bigint NOT NULL,
    type integer NOT NULL,
    currency_type integer NOT NULL,
    amount bigint NOT NULL,
    user_id_one bigint NOT NULL,
    user_id_two bigint NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    asset_id bigint,
    user_asset_id bigint,
    sub_type integer,
    old_username character varying(255) DEFAULT NULL::character varying,
    new_username character varying(255) DEFAULT NULL::character varying,
    group_id_one bigint,
    group_id_two bigint
);


ALTER TABLE public.user_transaction OWNER TO postgres;

--
-- Name: user_transaction_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.user_transaction_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.user_transaction_id_seq OWNER TO postgres;

--
-- Name: user_transaction_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.user_transaction_id_seq OWNED BY public.user_transaction.id;


--
-- Name: asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset ALTER COLUMN id SET DEFAULT nextval('public.asset_id_seq'::regclass);


--
-- Name: asset_advertisement id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_advertisement ALTER COLUMN id SET DEFAULT nextval('public.asset_advertisement_id_seq'::regclass);


--
-- Name: asset_comment id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_comment ALTER COLUMN id SET DEFAULT nextval('public.asset_comment_id_seq'::regclass);


--
-- Name: asset_datastore id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_datastore ALTER COLUMN id SET DEFAULT nextval('public.asset_datastore_id_seq'::regclass);


--
-- Name: asset_favorite id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_favorite ALTER COLUMN id SET DEFAULT nextval('public.asset_favorite_id_seq'::regclass);


--
-- Name: asset_icon id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_icon ALTER COLUMN id SET DEFAULT nextval('public.asset_icon_id_seq'::regclass);


--
-- Name: asset_media id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_media ALTER COLUMN id SET DEFAULT nextval('public.asset_media_id_seq'::regclass);


--
-- Name: asset_play_history id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_play_history ALTER COLUMN id SET DEFAULT nextval('public.asset_play_history_id_seq'::regclass);


--
-- Name: asset_version id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_version ALTER COLUMN id SET DEFAULT nextval('public.asset_version_id_seq'::regclass);


--
-- Name: asset_vote id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_vote ALTER COLUMN id SET DEFAULT nextval('public.asset_vote_id_seq'::regclass);


--
-- Name: collectible_sale_logs id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.collectible_sale_logs ALTER COLUMN id SET DEFAULT nextval('public.collectible_sale_logs_id_seq'::regclass);


--
-- Name: forum_post id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.forum_post ALTER COLUMN id SET DEFAULT nextval('public.forum_post_id_seq'::regclass);


--
-- Name: group id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."group" ALTER COLUMN id SET DEFAULT nextval('public.group_id_seq'::regclass);


--
-- Name: group_audit_log id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_audit_log ALTER COLUMN id SET DEFAULT nextval('public.group_audit_log_id_seq'::regclass);


--
-- Name: group_role id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_role ALTER COLUMN id SET DEFAULT nextval('public.group_role_id_seq'::regclass);


--
-- Name: group_social_link id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_social_link ALTER COLUMN id SET DEFAULT nextval('public.group_social_link_id_seq'::regclass);


--
-- Name: group_status id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_status ALTER COLUMN id SET DEFAULT nextval('public.group_status_id_seq'::regclass);


--
-- Name: group_user id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_user ALTER COLUMN id SET DEFAULT nextval('public.group_user_id_seq'::regclass);


--
-- Name: group_wall id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_wall ALTER COLUMN id SET DEFAULT nextval('public.group_wall_id_seq'::regclass);


--
-- Name: knex_migrations id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.knex_migrations ALTER COLUMN id SET DEFAULT nextval('public.knex_migrations_id_seq'::regclass);


--
-- Name: knex_migrations_lock index; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.knex_migrations_lock ALTER COLUMN index SET DEFAULT nextval('public.knex_migrations_lock_index_seq'::regclass);


--
-- Name: moderation_admin_message id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_admin_message ALTER COLUMN id SET DEFAULT nextval('public.moderation_admin_message_id_seq'::regclass);


--
-- Name: moderation_bad_username id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_bad_username ALTER COLUMN id SET DEFAULT nextval('public.moderation_bad_username_id_seq'::regclass);


--
-- Name: moderation_bad_username_log id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_bad_username_log ALTER COLUMN id SET DEFAULT nextval('public.moderation_bad_username_log_id_seq'::regclass);


--
-- Name: moderation_ban id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_ban ALTER COLUMN id SET DEFAULT nextval('public.moderation_ban_id_seq'::regclass);


--
-- Name: moderation_change_join_app id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_change_join_app ALTER COLUMN id SET DEFAULT nextval('public.moderation_change_join_app_id_seq'::regclass);


--
-- Name: moderation_give_item id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_give_item ALTER COLUMN id SET DEFAULT nextval('public.moderation_give_item_id_seq'::regclass);


--
-- Name: moderation_give_robux id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_give_robux ALTER COLUMN id SET DEFAULT nextval('public.moderation_give_robux_id_seq'::regclass);


--
-- Name: moderation_give_tickets id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_give_tickets ALTER COLUMN id SET DEFAULT nextval('public.moderation_give_tickets_id_seq'::regclass);


--
-- Name: moderation_manage_asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_manage_asset ALTER COLUMN id SET DEFAULT nextval('public.moderation_manage_asset_id_seq'::regclass);


--
-- Name: moderation_migrate_asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_migrate_asset ALTER COLUMN id SET DEFAULT nextval('public.moderation_migrate_asset_id_seq'::regclass);


--
-- Name: moderation_modify_asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_modify_asset ALTER COLUMN id SET DEFAULT nextval('public.moderation_modify_asset_id_seq'::regclass);


--
-- Name: moderation_overwrite_thumbnail id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_overwrite_thumbnail ALTER COLUMN id SET DEFAULT nextval('public.moderation_overwrite_thumbnail_id_seq'::regclass);


--
-- Name: moderation_purchase_resale_asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_purchase_resale_asset ALTER COLUMN id SET DEFAULT nextval('public.moderation_purchase_resale_asset_id_seq'::regclass);


--
-- Name: moderation_refund_transaction id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_refund_transaction ALTER COLUMN id SET DEFAULT nextval('public.moderation_refund_transaction_id_seq'::regclass);


--
-- Name: moderation_reset_password id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_reset_password ALTER COLUMN id SET DEFAULT nextval('public.moderation_reset_password_id_seq'::regclass);


--
-- Name: moderation_sell_asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_sell_asset ALTER COLUMN id SET DEFAULT nextval('public.moderation_sell_asset_id_seq'::regclass);


--
-- Name: moderation_set_alert id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_set_alert ALTER COLUMN id SET DEFAULT nextval('public.moderation_set_alert_id_seq'::regclass);


--
-- Name: moderation_set_rap id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_set_rap ALTER COLUMN id SET DEFAULT nextval('public.moderation_set_rap_id_seq'::regclass);


--
-- Name: moderation_transcripts id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_transcripts ALTER COLUMN id SET DEFAULT nextval('public.moderation_transcripts_id_seq'::regclass);


--
-- Name: moderation_unban id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_unban ALTER COLUMN id SET DEFAULT nextval('public.moderation_unban_id_seq'::regclass);


--
-- Name: moderation_update_product id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_update_product ALTER COLUMN id SET DEFAULT nextval('public.moderation_update_product_id_seq'::regclass);


--
-- Name: moderation_user_ban id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_user_ban ALTER COLUMN id SET DEFAULT nextval('public.moderation_user_ban_id_seq'::regclass);


--
-- Name: promocode_redemptions id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.promocode_redemptions ALTER COLUMN id SET DEFAULT nextval('public.promocode_redemptions_id_seq'::regclass);


--
-- Name: promocodes id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.promocodes ALTER COLUMN id SET DEFAULT nextval('public.promocodes_id_seq'::regclass);


--
-- Name: trade_currency_log id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trade_currency_log ALTER COLUMN id SET DEFAULT nextval('public.trade_currency_log_id_seq'::regclass);


--
-- Name: trade_currency_order id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trade_currency_order ALTER COLUMN id SET DEFAULT nextval('public.trade_currency_order_id_seq'::regclass);


--
-- Name: universe id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.universe ALTER COLUMN id SET DEFAULT nextval('public.universe_id_seq'::regclass);


--
-- Name: user id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."user" ALTER COLUMN id SET DEFAULT nextval('public.user_id_seq'::regclass);


--
-- Name: user_asset id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_asset ALTER COLUMN id SET DEFAULT nextval('public.user_asset_id_seq'::regclass);


--
-- Name: user_ban id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_ban ALTER COLUMN id SET DEFAULT nextval('public.user_ban_id_seq'::regclass);


--
-- Name: user_conversation id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_conversation ALTER COLUMN id SET DEFAULT nextval('public.user_conversation_id_seq'::regclass);


--
-- Name: user_email id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_email ALTER COLUMN id SET DEFAULT nextval('public.user_email_id_seq'::regclass);


--
-- Name: user_following id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_following ALTER COLUMN id SET DEFAULT nextval('public.user_following_id_seq'::regclass);


--
-- Name: user_friend id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_friend ALTER COLUMN id SET DEFAULT nextval('public.user_friend_id_seq'::regclass);


--
-- Name: user_friend_request id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_friend_request ALTER COLUMN id SET DEFAULT nextval('public.user_friend_request_id_seq'::regclass);


--
-- Name: user_message id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_message ALTER COLUMN id SET DEFAULT nextval('public.user_message_id_seq'::regclass);


--
-- Name: user_outfit id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_outfit ALTER COLUMN id SET DEFAULT nextval('public.user_outfit_id_seq'::regclass);


--
-- Name: user_previous_username id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_previous_username ALTER COLUMN id SET DEFAULT nextval('public.user_previous_username_id_seq'::regclass);


--
-- Name: user_status id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_status ALTER COLUMN id SET DEFAULT nextval('public.user_status_id_seq'::regclass);


--
-- Name: user_trade id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_trade ALTER COLUMN id SET DEFAULT nextval('public.user_trade_id_seq'::regclass);


--
-- Name: user_transaction id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_transaction ALTER COLUMN id SET DEFAULT nextval('public.user_transaction_id_seq'::regclass);


--
-- Name: abuse_report abuse_report_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.abuse_report
    ADD CONSTRAINT abuse_report_id_unique UNIQUE (id);


--
-- Name: asset_advertisement asset_advertisement_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_advertisement
    ADD CONSTRAINT asset_advertisement_pkey PRIMARY KEY (id);


--
-- Name: asset_comment asset_comment_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_comment
    ADD CONSTRAINT asset_comment_pkey PRIMARY KEY (id);


--
-- Name: asset_datastore asset_datastore_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_datastore
    ADD CONSTRAINT asset_datastore_pkey PRIMARY KEY (id);


--
-- Name: asset_favorite asset_favorite_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_favorite
    ADD CONSTRAINT asset_favorite_pkey PRIMARY KEY (id);


--
-- Name: asset_favorite asset_favorite_user_id_asset_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_favorite
    ADD CONSTRAINT asset_favorite_user_id_asset_id_unique UNIQUE (user_id, asset_id);


--
-- Name: asset_icon asset_icon_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_icon
    ADD CONSTRAINT asset_icon_pkey PRIMARY KEY (id);


--
-- Name: asset_media asset_media_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_media
    ADD CONSTRAINT asset_media_pkey PRIMARY KEY (id);


--
-- Name: asset asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset
    ADD CONSTRAINT asset_pkey PRIMARY KEY (id);


--
-- Name: asset_play_history asset_play_history_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_play_history
    ADD CONSTRAINT asset_play_history_pkey PRIMARY KEY (id);


--
-- Name: asset_server asset_server_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_server
    ADD CONSTRAINT asset_server_pkey PRIMARY KEY (id);


--
-- Name: asset_version_metadata_image asset_version_metadata_image_asset_version_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_version_metadata_image
    ADD CONSTRAINT asset_version_metadata_image_asset_version_id_unique UNIQUE (asset_version_id);


--
-- Name: asset_version asset_version_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_version
    ADD CONSTRAINT asset_version_pkey PRIMARY KEY (id);


--
-- Name: asset_vote asset_vote_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.asset_vote
    ADD CONSTRAINT asset_vote_pkey PRIMARY KEY (id);


--
-- Name: collectible_sale_logs collectible_sale_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.collectible_sale_logs
    ADD CONSTRAINT collectible_sale_logs_pkey PRIMARY KEY (id);


--
-- Name: forum_post forum_post_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.forum_post
    ADD CONSTRAINT forum_post_pkey PRIMARY KEY (id);


--
-- Name: forum_post_read forum_post_read_forum_post_id_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.forum_post_read
    ADD CONSTRAINT forum_post_read_forum_post_id_user_id_unique UNIQUE (forum_post_id, user_id);


--
-- Name: group_audit_log group_audit_log_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_audit_log
    ADD CONSTRAINT group_audit_log_pkey PRIMARY KEY (id);


--
-- Name: group_economy group_economy_group_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_economy
    ADD CONSTRAINT group_economy_group_id_unique UNIQUE (group_id);


--
-- Name: group_icon group_icon_group_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_icon
    ADD CONSTRAINT group_icon_group_id_unique UNIQUE (group_id);


--
-- Name: group group_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."group"
    ADD CONSTRAINT group_pkey PRIMARY KEY (id);


--
-- Name: group_role group_role_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_role
    ADD CONSTRAINT group_role_pkey PRIMARY KEY (id);


--
-- Name: group_settings group_settings_group_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_settings
    ADD CONSTRAINT group_settings_group_id_unique UNIQUE (group_id);


--
-- Name: group_social_link group_social_link_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_social_link
    ADD CONSTRAINT group_social_link_pkey PRIMARY KEY (id);


--
-- Name: group_status group_status_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_status
    ADD CONSTRAINT group_status_pkey PRIMARY KEY (id);


--
-- Name: group_user group_user_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_user
    ADD CONSTRAINT group_user_pkey PRIMARY KEY (id);


--
-- Name: group_wall group_wall_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.group_wall
    ADD CONSTRAINT group_wall_pkey PRIMARY KEY (id);


--
-- Name: knex_migrations_lock knex_migrations_lock_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.knex_migrations_lock
    ADD CONSTRAINT knex_migrations_lock_pkey PRIMARY KEY (index);


--
-- Name: knex_migrations knex_migrations_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.knex_migrations
    ADD CONSTRAINT knex_migrations_pkey PRIMARY KEY (id);


--
-- Name: moderation_admin_message moderation_admin_message_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_admin_message
    ADD CONSTRAINT moderation_admin_message_pkey PRIMARY KEY (id);


--
-- Name: moderation_bad_username_log moderation_bad_username_log_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_bad_username_log
    ADD CONSTRAINT moderation_bad_username_log_pkey PRIMARY KEY (id);


--
-- Name: moderation_bad_username moderation_bad_username_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_bad_username
    ADD CONSTRAINT moderation_bad_username_pkey PRIMARY KEY (id);


--
-- Name: moderation_ban moderation_ban_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_ban
    ADD CONSTRAINT moderation_ban_pkey PRIMARY KEY (id);


--
-- Name: moderation_change_join_app moderation_change_join_app_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_change_join_app
    ADD CONSTRAINT moderation_change_join_app_pkey PRIMARY KEY (id);


--
-- Name: moderation_give_item moderation_give_item_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_give_item
    ADD CONSTRAINT moderation_give_item_pkey PRIMARY KEY (id);


--
-- Name: moderation_give_robux moderation_give_robux_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_give_robux
    ADD CONSTRAINT moderation_give_robux_pkey PRIMARY KEY (id);


--
-- Name: moderation_give_tickets moderation_give_tickets_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_give_tickets
    ADD CONSTRAINT moderation_give_tickets_pkey PRIMARY KEY (id);


--
-- Name: moderation_manage_asset moderation_manage_asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_manage_asset
    ADD CONSTRAINT moderation_manage_asset_pkey PRIMARY KEY (id);


--
-- Name: moderation_migrate_asset moderation_migrate_asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_migrate_asset
    ADD CONSTRAINT moderation_migrate_asset_pkey PRIMARY KEY (id);


--
-- Name: moderation_modify_asset moderation_modify_asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_modify_asset
    ADD CONSTRAINT moderation_modify_asset_pkey PRIMARY KEY (id);


--
-- Name: moderation_overwrite_thumbnail moderation_overwrite_thumbnail_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_overwrite_thumbnail
    ADD CONSTRAINT moderation_overwrite_thumbnail_pkey PRIMARY KEY (id);


--
-- Name: moderation_purchase_resale_asset moderation_purchase_resale_asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_purchase_resale_asset
    ADD CONSTRAINT moderation_purchase_resale_asset_pkey PRIMARY KEY (id);


--
-- Name: moderation_refund_transaction moderation_refund_transaction_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_refund_transaction
    ADD CONSTRAINT moderation_refund_transaction_pkey PRIMARY KEY (id);


--
-- Name: moderation_reset_password moderation_reset_password_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_reset_password
    ADD CONSTRAINT moderation_reset_password_pkey PRIMARY KEY (id);


--
-- Name: moderation_sell_asset moderation_sell_asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_sell_asset
    ADD CONSTRAINT moderation_sell_asset_pkey PRIMARY KEY (id);


--
-- Name: moderation_set_alert moderation_set_alert_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_set_alert
    ADD CONSTRAINT moderation_set_alert_pkey PRIMARY KEY (id);


--
-- Name: moderation_set_rap moderation_set_rap_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_set_rap
    ADD CONSTRAINT moderation_set_rap_pkey PRIMARY KEY (id);


--
-- Name: moderation_transcripts moderation_transcripts_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_transcripts
    ADD CONSTRAINT moderation_transcripts_pkey PRIMARY KEY (id);


--
-- Name: moderation_unban moderation_unban_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_unban
    ADD CONSTRAINT moderation_unban_pkey PRIMARY KEY (id);


--
-- Name: moderation_update_product moderation_update_product_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_update_product
    ADD CONSTRAINT moderation_update_product_pkey PRIMARY KEY (id);


--
-- Name: moderation_user_ban moderation_user_ban_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_user_ban
    ADD CONSTRAINT moderation_user_ban_pkey PRIMARY KEY (id);


--
-- Name: password_reset_tokens password_reset_tokens_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.password_reset_tokens
    ADD CONSTRAINT password_reset_tokens_pkey PRIMARY KEY (user_id, token);


--
-- Name: promocode_redemptions promocode_redemptions_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.promocode_redemptions
    ADD CONSTRAINT promocode_redemptions_pkey PRIMARY KEY (id);


--
-- Name: promocodes promocodes_code_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.promocodes
    ADD CONSTRAINT promocodes_code_key UNIQUE (code);


--
-- Name: promocodes promocodes_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.promocodes
    ADD CONSTRAINT promocodes_pkey PRIMARY KEY (id);


--
-- Name: trade_currency_log trade_currency_log_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trade_currency_log
    ADD CONSTRAINT trade_currency_log_pkey PRIMARY KEY (id);


--
-- Name: trade_currency_order trade_currency_order_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.trade_currency_order
    ADD CONSTRAINT trade_currency_order_pkey PRIMARY KEY (id);


--
-- Name: password_reset_tokens unique_token; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.password_reset_tokens
    ADD CONSTRAINT unique_token UNIQUE (token);


--
-- Name: universe universe_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.universe
    ADD CONSTRAINT universe_pkey PRIMARY KEY (id);


--
-- Name: user_asset user_asset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_asset
    ADD CONSTRAINT user_asset_pkey PRIMARY KEY (id);


--
-- Name: user_avatar_type user_avatar_type_user_id_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_avatar_type
    ADD CONSTRAINT user_avatar_type_user_id_key UNIQUE (user_id);


--
-- Name: user_avatar user_avatar_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_avatar
    ADD CONSTRAINT user_avatar_user_id_unique UNIQUE (user_id);


--
-- Name: user_ban user_ban_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_ban
    ADD CONSTRAINT user_ban_pkey PRIMARY KEY (id);


--
-- Name: user_conversation_message user_conversation_message_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_conversation_message
    ADD CONSTRAINT user_conversation_message_id_unique UNIQUE (id);


--
-- Name: user_conversation_message_read user_conversation_message_read_conversation_id_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_conversation_message_read
    ADD CONSTRAINT user_conversation_message_read_conversation_id_user_id_unique UNIQUE (conversation_id, user_id);


--
-- Name: user_conversation_participant user_conversation_participant_conversation_id_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_conversation_participant
    ADD CONSTRAINT user_conversation_participant_conversation_id_user_id_unique UNIQUE (conversation_id, user_id);


--
-- Name: user_conversation user_conversation_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_conversation
    ADD CONSTRAINT user_conversation_pkey PRIMARY KEY (id);


--
-- Name: user_discord_links user_discord_links_discord_id_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_discord_links
    ADD CONSTRAINT user_discord_links_discord_id_key UNIQUE (discord_id);


--
-- Name: user_discord_links user_discord_links_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_discord_links
    ADD CONSTRAINT user_discord_links_pkey PRIMARY KEY (user_id);


--
-- Name: user_economy user_economy_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_economy
    ADD CONSTRAINT user_economy_user_id_unique UNIQUE (user_id);


--
-- Name: user_email user_email_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_email
    ADD CONSTRAINT user_email_pkey PRIMARY KEY (id);


--
-- Name: user_following user_following_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_following
    ADD CONSTRAINT user_following_pkey PRIMARY KEY (id);


--
-- Name: user_friend user_friend_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_friend
    ADD CONSTRAINT user_friend_pkey PRIMARY KEY (id);


--
-- Name: user_friend_request user_friend_request_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_friend_request
    ADD CONSTRAINT user_friend_request_pkey PRIMARY KEY (id);


--
-- Name: user_hashed_ips user_hashed_ips_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_hashed_ips
    ADD CONSTRAINT user_hashed_ips_pkey PRIMARY KEY (user_id);


--
-- Name: user_membership user_membership_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_membership
    ADD CONSTRAINT user_membership_user_id_unique UNIQUE (user_id);


--
-- Name: user_message user_message_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_message
    ADD CONSTRAINT user_message_pkey PRIMARY KEY (id);


--
-- Name: user_outfit user_outfit_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_outfit
    ADD CONSTRAINT user_outfit_id_unique UNIQUE (id);


--
-- Name: user_outfit user_outfit_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_outfit
    ADD CONSTRAINT user_outfit_pkey PRIMARY KEY (id);


--
-- Name: user_password_reset user_password_reset_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_password_reset
    ADD CONSTRAINT user_password_reset_pkey PRIMARY KEY (id);


--
-- Name: user_permission user_permission_user_id_permission_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_permission
    ADD CONSTRAINT user_permission_user_id_permission_unique UNIQUE (user_id, permission);


--
-- Name: user user_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."user"
    ADD CONSTRAINT user_pkey PRIMARY KEY (id);


--
-- Name: user_previous_username user_previous_username_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_previous_username
    ADD CONSTRAINT user_previous_username_pkey PRIMARY KEY (id);


--
-- Name: user_settings user_settings_user_id_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_settings
    ADD CONSTRAINT user_settings_user_id_unique UNIQUE (user_id);


--
-- Name: user_signup_tokens user_signup_tokens_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_signup_tokens
    ADD CONSTRAINT user_signup_tokens_pkey PRIMARY KEY (token);


--
-- Name: user_status user_status_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_status
    ADD CONSTRAINT user_status_pkey PRIMARY KEY (id);


--
-- Name: user_trade user_trade_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_trade
    ADD CONSTRAINT user_trade_pkey PRIMARY KEY (id);


--
-- Name: user_transaction user_transaction_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_transaction
    ADD CONSTRAINT user_transaction_pkey PRIMARY KEY (id);


--
-- Name: user user_username_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."user"
    ADD CONSTRAINT user_username_unique UNIQUE (username);


--
-- Name: asset_advertisement_target_id_target_type_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_advertisement_target_id_target_type_index ON public.asset_advertisement USING btree (target_id, target_type);


--
-- Name: asset_advertisement_updated_at_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_advertisement_updated_at_index ON public.asset_advertisement USING btree (updated_at);


--
-- Name: asset_asset_type_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_asset_type_index ON public.asset USING btree (asset_type);


--
-- Name: asset_comment_asset_id_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_comment_asset_id_id_index ON public.asset_comment USING btree (asset_id, id);


--
-- Name: asset_comment_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_comment_user_id_index ON public.asset_comment USING btree (user_id);


--
-- Name: asset_favorite_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_favorite_asset_id_index ON public.asset_favorite USING btree (asset_id);


--
-- Name: asset_favorite_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_favorite_user_id_index ON public.asset_favorite USING btree (user_id);


--
-- Name: asset_icon_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_icon_asset_id_index ON public.asset_icon USING btree (asset_id);


--
-- Name: asset_is_for_sale_creator_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_is_for_sale_creator_idx ON public.asset USING btree (creator_id, creator_type) WHERE (is_for_sale OR is_limited);


--
-- Name: asset_is_for_sale_creator_type_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_is_for_sale_creator_type_idx ON public.asset USING btree (creator_id, creator_type, asset_type) WHERE (is_for_sale OR is_limited);


--
-- Name: asset_is_limited_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_is_limited_index ON public.asset USING btree (is_limited);


--
-- Name: asset_roblox_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_roblox_asset_id_index ON public.asset USING btree (roblox_asset_id);


--
-- Name: asset_server_player_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_server_player_asset_id_index ON public.asset_server_player USING btree (asset_id);


--
-- Name: asset_server_player_server_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_server_player_server_id_index ON public.asset_server_player USING btree (server_id);


--
-- Name: asset_version_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX asset_version_asset_id_index ON public.asset_version USING btree (asset_id);


--
-- Name: collectible_sale_logs_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX collectible_sale_logs_asset_id_index ON public.collectible_sale_logs USING btree (asset_id);


--
-- Name: forum_post_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_id_index ON public.forum_post USING btree (id);


--
-- Name: forum_post_sub_category_id_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_sub_category_id_id_index ON public.forum_post USING btree (sub_category_id, id);


--
-- Name: forum_post_subcategory_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_subcategory_id ON public.forum_post USING btree (sub_category_id);


--
-- Name: forum_post_subcategory_id_id_desc; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_subcategory_id_id_desc ON public.forum_post USING btree (sub_category_id, id DESC);


--
-- Name: forum_post_thread_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_thread_id ON public.forum_post USING btree (thread_id) WHERE (thread_id IS NOT NULL);


--
-- Name: forum_post_thread_id_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_thread_id_id_index ON public.forum_post USING btree (thread_id, id);


--
-- Name: forum_post_user_id_created_at_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX forum_post_user_id_created_at_index ON public.forum_post USING btree (user_id, created_at);


--
-- Name: group_name_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_name_index ON public."group" USING btree (name);


--
-- Name: group_role_group_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_role_group_id_index ON public.group_role USING btree (group_id);


--
-- Name: group_social_link_group_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_social_link_group_id_index ON public.group_social_link USING btree (group_id);


--
-- Name: group_status_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_status_user_id_index ON public.group_status USING btree (user_id);


--
-- Name: group_user_group_role_id_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_user_group_role_id_id_index ON public.group_user USING btree (group_role_id, id);


--
-- Name: group_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_user_id_index ON public."group" USING btree (user_id);


--
-- Name: group_user_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_user_user_id_index ON public.group_user USING btree (user_id);


--
-- Name: group_wall_id_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX group_wall_id_idx ON public.group_wall USING btree (group_id, id) WHERE (is_deleted IS FALSE);


--
-- Name: idx_user_signup_tokens_discord_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_user_signup_tokens_discord_id ON public.user_signup_tokens USING btree (discord_id);


--
-- Name: idx_user_signup_tokens_user_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX idx_user_signup_tokens_user_id ON public.user_signup_tokens USING btree (user_id);


--
-- Name: idx_user_username_lower; Type: INDEX; Schema: public; Owner: postgres
--

CREATE UNIQUE INDEX idx_user_username_lower ON public."user" USING btree (lower((username)::text));


--
-- Name: moderation_bad_username_username_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX moderation_bad_username_username_index ON public.moderation_bad_username USING btree (username);


--
-- Name: moderation_sell_asset_asset_id_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX moderation_sell_asset_asset_id_idx ON public.moderation_sell_asset USING btree (asset_id);


--
-- Name: moderation_sell_asset_created_at_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX moderation_sell_asset_created_at_idx ON public.moderation_sell_asset USING btree (created_at);


--
-- Name: moderation_sell_asset_user_asset_id_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX moderation_sell_asset_user_asset_id_idx ON public.moderation_sell_asset USING btree (user_asset_id);


--
-- Name: moderation_sell_asset_user_id_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX moderation_sell_asset_user_id_idx ON public.moderation_sell_asset USING btree (user_id);


--
-- Name: trx_user_asset_id_idx; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX trx_user_asset_id_idx ON public.user_transaction USING btree (user_asset_id) WHERE (user_asset_id IS NOT NULL);


--
-- Name: universe_asset_asset_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX universe_asset_asset_id_index ON public.universe_asset USING btree (asset_id);


--
-- Name: universe_asset_universe_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX universe_asset_universe_id_index ON public.universe_asset USING btree (universe_id);


--
-- Name: user_avatar_asset_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_avatar_asset_user_id_index ON public.user_avatar_asset USING btree (user_id);


--
-- Name: user_avatar_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_avatar_user_id_index ON public.user_avatar USING btree (user_id);


--
-- Name: user_badge_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_badge_user_id_index ON public.user_badge USING btree (user_id);


--
-- Name: user_ban_author_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_ban_author_user_id_index ON public.user_ban USING btree (author_user_id);


--
-- Name: user_ban_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_ban_user_id_index ON public.user_ban USING btree (user_id);


--
-- Name: user_conversation_message_conversation_id_created_at_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_conversation_message_conversation_id_created_at_index ON public.user_conversation_message USING btree (conversation_id, created_at);


--
-- Name: user_conversation_message_conversation_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_conversation_message_conversation_id_index ON public.user_conversation_message USING btree (conversation_id);


--
-- Name: user_conversation_participant_conversation_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_conversation_participant_conversation_id_index ON public.user_conversation_participant USING btree (conversation_id);


--
-- Name: user_conversation_participant_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_conversation_participant_user_id_index ON public.user_conversation_participant USING btree (user_id);


--
-- Name: user_economy_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_economy_user_id_index ON public.user_economy USING btree (user_id);


--
-- Name: user_email_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_email_user_id_index ON public.user_email USING btree (user_id);


--
-- Name: user_email_user_id_status_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_email_user_id_status_index ON public.user_email USING btree (user_id, status);


--
-- Name: user_following_user_id_being_followed_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_following_user_id_being_followed_index ON public.user_following USING btree (user_id_being_followed);


--
-- Name: user_following_user_id_who_is_following_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_following_user_id_who_is_following_index ON public.user_following USING btree (user_id_who_is_following);


--
-- Name: user_friend_request_user_id_one_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_friend_request_user_id_one_index ON public.user_friend_request USING btree (user_id_one);


--
-- Name: user_friend_request_user_id_two_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_friend_request_user_id_two_index ON public.user_friend_request USING btree (user_id_two);


--
-- Name: user_friend_user_id_one_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_friend_user_id_one_index ON public.user_friend USING btree (user_id_one);


--
-- Name: user_friend_user_id_two_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_friend_user_id_two_index ON public.user_friend USING btree (user_id_two);


--
-- Name: user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_id_index ON public."user" USING btree (id);


--
-- Name: user_message_user_id_from_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_message_user_id_from_index ON public.user_message USING btree (user_id_from);


--
-- Name: user_message_user_id_to_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_message_user_id_to_index ON public.user_message USING btree (user_id_to);


--
-- Name: user_outfit_asset_outfit_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_outfit_asset_outfit_id_index ON public.user_outfit_asset USING btree (outfit_id);


--
-- Name: user_outfit_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_outfit_user_id_index ON public.user_outfit USING btree (user_id);


--
-- Name: user_permission_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_permission_user_id_index ON public.user_permission USING btree (user_id);


--
-- Name: user_previous_username_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_previous_username_user_id_index ON public.user_previous_username USING btree (user_id);


--
-- Name: user_previous_username_username_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_previous_username_username_index ON public.user_previous_username USING btree (username);


--
-- Name: user_settings_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_settings_user_id_index ON public.user_settings USING btree (user_id);


--
-- Name: user_status_user_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_status_user_id_index ON public.user_status USING btree (user_id);


--
-- Name: user_trade_asset_trade_id_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_trade_asset_trade_id_index ON public.user_trade_asset USING btree (trade_id);


--
-- Name: user_trade_user_id_one_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_trade_user_id_one_index ON public.user_trade USING btree (user_id_one);


--
-- Name: user_trade_user_id_two_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_trade_user_id_two_index ON public.user_trade USING btree (user_id_two);


--
-- Name: user_transaction_asset_type_sub_id; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_transaction_asset_type_sub_id ON public.user_transaction USING btree (asset_id) WHERE ((type = 1) AND (sub_type = 1));


--
-- Name: user_transaction_user_id_one_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_transaction_user_id_one_index ON public.user_transaction USING btree (user_id_one);


--
-- Name: user_transaction_user_id_two_index; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX user_transaction_user_id_two_index ON public.user_transaction USING btree (user_id_two);


--
-- Name: user_hashed_ips fk_user_id; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_hashed_ips
    ADD CONSTRAINT fk_user_id FOREIGN KEY (user_id) REFERENCES public."user"(id) ON DELETE CASCADE;


--
-- Name: moderation_reset_password moderation_reset_password_actor_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_reset_password
    ADD CONSTRAINT moderation_reset_password_actor_id_fkey FOREIGN KEY (actor_id) REFERENCES public."user"(id) ON DELETE CASCADE;


--
-- Name: moderation_reset_password moderation_reset_password_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_reset_password
    ADD CONSTRAINT moderation_reset_password_user_id_fkey FOREIGN KEY (user_id) REFERENCES public."user"(id) ON DELETE CASCADE;


--
-- Name: moderation_sell_asset moderation_sell_asset_asset_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_sell_asset
    ADD CONSTRAINT moderation_sell_asset_asset_id_fkey FOREIGN KEY (asset_id) REFERENCES public.asset(id) ON DELETE CASCADE;


--
-- Name: moderation_sell_asset moderation_sell_asset_user_asset_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_sell_asset
    ADD CONSTRAINT moderation_sell_asset_user_asset_id_fkey FOREIGN KEY (user_asset_id) REFERENCES public.user_asset(id) ON DELETE CASCADE;


--
-- Name: moderation_sell_asset moderation_sell_asset_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.moderation_sell_asset
    ADD CONSTRAINT moderation_sell_asset_user_id_fkey FOREIGN KEY (user_id) REFERENCES public."user"(id) ON DELETE CASCADE;


--
-- Name: password_reset_tokens password_reset_tokens_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.password_reset_tokens
    ADD CONSTRAINT password_reset_tokens_user_id_fkey FOREIGN KEY (user_id) REFERENCES public."user"(id) ON DELETE CASCADE;


--
-- Name: promocode_redemptions promocode_redemptions_promocode_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.promocode_redemptions
    ADD CONSTRAINT promocode_redemptions_promocode_id_fkey FOREIGN KEY (promocode) REFERENCES public.promocodes(id);


--
-- Name: user_discord_links user_discord_links_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_discord_links
    ADD CONSTRAINT user_discord_links_user_id_fkey FOREIGN KEY (user_id) REFERENCES public."user"(id) ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

