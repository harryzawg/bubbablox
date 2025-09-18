<script lang="ts">
	import { link } from "svelte-routing";
	import { LinkIcon, FileTextIcon, UserMinusIcon, TrashIcon, DownloadIcon } from "svelte-feather-icons";
	import moment from "../lib/moment";
	import Main from "../components/templates/Main.svelte";
	import Confirm from "../components/modal/Confirm.svelte";
	import request from "../lib/request";

	export let placeId: string;
	let placeInfo: any;
	let title: string = "";
	let errorMessage: string | undefined;
	let modalBody: string | undefined;
	let modalVisible = false;
	let modalCb: (arg1: boolean) => void;
	$: {
		placeInfo = request
			.get(`/games/${placeId}/details`)
			.then((d) => {
				title = `${d.data.placeName} (Place #${d.data.placeId})`;
				return d.data;
			});
	}
</script>

<svelte:head>
	<title>{title || "Place"}</title>
</svelte:head>

<Main>
	{#if modalVisible}
		<Confirm
			title="Confirm"
			message={modalBody}
			cb={(e) => {
				modalVisible = false;
				modalCb(e);
			}}
		/>
	{/if}

	{#await placeInfo}
		<div class="text-center">
			<div class="spinner-border" />
		</div>
	{:then info}
		<div class="row">
			<div class="col-12 col-lg-9">
				<div class="card mb-3">
					<div class="card card-body card-header">
						<h3 class="mb-0">
							{info.placeName}
						</h3>
					</div>
					<div class="card-body">
						<img class="img-fluid mb-2" src={`/thumbs/asset.ashx?assetId=${info.placeId}`} alt="Place" />
						<p class="fw-bold">Created by: <a use:link href={`/admin/manage-user/${info.creatorId}`}>{info.creatorName}</a></p>
						<p>{info.description || "No description provided."}</p>
						<p>
							<span class="badge bg-success">{info.visits.toLocaleString()} Visits</span>
							<span class="badge bg-primary">{info.currentPlayers} Players</span>
							<span class="badge bg-dark">Year: {info.year}</span>
						</p>
						<p class="mb-0">Last updated: {moment(info.created_at).format("MMM DD YYYY, h:mm A")}</p>
					</div>
				</div>
			</div>
			<div class="col-12 col-md-6 col-lg-3">
				<div class="card mb-2">
					<div class="card-body card-header"><h4 class="mb-0">Place Actions</h4></div>
				</div>
				<button
					class="btn-outline-dark btn w-100 mb-2"
					on:click={() => {
						modalBody = "Please confirm the reset of this place's name.";
						modalCb = (t) => {
							if (t) {
								request.post(`/games/${placeId}/reset-name`).then(() => window.location.reload()).catch((err) => errorMessage = err.message);
							}
						};
						modalVisible = true;
					}}
				>
					<UserMinusIcon /> Reset Place Name
				</button>
				<button
					class="btn-outline-dark btn w-100 mb-2"
					on:click={() => {
						modalBody = "Please confirm the reset of this place's description.";
						modalCb = (t) => {
							if (t) {
								request.post(`/games/${placeId}/reset-description`).then(() => window.location.reload()).catch((err) => errorMessage = err.message);
							}
						};
						modalVisible = true;
					}}
				>
					<FileTextIcon /> Reset Description
				</button>
				<button
					class="btn-outline-danger btn w-100 mb-2"
					on:click={() => {
						modalBody = "This will permanently delete the place. Continue?";
						modalCb = (t) => {
							if (t) {
								request.delete(`/games/${placeId}/delete`).then(() => {
									window.location.href = "/admin/places";
								}).catch((err) => errorMessage = err.message);
							}
						};
						modalVisible = true;
					}}
				>
					<TrashIcon /> Delete Place
				</button>

				<div class="card mb-2 mt-3">
					<div class="card-body card-header"><h4 class="mb-0">Place Management</h4></div>
				</div>
				<a class="btn-outline-dark btn w-100 mb-2" href={`/admin-api/api/games/${placeId}/download`}>
					<DownloadIcon /> Download Place RBXL
				</a>
				<a class="btn-outline-dark btn w-100" target="_blank" href={`/games/${placeId}/Place`}>
					<LinkIcon /> View BubbaBlox Place
				</a>
			</div>
		</div>
	{:catch e}
		<p>Error loading place info</p>
	{/await}
</Main>
